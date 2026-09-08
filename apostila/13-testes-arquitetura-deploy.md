# 13 — Testes, arquitetura e deploy

Fecha seu tópico 7 e a apostila: nada dos módulos anteriores vale se não for testável e se o
sistema não rodar de forma reproduzível. Este é o checkpoint final — o backend do Hub
completo, testado, em containers.

## xUnit — fundamentos

### Conceito
Framework de testes padrão do ecossistema .NET moderno — você já vem usando desde o
[módulo 05](05-modelagem-de-dominio.md), aqui formaliza-se o vocabulário.

### Como funciona
```csharp
public class GrantTests
{
    [Fact]
    public void Approve_WhenRequested_TransitionsToApproved()
    {
        var grant = Grant.Request(InstallationId.New(), DateTimeOffset.UtcNow);

        grant.Approve(DateTimeOffset.UtcNow, TimeSpan.FromDays(30));

        Assert.Equal(GrantStatus.Approved, grant.Status);
    }

    [Theory]
    [InlineData(GrantStatus.Approved)]
    [InlineData(GrantStatus.Active)]
    [InlineData(GrantStatus.Denied)]
    public void Approve_WhenNotRequested_Throws(GrantStatus currentStatus)
    {
        var grant = CreateGrantInStatus(currentStatus);

        Assert.Throws<DomainException>(() => grant.Approve(DateTimeOffset.UtcNow, TimeSpan.FromDays(30)));
    }
}
```

`[Fact]` = um caso fixo. `[Theory]` + `[InlineData]` = o mesmo teste rodado com vários
valores — útil exatamente para testar "toda transição inválida" do
[módulo 06](06-maquinas-de-estado.md) sem repetir código.

### Equivalente C/C++ (e no seu v2 TS)
Mesmo padrão **Arrange-Act-Assert** que qualquer framework de teste segue (comparável ao seu
`vitest` no v2) — arrange (monta o cenário), act (executa a ação), assert (confere o
resultado).

### Exercício
Sem exercício isolado — você já vem escrevendo testes desde o módulo 05.

---

## Testes de integração: WebApplicationFactory e Testcontainers

### Conceito
Testes unitários (domínio, casos de uso com repositório fake) não tocam infraestrutura real.
Testes de **integração** validam o sistema com as peças de verdade — Postgres real, pipeline
HTTP real.

### Como funciona
```csharp
public class HubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<HubDbContext>>();
            services.AddDbContext<HubDbContext>(opts => opts.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _postgres.DisposeAsync();
}

public class GrantEndpointsTests(HubApiFactory factory) : IClassFixture<HubApiFactory>
{
    [Fact]
    public async Task RequestGrant_ThenApprove_ReturnsApprovedStatus()
    {
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/grants", new RequestGrantDto(installationId, "teste"));
        var grant = await createResponse.Content.ReadFromJsonAsync<GrantResponse>();

        var approveResponse = await client.PostAsync($"/grants/{grant!.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
    }
}
```

**Testcontainers** sobe um Postgres real em Docker só para a duração dos testes — mesmo
espírito do seu `vitest.integration.config.ts` no v2, testando contra infraestrutura real em
vez de mocks.

### Equivalente C/C++ (e no seu v2 TS)
Exatamente o par que você já usa no v2: testes unitários rápidos e isolados (domínio puro) +
testes de integração contra banco real (o que seus testes de integração em vitest já fazem).
A tradução para .NET troca a ferramenta, não a estratégia.

### Armadilha comum
Testar tudo só com repositório fake (rápido, mas nunca detecta um `HasQueryFilter` mal
configurado ou uma migration quebrada) ou só com integração (correto, mas lento e frágil como
suíte principal) — os dois níveis são complementares, não substitutos um do outro.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Clean Architecture — revisão

### Conceito
Recapitulando a estrutura de solution do [módulo 00](00-modelo-mental-e-setup.md), agora que
você construiu cada camada:

```text
Hub.Domain            -- zero dependências. Grant, Session, Hub, Installation (módulos 05-06)
   ↑
Hub.Application        -- depende só de Domain. Casos de uso, interfaces de repositório (módulo 09)
   ↑
Hub.Infrastructure       -- depende de Application. EF Core, Postgres, implementações (módulo 07)
   ↑
Hub.Api                  -- depende de Infrastructure. Endpoints, pipeline, DI wiring (módulo 08, 10)
```

A regra de dependência (sempre apontando "para dentro", em direção ao Domain) é o que torna o
domínio testável sem infraestrutura, e o que permitiria trocar Postgres por outra coisa sem
tocar em `Hub.Domain`/`Hub.Application`.

### Equivalente C/C++ (e no seu v2 TS)
A mesma fronteira `domain/application/infrastructure` que você já usa no v2 — a diferença é
que em C# a fronteira é **imposta pelo compilador** (referências de projeto), não só por
convenção de pastas.

### Monolito modular vs microsserviços

Para o Hub, comece com um **monolito modular**: um único `Hub.Api` deployável, mas com
fronteiras internas claras (a mesma separação em camadas acima, e potencialmente pastas por
módulo de domínio se o Hub crescer — `Grants/`, `Sessions/`, `Identity/` dentro de
`Hub.Domain`). Microsserviços resolvem problemas de escala de **time** e de **deploy
independente** — nenhum dos dois é o problema do Hub agora. Extrair um serviço depois, a
partir de fronteiras já limpas, é mecânico; desfazer um split prematuro é caro.

### Exercício
Sem exercício isolado.

---

## Docker e deploy

### Conceito
Empacotar a API e o Postgres de forma reproduzível — a mesma disciplina de ambiente que você
já pratica com containers no v2.

### Como funciona
```dockerfile
# Hub.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Hub.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Hub.Api.dll"]
```

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: hub
      POSTGRES_PASSWORD: devpassword
    ports: ["5432:5432"]

  api:
    build: .
    depends_on: [postgres]
    environment:
      ConnectionStrings__Default: "Host=postgres;Database=hub;Username=postgres;Password=devpassword"
    ports: ["8080:8080"]
```

Migrations em produção: rode `dbContext.Database.MigrateAsync()` na inicialização do `Hub.Api`
(aceitável para o estágio atual do projeto) ou como um passo explícito de deploy separado
(mais seguro conforme o time cresce, evita duas instâncias tentando migrar ao mesmo tempo).

### Exercício
Ver exercício-âncora final abaixo.

---

## Exercício-âncora final — o checkpoint de toda a apostila

1. Escreva `docker-compose.yml` com Postgres + `Hub.Api`.
2. Configure `HubApiFactory` com Testcontainers para os testes de integração.
3. Rode `dotnet test` na solution inteira — domínio ([05](05-modelagem-de-dominio.md)/[06](06-maquinas-de-estado.md)),
   casos de uso com fake repository ([09](09-fronteiras-de-api.md)), e integração (este
   módulo) — tudo verde.
4. Suba `docker compose up`, e via HTTP (curl/`.http` file/Postman) execute o fluxo completo:
   ```text
   1. autentique-se (endpoint de dev token do módulo 10)
   2. instale um App no Hub do workspace
   3. solicite um Grant para essa installation
   4. aprove o Grant (exige policy "CanApproveGrant" — módulo 10)
   5. inicie uma Session a partir do Grant aprovado
   6. force expires_at para o passado (ajuste manual pra teste) e aguarde o
      ExpirationWorker (módulo 11) expirar a session sozinho
   7. dispare um impersonation e confirme o audit event append-only +
      as notificações fan-out (módulo 12)
   ```
5. Confirme, para cada peça do fluxo acima, que você consegue explicar **o que está
   acontecendo por baixo** — não só que "funcionou": onde está o lock otimista agindo, onde a
   máquina de estados barrou uma transição inválida se você tentar forçar uma, onde o
   middleware de authz rejeitaria um usuário sem a permissão.

Critério de pronto: o fluxo completo roda de ponta a ponta em containers, os testes
(unitários + integração) passam, e o sistema tem exatamente as garantias que o
[módulo 00](00-modelo-mental-e-setup.md) prometeu no início. Este é o sinal de que a apostila
cumpriu seu objetivo — a partir daqui, o ciclo `estuda → implementa → dúvida → volta` continua
sozinho, guiado pelo que o Hub de Apps pedir a seguir.
