# 11 — Concorrência e workers

Seu tópico 5. O caso concreto: **nada vai clicar num botão para expirar um grant ou uma
session** — `expires_at` só tem efeito se algum processo, sozinho, o observar e agir. Isso é
trabalho de **background worker**, e onde há escrita concorrente, é trabalho do lock otimista
do [módulo 07](07-ef-core-multitenant.md), com retry.

## Async a fundo: ThreadPool starvation e deadlocks

### Conceito
Retomando o [módulo 03](03-runtime-dotnet.md): dois problemas clássicos de código assíncrono
mal escrito, agora relevantes porque um worker roda por muito mais tempo que uma request HTTP.

### Como funciona
- **Deadlock**: misturar código síncrono e assíncrono com `.Result`/`.Wait()` pode travar
  para sempre, especialmente em contextos com `SynchronizationContext` (menos comum em
  ASP.NET Core moderno, que não tem `SynchronizationContext` por padrão — mas ainda um hábito
  a evitar).
- **ThreadPool starvation**: usar `Task.Run` para I/O ou bloquear threads do pool com trabalho
  síncrono pesado esgota o pool — novas requisições/timers ficam esperando uma thread livre.

```csharp
// ERRADO — bloqueia a thread, risco de deadlock
var result = SomeAsyncMethod().Result;

// CERTO — propaga async até o topo
var result = await SomeAsyncMethod();
```

### Equivalente C/C++
Parecido com um deadlock clássico entre duas threads esperando um mutex uma da outra — só que
aqui é entre a thread chamadora e o contexto que o `await` precisaria retomar.

### Exercício
Sem exercício isolado — disciplina aplicada no resto do módulo.

---

## BackgroundService / IHostedService

### Conceito
Um serviço de longa duração, hospedado dentro do mesmo processo da API, que roda em paralelo
ao pipeline HTTP — o lugar certo para "expirar grants/sessions vencidos" rodar sozinho.

### Como funciona
```csharp
public class ExpirationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireOverdueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // um ciclo falhar não pode matar o worker inteiro — logue e tente de novo no próximo tick
                logger.LogError(ex, "Falha ao expirar grants/sessions.");
            }
        }
    }

    private async Task ExpireOverdueAsync(CancellationToken ct)
    {
        // BackgroundService vive por toda a aplicação (singleton) — mas DbContext é Scoped
        // (módulo 04). Por isso criamos um escopo novo a cada ciclo, em vez de injetar
        // HubDbContext direto no construtor do worker.
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var now = clock.GetUtcNow();

        var overdueGrants = await dbContext.Grants
            .Where(g => g.Status == GrantStatus.Active && g.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (var grant in overdueGrants)
            grant.Expire(now);   // método de domínio do módulo 06 — a regra mora lá, não aqui

        await dbContext.SaveChangesAsync(ct);
    }
}
```

```csharp
builder.Services.AddHostedService<ExpirationWorker>();
```

### Equivalente C/C++
Comparável a um processo/thread daemon que você rodaria com um timer (`alarm`/loop com
`sleep`) checando periodicamente uma condição — só que integrado ao ciclo de vida do host
(inicia com a aplicação, recebe `stoppingToken` para desligar graciosamente).

### Armadilha comum
Injetar `HubDbContext` **diretamente** no construtor de um `BackgroundService` — o worker é
`Singleton` (vive a aplicação inteira), mas `DbContext` é `Scoped`
([módulo 04](04-di-config-logging.md)); misturar os dois é exatamente o "captive dependency"
já visto lá. Sempre crie um `IServiceScope` novo a cada ciclo de trabalho, como acima.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Corrida em `hubs.version`: detectar e resolver com retry

### Conceito
Retomando o [módulo 07](07-ef-core-multitenant.md): `DbUpdateConcurrencyException` **detecta**
o conflito — cabe ao código decidir o que fazer. Para um worker automático (ninguém está
esperando na tela), a resposta certa costuma ser **recarregar e tentar de novo**.

### Como funciona
```csharp
private async Task<bool> TryExpireWithRetryAsync(GrantId id, DateTimeOffset now, HubDbContext dbContext, CancellationToken ct)
{
    const int maxAttempts = 3;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        var grant = await dbContext.Grants.FindAsync([id], ct);
        if (grant is null) return false;

        try
        {
            grant.Expire(now);
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
        {
            // outra escrita venceu a corrida — descarta o tracking local e tenta de novo
            dbContext.Entry(grant).State = EntityState.Detached;
        }
    }

    return false;
}
```

### Equivalente C/C++
O mesmo padrão de **retry com backoff** que você aplicaria em C sobre uma operação
lock-free/CAS que falhou por causa de outra thread ter vencido a corrida — tentar de novo é a
resposta correta, não um bug.

### Armadilha comum
Fazer retry **infinito** sem limite (`while(true)`) pode travar um worker inteiro se duas
fontes ficarem competindo indefinidamente pelo mesmo registro — sempre limite tentativas e
logue quando esgotar.

### Idempotência do worker

### Conceito
Um worker que roda periodicamente **vai**, eventualmente, processar o mesmo registro mais de
uma vez (crash no meio de um ciclo, dois pods rodando o mesmo worker, etc.) — o código precisa
tolerar isso sem efeito duplicado.

### Como funciona
No exemplo acima, isso já é garantido "de graça": `grant.Expire(now)` é uma transição guardada
([módulo 06](06-maquinas-de-estado.md)) que só age se `Status == Active` — rodar duas vezes
sobre o mesmo grant já expirado simplesmente não encontra mais grants `Active` com
`ExpiresAt <= now` na query, então não faz nada na segunda vez.

### Armadilha comum
Se o worker também dispara notificações (módulo 12), a idempotência da **transição de estado**
não protege sozinha contra **notificar duas vezes** — isso precisa de sua própria proteção
(ex: registrar que a notificação já foi enviada antes de reenviar).

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Exercício-âncora do módulo

1. Implemente `ExpirationWorker : BackgroundService` como acima, cobrindo tanto `Grant` quanto
   `Session` vencidos (`ExpiresAt <= now` e `Status` ativo).
2. Registre-o com `AddHostedService` no `Hub.Api`.
3. Adicione retry com limite (`TryExpireWithRetryAsync` ou equivalente) para tratar
   `DbUpdateConcurrencyException`.
4. Escreva um teste de integração que força a corrida deliberadamente: crie um `Grant` já
   vencido, abra dois `DbContext`s simulando o worker rodando "duas vezes" quase ao mesmo
   tempo sobre o mesmo grant, e confirme que exatamente uma das duas tentativas ganha e a
   outra recebe `DbUpdateConcurrencyException` (ou, com retry, ambas convergem para o mesmo
   estado final sem erro não tratado).
5. Rode o worker manualmente (`dotnet run`, esperando o `PeriodicTimer`) contra dados de teste
   e confirme nos logs que grants/sessions vencidos são expirados sozinhos, sem nenhuma
   chamada HTTP.

Critério de pronto: um `Grant`/`Session` com `expires_at` no passado é expirado
automaticamente pelo worker dentro de um ciclo do timer, e a corrida concorrente do item 4 não
deixa o sistema num estado inconsistente.

Próximo módulo: **12 — Segurança, auditoria e mensageria**.
