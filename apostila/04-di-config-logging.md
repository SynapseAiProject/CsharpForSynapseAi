# 04 — DI, Configuration e Logging

O `composition/` do seu v2 em TypeScript já é, na prática, injeção de dependência manual
("wire tudo na mão"). O .NET tem um **container de DI nativo** que faz essa fiação
automaticamente — este módulo mostra como pensar nele.

## Dependency Injection nativo

### Conceito
Um container que resolve e constrói suas dependências automaticamente, a partir de um
"registro" central (`IServiceCollection`).

### Como funciona
```csharp
var services = new ServiceCollection();

services.AddSingleton<ITodoRepository, InMemoryTodoRepository>();
services.AddScoped<ITodoService, TodoService>();
services.AddTransient<IIdGenerator, GuidIdGenerator>();

using var provider = services.BuildServiceProvider();
var todoService = provider.GetRequiredService<ITodoService>();
```

`TodoService` pode simplesmente declarar o que precisa no construtor — o container resolve
sozinho:

```csharp
public class TodoService : ITodoService
{
    private readonly ITodoRepository _repository;
    public TodoService(ITodoRepository repository) => _repository = repository;
    // ...
}
```

### Equivalente C/C++ (e no seu v2 TS)
Isso é exatamente o que seu `composition/` no v2 faz manualmente: instanciar cada
implementação concreta e passar pelas interfaces esperadas nos construtores (constructor
injection). Aqui, você só **registra** a receita (`interface → implementação` + lifetime) uma
vez, e o container monta a árvore de dependências pra qualquer coisa que precisar dela — sem
você escrever `new TodoService(new InMemoryTodoRepository())` espalhado pelo código.

### Armadilha comum
DI nativo do .NET **não resolve dependências circulares** (`A` precisa de `B`, `B` precisa de
`A`) — isso é sinal de design ruim (viole a Dependency Inversion de propósito, não around).

### Exercício
Sem exercício isolado — usado no exercício de módulo.

---

## Lifetimes: Transient, Scoped, Singleton

### Conceito
Controla **quando** o container cria uma nova instância.

| Lifetime | Quando cria uma instância nova | Equivalente mental |
|---|---|---|
| `Transient` | toda vez que é pedida | `new X()` a cada uso |
| `Scoped` | uma vez por "escopo" (numa API, por request HTTP) | um objeto por requisição |
| `Singleton` | uma vez, para a vida inteira da aplicação | uma instância global, mas gerida pelo container |

### Equivalente C/C++
`Singleton` ≈ um objeto global/estático (mas sem os problemas de inicialização estática
implícita de C++ — o container controla a ordem). `Scoped` não tem equivalente direto em C/C++
puro — é um conceito de "vida da requisição" que só faz sentido com um framework web por trás.

### Armadilha comum
**Captive dependency**: injetar um serviço `Scoped` ou `Transient` dentro de um `Singleton`
faz esse serviço "vazar" e viver para sempre, junto com o singleton — quebrando a expectativa
de que ele seria recriado por request. No Hub, o `DbContext` do EF Core é sempre `Scoped`
(um por request) — nunca torne algo que depende dele em `Singleton`.

### Exercício
Sem exercício isolado — usado no exercício de módulo.

---

## Configuration e Options pattern

### Conceito
`IConfiguration` unifica várias fontes de configuração (JSON, variáveis de ambiente, secrets,
argumentos de linha de comando) numa única árvore de chave/valor.

### Como funciona
```json
// appsettings.json
{
  "Database": {
    "ConnectionString": "Host=localhost;Database=hub;Username=hub;Password=..."
  }
}
```

```csharp
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
}

services.Configure<DatabaseOptions>(configuration.GetSection("Database"));

// em qualquer serviço:
public class MyService(IOptions<DatabaseOptions> options)
{
    private readonly string _connectionString = options.Value.ConnectionString;
}
```

Fontes de configuração são aplicadas **em camadas**, na ordem em que são adicionadas —
variáveis de ambiente e secrets normalmente sobrescrevem o `appsettings.json` em produção.

### Equivalente C/C++
Parecido com ler variáveis de ambiente (`getenv`) e arquivos de config manualmente, mas
unificado: o código de negócio não sabe (nem precisa saber) se o valor veio de um JSON, de uma
env var, ou de um secret store — só pede `IOptions<DatabaseOptions>`.

### Armadilha comum
Nunca commite secrets (connection strings com senha, chaves JWT) no `appsettings.json`. Use
`dotnet user-secrets` em desenvolvimento e variáveis de ambiente/secret manager em produção —
aprofundado no [módulo 12](12-seguranca-auditoria-mensageria.md).

### Exercício
Sem exercício isolado — usado no exercício de módulo.

---

## Logging

### Conceito
`ILogger<T>` é a abstração de log da BCL, injetada como qualquer outra dependência.

### Como funciona
```csharp
public class TodoService(ILogger<TodoService> logger, ITodoRepository repository)
{
    public async Task CompleteAsync(TodoId id)
    {
        logger.LogInformation("Completando todo {TodoId}", id);   // structured logging
        // ...
    }
}
```

Structured logging: `{TodoId}` não é interpolação de string — é um **campo nomeado**, que
provedores de log (console, arquivo, Seq, Application Insights...) podem indexar e consultar
depois, sem parsear texto.

### Equivalente C/C++
Muito além de `printf`/`fprintf(stderr, ...)`. O ganho real é a estrutura: em produção, você
consulta "todos os logs com `TodoId = X`" em vez de fazer grep em texto solto.

### Armadilha comum
Nunca logue segredos (senhas, tokens, `token_hash` incluso) — trate log como algo que pode
vazar para múltiplos sistemas.

### Exercício
Sem exercício isolado — usado no exercício de módulo.

---

## Exercício do módulo — composition root do Todo com DI

Reescreva a montagem do Todo CLI (dos módulos [01](01-csharp-essencial.md)–[03](03-runtime-dotnet.md))
usando o **Generic Host**:

```csharp
using var host = Host.CreateApplicationBuilder(args).Build();
```

1. Registre `ITodoRepository`/implementação SQLite como `Singleton` (uma conexão para o app
   inteiro, ou `Scoped` se você preferir simular vida de "requisição" por comando).
2. Registre um `TodoService` que depende de `ITodoRepository` e `ILogger<TodoService>`.
3. Mova a connection string do SQLite para `appsettings.json`, lida via `IOptions<DatabaseOptions>`
   em vez de hardcoded.
4. Troque todo `Console.WriteLine` de diagnóstico por `ILogger<T>` (structured logging).
5. Resolva o `TodoService` a partir do host (`host.Services.GetRequiredService<TodoService>()`)
   no loop principal, em vez de instanciar manualmente.

Critério de pronto: nenhuma classe do domínio faz `new` de suas próprias dependências — tudo é
injetado pelo container, registrado uma vez no ponto de composição.

Próximo módulo: **05 — Modelagem de domínio**.
