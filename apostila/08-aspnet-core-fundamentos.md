# 08 — ASP.NET Core fundamentos

A transição direta do seu Webserver em C++ (sockets, epoll, parsing HTTP manual) para um
framework que já resolve essa camada — mas com o mesmo modelo mental de pipeline que você já
implementou na mão uma vez.

## O Host e o pipeline HTTP

### Conceito
`WebApplication.CreateBuilder()` monta um **Generic Host** ([módulo 04](04-di-config-logging.md))
especializado para web: DI, Configuration, Logging, e um servidor HTTP (**Kestrel**) tudo
integrado.

### Como funciona
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HubDbContext>(opts => opts.UseNpgsql(connectionString));
builder.Services.AddScoped<ITodoService, TodoService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/hubs/{id}", async (Guid id, HubDbContext db) =>
{
    var hub = await db.Hubs.FindAsync(new HubId(id));
    return hub is not null ? Results.Ok(hub) : Results.NotFound();
});

app.Run();
```

### Equivalente C/C++
`Kestrel` faz o papel do seu loop de `epoll`/`accept`/`recv` no Webserver em C++ — aceita
conexões TCP, faz o parsing HTTP, e entrega uma requisição já estruturada
(`HttpContext`) para o seu código. Você não escreve mais parsing de request line/headers na
mão.

> 🔬 **Aprofundamento opcional** — Kestrel é implementado sobre o mesmo modelo de I/O
> assíncrono do [módulo 03](03-runtime-dotnet.md) (não bloqueia threads esperando socket). Em
> produção, normalmente fica atrás de um reverse proxy (nginx, YARP) para TLS
> termination/balanceamento — mas o modelo de pipeline é o mesmo.

### Exercício
Sem exercício isolado — usado abaixo.

---

## Middleware pipeline

### Conceito
Uma cadeia de componentes que toda requisição atravessa, em ordem — cada um decide processar,
modificar, e/ou passar adiante (`next()`).

### Como funciona
```text
Request
  ↓
UseExceptionHandler     -- captura exceptions não tratadas, vira resposta padronizada
  ↓
UseHttpsRedirection
  ↓
UseAuthentication        -- identifica QUEM está fazendo a request (lê o JWT)
  ↓
UseAuthorization          -- decide SE essa pessoa pode fazer isso (módulo 10)
  ↓
Routing → Endpoint         -- o handler específico da rota
  ↓
Response
```

**Ordem importa**: `UseAuthorization` antes de `UseRouting`/mapeamento de endpoint não faz
sentido (ainda não se sabe qual endpoint, logo não se sabe qual policy checar). Cada
middleware pode agir tanto na "ida" quanto na "volta" da requisição (como um envelope):

```csharp
app.Use(async (context, next) =>
{
    logger.LogInformation("→ {Method} {Path}", context.Request.Method, context.Request.Path);
    await next(context);                                    // chama o próximo da cadeia
    logger.LogInformation("← {StatusCode}", context.Response.StatusCode);
});
```

### Equivalente C/C++
Exatamente o padrão de cadeia de handlers que você provavelmente já usou/consideraria no
Webserver em C++ para processar uma request em estágios (parse → roteamento → handler →
resposta) — ASP.NET Core formaliza isso como abstração de primeira classe da framework.

### Armadilha comum
Um middleware que não chama `next()` **interrompe a cadeia** — às vezes intencional (um
middleware de autenticação que rejeita a requisição antes do endpoint), às vezes um bug
(esquecer o `await next(context)`).

### Exercício
Sem exercício isolado — usado abaixo.

---

## Minimal APIs vs Controllers

### Conceito
Duas formas de declarar endpoints. **Minimal APIs** (`app.MapGet/MapPost/...`) são funções
diretas; **Controllers** (`[ApiController]` + classes com `[HttpGet]`) são o modelo mais
tradicional, orientado a classes.

### Como funciona
```csharp
// Minimal API
app.MapPost("/workspaces/{workspaceId}/grants", async (
    Guid workspaceId, RequestGrantDto dto, IGrantService grantService) =>
{
    var grant = await grantService.RequestAsync(new WorkspaceId(workspaceId), dto);
    return Results.Created($"/grants/{grant.Id}", grant);
});
```

```csharp
// Controller equivalente
[ApiController]
[Route("workspaces/{workspaceId}/grants")]
public class GrantsController(IGrantService grantService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Request(Guid workspaceId, RequestGrantDto dto)
    {
        var grant = await grantService.RequestAsync(new WorkspaceId(workspaceId), dto);
        return Created($"/grants/{grant.Id}", grant);
    }
}
```

**Recomendação para o Hub**: Minimal APIs, agrupadas por área com `MapGroup(...)` — menos
cerimônia, e o projeto já separa casos de uso na `Application layer`
([módulo 09](09-fronteiras-de-api.md)), então o controller/endpoint fica fino de qualquer
jeito.

### Equivalente C/C++
Ambos são, no fim, "rota → função handler" — a mesma tabela de dispatch que você escreveria
manualmente mapeando path+método para uma função no Webserver em C++.

### Armadilha comum
Colocar lógica de negócio direto dentro do lambda do `MapPost` — o endpoint deveria só
traduzir HTTP ↔ chamada de caso de uso. Aprofundado no [módulo 09](09-fronteiras-de-api.md).

### Exercício
Ver exercício progressivo no fim do módulo.

---

## Model binding, DTOs e validação

### Conceito
ASP.NET Core desserializa o corpo/query/rota da requisição automaticamente para um objeto
C# (**model binding**) — você declara o formato esperado com um DTO.

### Como funciona
```csharp
public record RequestGrantDto(Guid InstallationId, string Justification);

app.MapPost("/grants", (RequestGrantDto dto) => { /* dto já vem populado do JSON do body */ });
```

Validação básica com Data Annotations:

```csharp
public record RequestGrantDto(
    [property: Required] Guid InstallationId,
    [property: MaxLength(500)] string Justification);
```

Minimal APIs não validam annotations automaticamente (ao contrário de Controllers com
`[ApiController]`) — para o Hub, prefira validação explícita no início do caso de uso
(módulo 09) ou um filtro de endpoint (`.AddEndpointFilter`), mantendo a regra visível e
testável.

### Equivalente C/C++
Isso substitui o parsing manual de JSON/query string que você faria em C — sem
`sscanf`/parsing de buffer à mão, e com checagem de tipo antes mesmo do seu código rodar
(uma request com `InstallationId` que não é um GUID válido nem chega no seu handler — vira
`400` automaticamente).

### Armadilha comum
Nunca faça model binding de uma requisição **direto para uma entidade de domínio**
(`app.MapPost("/hubs", (Hub hub) => ...)`) — isso convida o cliente HTTP a preencher campos
que deveriam ser controlados só pelo domínio (como `Version`). Sempre um DTO próprio de
entrada, mapeado explicitamente. Aprofundado no [módulo 09](09-fronteiras-de-api.md).

### Exercício
Ver exercício progressivo no fim do módulo.

---

## Serialização e ProblemDetails

### Conceito
`System.Text.Json` serializa/desserializa objetos ↔ JSON automaticamente nos endpoints.
`ProblemDetails` é o formato padronizado (RFC 7807) de resposta de erro.

### Como funciona
```csharp
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var (status, title) = feature?.Error switch
        {
            DomainException => (StatusCodes.Status400BadRequest, feature.Error.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno."),
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
        });
    });
});
```

Isso conecta direto com o [módulo 05](05-modelagem-de-dominio.md): `DomainException` vira
`400` de forma centralizada — nenhum endpoint precisa de `try`/`catch` individual para isso.

### Equivalente C/C++
Comparável a centralizar a montagem da resposta HTTP de erro num único lugar do seu Webserver
em C++, em vez de formatar a resposta manualmente em cada handler.

### Exercício
Ver exercício progressivo no fim do módulo.

---

## Exercício progressivo do módulo

1. **Neutro**: pegue o Todo CLI ([módulos 01–04](01-csharp-essencial.md)) e exponha-o como uma
   API HTTP mínima (`Hub.Api` ou um projeto `Todo.Api` à parte) — `GET /todos`,
   `POST /todos`, `POST /todos/{id}/complete`, `DELETE /todos/{id}`, usando Minimal APIs e o
   `TodoService` já registrado via DI.
2. **Âncora**: no projeto `Hub.Api`, monte o pipeline completo (`UseExceptionHandler` com
   tradução `DomainException → 400`, `UseAuthentication`/`UseAuthorization` mesmo que ainda
   sem policies reais) e exponha os primeiros endpoints reais do Hub:
   - `GET /workspaces/{workspaceId}/hub` — retorna o Hub e suas installations.
   - `POST /workspaces/{workspaceId}/hub/installations` — instala um App
     (`hub.Install(app)` do [módulo 05](05-modelagem-de-dominio.md), persistido via
     `HubDbContext` do [módulo 07](07-ef-core-multitenant.md)).
   - Um `RequestGrantDto`/`POST /grants` chamando `Grant.Request(...)` do
     [módulo 06](06-maquinas-de-estado.md).

Critério de pronto: `dotnet run --project src/Hub.Api`, e via `curl`/Postman/`.http` file você
consegue instalar um App num Hub e solicitar um Grant, recebendo `DomainException`s traduzidas
para `400` com mensagem clara quando violar uma invariante (ex: instalar o mesmo app duas
vezes).

Próximo módulo: **09 — Fronteiras de API**.
