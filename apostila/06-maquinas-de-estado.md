# 06 — Máquinas de estado

Seu tópico 2. Continuação direta do [módulo 05](05-modelagem-de-dominio.md): agora o foco é
**status como contrato de transições válidas**, não uma string ou enum livre que qualquer
código pode setar para qualquer valor.

## Por que um enum sozinho não basta

### Conceito
Um `enum GrantStatus { Requested, Approved, Active, Expired, Revoked, Denied }` sozinho
**não impede** `grant.Status = GrantStatus.Active` a partir de `Denied` — ele só nomeia os
valores possíveis, não as transições permitidas entre eles.

### Como funciona
A regra do Hub de Apps (do [módulo 00](00-modelo-mental-e-setup.md)):

```text
grant_status:   Requested → Approved → Active → Expired
                                     ↘ Revoked
                          ↘ Denied

session_status: Active → Ended
                       ↘ Expired
                       ↘ Revoked
```

Isso é uma **máquina de estados finita**: um conjunto de estados + um conjunto de transições
válidas entre eles. O trabalho é fazer essa tabela existir **em código executável**, não só
neste diagrama.

### Equivalente C/C++
Comparável a implementar uma FSM explícita em C (um `switch` sobre estado atual + evento, com
uma tabela de transições) — a diferença é que em C# usamos o próprio sistema de tipos e
métodos para tornar transições inválidas **inexpressáveis**, não apenas verificadas em
runtime.

### Exercício
Sem exercício isolado — usado abaixo.

---

## Transições guardadas: o estado só muda através de métodos que validam

### Conceito
Cada transição é um **método** no aggregate, que checa o estado atual antes de mudar — nunca
um setter público de `Status`.

### Como funciona
```csharp
public enum GrantStatus { Requested, Approved, Active, Expired, Revoked, Denied }

public sealed class Grant
{
    public GrantId Id { get; }
    public InstallationId InstallationId { get; }
    public GrantStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private Grant(GrantId id, InstallationId installationId, DateTimeOffset requestedAt)
    {
        Id = id;
        InstallationId = installationId;
        Status = GrantStatus.Requested;
        RequestedAt = requestedAt;
    }

    public static Grant Request(InstallationId installationId, DateTimeOffset now)
        => new(GrantId.New(), installationId, now);

    public void Approve(DateTimeOffset now, TimeSpan validFor)
    {
        EnsureStatus(GrantStatus.Requested);
        Status = GrantStatus.Approved;
        ApprovedAt = now;
        ExpiresAt = now + validFor;
    }

    public void Activate(DateTimeOffset now)
    {
        EnsureStatus(GrantStatus.Approved);
        Status = GrantStatus.Active;
    }

    public void Deny(DateTimeOffset now)
    {
        EnsureStatus(GrantStatus.Requested);
        Status = GrantStatus.Denied;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status is not (GrantStatus.Approved or GrantStatus.Active))
            throw new DomainException($"Não é possível revogar um grant em status {Status}.");

        Status = GrantStatus.Revoked;
        RevokedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        EnsureStatus(GrantStatus.Active);
        if (ExpiresAt is null || now < ExpiresAt)
            throw new DomainException("Grant ainda não atingiu expires_at.");

        Status = GrantStatus.Expired;
    }

    private void EnsureStatus(GrantStatus expected)
    {
        if (Status != expected)
            throw new DomainException(
                $"Transição inválida: esperava status {expected}, encontrado {Status}.");
    }
}
```

Cada método representa uma **intenção de negócio** (`Approve`, `Deny`, `Revoke`, `Expire`) —
nunca `grant.Status = GrantStatus.Active` de fora. A transição inválida não é "um bug que os
testes pegam depois" — é uma exception lançada no exato ponto onde alguém tentou.

### Equivalente C/C++
É o mesmo espírito de uma FSM com `assert`/checagem de precondição no início de cada função de
transição — só que aqui a exception é o mecanismo de "falha alto e claro" (fail-fast) em vez
de um `assert` que pode estar desligado em release.

### Armadilha comum
Colocar a lógica de transição **fora** da entidade (ex: num serviço de aplicação que faz
`if (grant.Status == Requested) grant.Status = Approved`) recria o problema do enum livre —
qualquer outro serviço pode fazer a mesma checagem errada ou esquecer de checar. A regra
**tem que morar dentro do `Grant`**.

### 🔬 Pergunta profunda (opcional)
> Vale a pena um framework de state machine (ex: `Stateless`)? Para duas máquinas de estado
> pequenas como `GrantStatus`/`SessionStatus`, não — o custo de aprender a API de uma lib
> supera o benefício. Frameworks de FSM compensam quando há dezenas de estados/transições ou
> quando o fluxo precisa ser configurável em runtime. Não é o caso aqui.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Session: escopo nunca maior que o Grant

### Conceito
Uma invariante **entre agregados**: `Session` referencia um `Grant`, e seu escopo/tempo de
vida nunca pode ultrapassar o do grant que a originou.

### Como funciona
```csharp
public enum SessionStatus { Active, Ended, Expired, Revoked }

public sealed class Session
{
    public SessionId Id { get; }
    public GrantId GrantId { get; }
    public SessionStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    private Session(SessionId id, GrantId grantId, DateTimeOffset startedAt, DateTimeOffset expiresAt)
    {
        Id = id;
        GrantId = grantId;
        Status = SessionStatus.Active;
        StartedAt = startedAt;
        ExpiresAt = expiresAt;
    }

    // O chamador (application layer) é responsável por só invocar isto com um Grant
    // Active — a checagem de "o grant permite abrir sessão" mora no caso de uso
    // (módulo 09), porque cruza dois agregados. Aqui, a invariante que O PRÓPRIO
    // Session garante é: nunca expira depois do grant.
    public static Session StartFor(Grant grant, DateTimeOffset now, TimeSpan duration)
    {
        if (grant.Status != GrantStatus.Active)
            throw new DomainException("Só é possível iniciar sessão para um grant ativo.");

        var requestedExpiry = now + duration;
        var cappedExpiry = grant.ExpiresAt is { } grantExpiry && requestedExpiry > grantExpiry
            ? grantExpiry
            : requestedExpiry;

        return new Session(SessionId.New(), grant.Id, now, cappedExpiry);
    }

    public void End(DateTimeOffset now)
    {
        EnsureActive();
        Status = SessionStatus.Ended;
        EndedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        EnsureActive();
        Status = SessionStatus.Revoked;
        EndedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        EnsureActive();
        if (now < ExpiresAt)
            throw new DomainException("Session ainda não atingiu expires_at.");

        Status = SessionStatus.Expired;
        EndedAt = now;
    }

    private void EnsureActive()
    {
        if (Status != SessionStatus.Active)
            throw new DomainException($"Session não está ativa (status atual: {Status}).");
    }
}
```

### Equivalente C/C++
Nenhuma novidade de sintaxe aqui — é a mesma disciplina de invariante-no-construtor do
[módulo 05](05-modelagem-de-dominio.md), aplicada a uma regra que cruza dois objetos.

### Armadilha comum
Decidir "o grant permite abrir sessão" (checar se `Grant.Status == Active`, se já não passou
do limite de sessions concorrentes etc.) é uma regra que envolve **buscar** o grant e o
histórico de sessions — isso é responsabilidade da **application layer** (caso de uso), não
do construtor do `Session` sozinho, porque cruzar agregados normalmente significa consultar
repositórios. O `Session.StartFor` acima só garante a invariante que ele consegue garantir
com os dados que já tem em mãos (o objeto `Grant` passado). Aprofundado no
[módulo 09](09-fronteiras-de-api.md).

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Exercício-âncora do módulo

No projeto `Hub.Domain`:

1. Implemente `Grant` com as transições `Request`/`Approve`/`Activate`/`Deny`/`Revoke`/`Expire`
   exatamente como acima (ou refine o design, mas mantendo transições guardadas).
2. Implemente `Session` com `StartFor`/`End`/`Revoke`/`Expire`, garantindo que
   `Session.ExpiresAt` nunca ultrapasse `Grant.ExpiresAt`.
3. No `Hub.Tests`, escreva testes exaustivos de transição — para cada estado, teste:
   - As transições **válidas** funcionam e alteram o status esperado.
   - **Todas** as transições inválidas a partir daquele estado lançam `DomainException`
     (ex: `Expire()` chamado antes de `expires_at` ser atingido; `Approve()` chamado num
     grant já `Approved`).
4. Teste explicitamente: uma `Session` iniciada com `duration` maior que o tempo restante do
   `Grant` é "capada" no `ExpiresAt` do grant, nunca ultrapassando.

Critério de pronto: `dotnet test` verde, cobrindo toda transição válida e pelo menos uma
transição inválida por estado — você deve conseguir olhar pro diagrama do
[módulo 00](00-modelo-mental-e-setup.md) e apontar o teste correspondente a cada seta.

Próximo módulo: **07 — EF Core multi-tenant**.
