# 12 — Segurança, auditoria e mensageria

Seu tópico 6. Duas garantias não-negociáveis do Hub: **o tenant sempre sabe que foi acessado**,
e **o log de auditoria nunca mente** — nem por bug, nem por escrita concorrente, nem por
alguém "corrigindo" um registro depois.

## Hashing de tokens: nunca guardar o valor cru

### Conceito
`tokens.token_hash` ([módulo 00](00-modelo-mental-e-setup.md)) nunca guarda o token que o
cliente recebe — só um hash dele. Se o banco vazar, os tokens continuam inúteis para um
atacante.

### Como funciona
```csharp
public static class TokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}

// emissão:
var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));   // gerado com CSPRNG
var tokenEntity = SessionToken.Issue(session.Id, TokenHasher.Hash(rawToken), expiresAt);
await tokenRepository.AddAsync(tokenEntity, ct);
return rawToken;   // devolvido ao cliente UMA VEZ — depois disso, só o hash existe no banco

// validação:
var candidateHash = TokenHasher.Hash(incomingRawToken);
var token = await tokenRepository.FindByHashAsync(candidateHash, ct);
```

### Equivalente C/C++
O mesmo princípio de nunca armazenar senha em texto puro (`/etc/shadow` guarda hash, não a
senha) — aqui aplicado a tokens de sessão. `RandomNumberGenerator` é o equivalente seguro de
gerar bytes aleatórios (não use `Random`/`rand()` para nada relacionado a segurança — não é
criptograficamente seguro).

### Armadilha comum
Usar `Random`/`Guid.NewGuid()` para gerar o token cru **não é necessariamente errado** para
`Guid` (é gerado com boa entropia na maioria das implementações), mas para tokens de sessão
prefira explicitamente `RandomNumberGenerator` (CSPRNG) — deixa a garantia de segurança
explícita no código, não dependente de detalhes de implementação da BCL.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Auditoria append-only com sequência monotônica

### Conceito
`impersonation_audit_events` nunca sofre `UPDATE`/`DELETE` — cada linha, uma vez escrita, é
definitiva. `sequence` garante uma ordem total, não ambígua, dos eventos.

### Como funciona
```csharp
public sealed class ImpersonationAuditEvent
{
    public long Sequence { get; }   // preenchido pelo banco (identity/sequence), não pelo código
    public MembershipId ActorMembershipId { get; }
    public UserId TargetUserId { get; }
    public string EventType { get; }
    public DateTimeOffset OccurredAt { get; }

    private ImpersonationAuditEvent(MembershipId actor, UserId target, string eventType, DateTimeOffset occurredAt)
    {
        ActorMembershipId = actor;
        TargetUserId = target;
        EventType = eventType;
        OccurredAt = occurredAt;
    }

    public static ImpersonationAuditEvent Record(MembershipId actor, UserId target, string eventType, DateTimeOffset now)
        => new(actor, target, eventType, now);

    // deliberadamente: NENHUM método de alteração além do construtor/factory.
    // Não existe Update() nem Delete() nesta classe.
}
```

No mapeamento EF Core ([módulo 07](07-ef-core-multitenant.md)), reforce isso no nível do
banco, não só por convenção de código:

```sql
-- na migration:
REVOKE UPDATE, DELETE ON impersonation_audit_events FROM app_user;
GRANT INSERT, SELECT ON impersonation_audit_events TO app_user;
```

E `Sequence` como coluna `GENERATED ALWAYS AS IDENTITY` (ou uma `SEQUENCE` dedicada do
Postgres) — o valor nunca é decidido pelo código C#, evitando qualquer chance de dois eventos
concorrentes reivindicarem o mesmo número.

### Equivalente C/C++ (e no seu v2 TS)
Comparável a um log append-only de verdade (como um write-ahead log) — a garantia vem de
**nunca oferecer a operação perigosa**, tanto na classe C# (sem método de update) quanto no
grant de permissões do banco (defesa em profundidade: mesmo um bug ou acesso direto ao banco
não consegue violar a regra).

### Armadilha comum
Confiar só na disciplina do código C# ("a classe não tem método de update, então está seguro")
ignora que outra parte do sistema (uma migration futura, um script administrativo, acesso
direto ao banco) pode violar a garantia se o banco permitir. Revogar `UPDATE`/`DELETE` no nível
do Postgres é o que torna a garantia real, não só convencional.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Mensageria: fan-out de notificações

### Conceito
`impersonation_notifications` distribui um evento de auditoria para múltiplos canais (email,
Slack, webhook) — um evento, N notificações.

### Como funciona
Para o Hub, a versão mais simples correta é o **outbox pattern**: gravar a intenção de
notificar na mesma transação do evento de auditoria, e processar o envio depois (via um
worker, igual ao [módulo 11](11-concorrencia-e-workers.md)) — assim uma falha ao "enviar
email" nunca perde o registro de que a notificação deveria acontecer.

```csharp
public async Task RecordImpersonationAsync(MembershipId actor, UserId target, CancellationToken ct)
{
    var auditEvent = ImpersonationAuditEvent.Record(actor, target, "impersonation_started", clock.GetUtcNow());
    await dbContext.ImpersonationAuditEvents.AddAsync(auditEvent, ct);

    foreach (var channel in new[] { "email", "slack" })
        await dbContext.ImpersonationNotifications.AddAsync(
            ImpersonationNotification.PendingFor(auditEvent, channel), ct);

    await dbContext.SaveChangesAsync(ct);   // evento + notificações pendentes, na MESMA transação
}
```

Um `NotificationDispatchWorker` (mesmo padrão do `ExpirationWorker`) processa
`ImpersonationNotifications` com status `Pending`, envia, e marca `Sent`/`Failed` —
idempotente pelo mesmo motivo do [módulo 11](11-concorrencia-e-workers.md): reprocessar um
`Sent` não faz nada.

### Equivalente C/C++
Comparável a enfileirar uma mensagem localmente (numa fila persistida) em vez de fazer a
chamada de rede síncrona dentro do fluxo principal — desacopla "registrar que aconteceu" de
"avisar todo mundo", que pode falhar por motivos completamente alheios ao domínio (Slack fora
do ar não deveria impedir o impersonation de ser registrado).

### Armadilha comum
Disparar a notificação **diretamente** dentro do mesmo método que grava o audit event (uma
chamada HTTP síncrona para o Slack, por exemplo) acopla a confiabilidade do seu log de
auditoria à disponibilidade de um serviço externo — se o Slack estiver fora do ar, o
impersonation não deveria falhar por causa disso.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Checklist de segurança geral

### Conceito
Itens que não são "um módulo", mas precisam estar cobertos antes de qualquer release.

| Item | Como o .NET ajuda |
|---|---|
| SQL Injection | EF Core parametriza automaticamente — nunca construa SQL por concatenação de string (`FromSqlRaw` com interpolação é a armadilha clássica; use sempre parâmetros) |
| HTTPS | `UseHttpsRedirection()` + certificado válido em produção |
| Secrets | `user-secrets` em dev, variável de ambiente/secret manager em produção — nunca no `appsettings.json` commitado ([módulo 04](04-di-config-logging.md)) |
| Rate limiting | Middleware nativo (`AddRateLimiter`, .NET 7+) — proteção básica contra abuso |
| CSRF | Baixo risco numa API pura consumida por SPA com Bearer token (diferente de auth por cookie) — reavalie se o Hub usar cookies de sessão |
| XSS | Responsabilidade principalmente do frontend (escapar output) — o backend contribui não refletindo input não sanitizado em respostas HTML (raro numa API JSON) |

### Exercício
Sem exercício isolado — verificado no checklist final do [módulo 13](13-testes-arquitetura-deploy.md).

---

## Exercício-âncora do módulo

1. Implemente `TokenHasher` e o fluxo de emissão/validação de `SessionToken` — o token cru só
   existe na resposta HTTP de criação de sessão, nunca mais depois disso.
2. Modele `ImpersonationAuditEvent` como classe append-only (sem métodos de alteração), mapeie
   `Sequence` como identity/sequence do Postgres, e adicione a migration que revoga
   `UPDATE`/`DELETE` na tabela para o usuário de aplicação.
3. Modele `ImpersonationNotification` com status (`Pending`/`Sent`/`Failed`) e implemente
   `RecordImpersonationAsync` gravando evento + notificações pendentes na mesma transação.
4. Implemente `NotificationDispatchWorker : BackgroundService` que processa notificações
   pendentes (pode simular o envio real com `logger.LogInformation` por enquanto — o objetivo
   é o mecanismo de fan-out + idempotência, não integrar com Slack/email de verdade).
5. Teste: tente, deliberadamente, fazer um `UPDATE` numa linha de `impersonation_audit_events`
   usando o usuário de aplicação (via `dbContext.Database.ExecuteSqlRaw` ou um teste de
   integração direto) e confirme que o banco rejeita.

Critério de pronto: um impersonation gera exatamente um audit event imutável e duas
notificações pendentes, processadas pelo worker sem duplicar envio se rodado mais de uma vez.

Próximo módulo: **13 — Testes, arquitetura e deploy**.
