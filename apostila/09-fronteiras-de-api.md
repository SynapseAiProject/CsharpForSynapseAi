# 09 — Fronteiras de API

Parte do seu tópico 7. O módulo anterior expôs endpoints de forma direta o suficiente para
funcionar — este módulo formaliza a fronteira: **a API nunca expõe a entidade EF diretamente**,
e a lógica de aplicação (casos de uso) mora num lugar próprio, testável sem HTTP.

## Por que nunca expor a entidade do EF direto

### Conceito
Uma entidade de domínio (`Hub`, `Grant`) é modelada para proteger invariantes internas
([módulo 05](05-modelagem-de-dominio.md)) — não para ser o formato de troca com o mundo
externo. Serializar `Grant` direto como resposta JSON acopla seu contrato de API à sua
modelagem interna.

### Como funciona
```csharp
// Formato interno (Hub.Domain) — pode mudar por razões internas de modelagem
public sealed class Grant { /* ... Value Objects, campos privados ... */ }

// Formato externo (Hub.Api ou Hub.Application) — contrato estável com o cliente
public record GrantResponse(Guid Id, Guid InstallationId, string Status, DateTimeOffset RequestedAt);

static GrantResponse ToResponse(Grant grant) =>
    new(grant.Id.Value, grant.InstallationId.Value, grant.Status.ToString(), grant.RequestedAt);
```

### Equivalente C/C++ (e no seu v2 TS)
Isso é a mesma disciplina do seu `interface/adaptors` no v2 — a camada de apresentação nunca
recebe a entidade de domínio crua, sempre um DTO traduzido. Em C, seria como nunca expor um
`struct` interno de um módulo pela API pública de uma biblioteca — você exporia uma versão
"achatada"/opaca.

### Armadilha comum
Serializar a entidade direto "funciona" no início (menos código!) até o dia em que você
precisa mudar a modelagem interna (ex: trocar `GrantId` de `Guid` para outra representação) e
descobre que quebrou o contrato que clientes de API já dependem. O mapeamento explícito é o
que separa as duas preocupações.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Application Services / Casos de uso

### Conceito
Uma camada entre a API e o domínio: cada caso de uso é um método/classe que orquestra
**um fluxo de negócio completo** — carrega entidades via repositório, chama os métodos de
domínio, persiste, e retorna um DTO.

### Como funciona
```csharp
// Hub.Application
public interface IGrantRepository
{
    Task<Grant?> FindAsync(GrantId id, CancellationToken ct);
    Task AddAsync(Grant grant, CancellationToken ct);
}

public class RequestGrantUseCase(
    IGrantRepository grants,
    IInstallationRepository installations,
    TimeProvider clock)
{
    public async Task<GrantResponse> ExecuteAsync(RequestGrantDto dto, CancellationToken ct)
    {
        var installation = await installations.FindAsync(new InstallationId(dto.InstallationId), ct)
            ?? throw new DomainException("Installation não encontrada.");

        var grant = Grant.Request(installation.Id, clock.GetUtcNow());
        await grants.AddAsync(grant, ct);

        return ToResponse(grant);
    }
}
```

O endpoint HTTP ([módulo 08](08-aspnet-core-fundamentos.md)) fica **fino** — só traduz
HTTP ↔ caso de uso:

```csharp
app.MapPost("/grants", async (RequestGrantDto dto, RequestGrantUseCase useCase, CancellationToken ct) =>
    Results.Ok(await useCase.ExecuteAsync(dto, ct)));
```

`IGrantRepository`/`IInstallationRepository` são **interfaces definidas em `Hub.Application`**
e implementadas em `Hub.Infrastructure` (com EF Core) — a application layer não sabe que existe
Postgres, só sabe que existe "um jeito de buscar/salvar um Grant". Essa é a mesma Dependency
Inversion do seu módulo Object/SOLID da 42, aplicada à fronteira entre camadas.

### Equivalente C/C++ (e no seu v2 TS)
Isso É o padrão que seus `application/` do v2 já seguem — casos de uso como unidade central,
dependendo de interfaces de repositório, não de implementações concretas. A tradução para C#
é quase 1:1 na estrutura, só muda a sintaxe (interfaces + DI container em vez de injeção
manual de módulos TS).

### Armadilha comum
Deixar o endpoint HTTP chamar o `DbContext`/repositório diretamente (como no exemplo rápido do
[módulo 08](08-aspnet-core-fundamentos.md)) funciona para prototipar, mas mistura
responsabilidades: o endpoint vira responsável tanto por HTTP quanto por orquestração de
negócio. Nada testável sem subir um servidor HTTP. O caso de uso resolve isso — é testável com
um repositório fake, sem nenhuma dependência de ASP.NET Core.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Paginação e contratos de listagem

### Conceito
Toda listagem que pode crescer sem limite (grants de um workspace, audit events) precisa de
paginação desde o início — não como otimização posterior.

### Como funciona
```csharp
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public async Task<PagedResponse<GrantResponse>> ListAsync(int page, int pageSize, CancellationToken ct)
{
    var query = dbContext.Grants.AsNoTracking().OrderByDescending(g => g.RequestedAt);

    var total = await query.CountAsync(ct);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    return new PagedResponse<GrantResponse>(items.Select(ToResponse).ToList(), page, pageSize, total);
}
```

### Armadilha comum
`impersonation_audit_events` ([módulo 12](12-seguranca-auditoria-mensageria.md)) cresce sem
limite por design — qualquer endpoint de consulta sobre essa tabela **precisa** de paginação
(e idealmente cursor baseado em `sequence`, não `OFFSET`, para não degradar com o tamanho da
tabela).

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Exercício-âncora do módulo

No projeto `Hub.Application` (criado no [módulo 00](00-modelo-mental-e-setup.md)):

1. Defina as interfaces de repositório necessárias (`IHubRepository`, `IGrantRepository`,
   `ISessionRepository`, `IInstallationRepository`) — sem nenhuma referência a EF Core.
2. Implemente os casos de uso do Hub como classes próprias, uma por fluxo:
   - `InstallAppUseCase`
   - `RequestGrantUseCase` / `ApproveGrantUseCase` / `DenyGrantUseCase` / `RevokeGrantUseCase`
   - `StartSessionUseCase` / `EndSessionUseCase`
3. Defina os DTOs de request/response de cada caso de uso (nunca a entidade de domínio
   diretamente).
4. Em `Hub.Infrastructure`, implemente as interfaces de repositório usando o `HubDbContext`
   do [módulo 07](07-ef-core-multitenant.md).
5. Reescreva os endpoints do `Hub.Api` (do exercício do [módulo 08](08-aspnet-core-fundamentos.md))
   para chamar os casos de uso, não o `DbContext` diretamente.
6. Escreva testes dos casos de uso usando um repositório **fake em memória** (implementando as
   mesmas interfaces) — sem precisar de Postgres nem de HTTP para rodar esses testes.

Critério de pronto: `Hub.Api` não tem nenhuma referência direta a `HubDbContext` nos seus
endpoints — só chama casos de uso de `Hub.Application`, que por sua vez só conhece interfaces.
Os testes de caso de uso rodam em milissegundos, sem infraestrutura.

Próximo módulo: **10 — Autorização por política**.
