# 07 — EF Core multi-tenant

Seu tópico 3. Aqui o domínio puro dos módulos [05](05-modelagem-de-dominio.md)/[06](06-maquinas-de-estado.md)
ganha persistência real em PostgreSQL — e o isolamento de tenant vira **estrutural**, não uma
disciplina manual de lembrar `WHERE workspace_id = ...` em toda query.

## DbContext e mapeamento de agregados

### Conceito
`DbContext` é a unidade de trabalho (Unit of Work) do EF Core — rastreia mudanças em
entidades carregadas e as traduz para SQL num `SaveChangesAsync()`.

### Como funciona
```csharp
public class HubDbContext(DbContextOptions<HubDbContext> options) : DbContext(options)
{
    public DbSet<Hub> Hubs => Set<Hub>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Grant> Grants => Set<Grant>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HubDbContext).Assembly);
    }
}
```

Cada entidade tem sua própria classe de configuração (Fluent API), mantendo `Hub.Domain` **sem
nenhuma referência ao EF Core** — o mapeamento mora em `Hub.Infrastructure`:

```csharp
public class HubConfiguration : IEntityTypeConfiguration<Hub>
{
    public void Configure(EntityTypeBuilder<Hub> builder)
    {
        builder.ToTable("hubs");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, value => new HubId(value));

        builder.Property(h => h.WorkspaceId)
            .HasConversion(id => id.Value, value => new WorkspaceId(value));
        builder.HasIndex(h => h.WorkspaceId).IsUnique();   // 1:1 com workspace

        builder.Property(h => h.Version).IsConcurrencyToken();   // ver "optimistic concurrency" abaixo

        builder.HasMany(h => h.Installations)
            .WithOne()
            .HasForeignKey(i => i.HubId);

        // expõe o backing field _installations pro EF Core preencher, sem precisar de setter público
        builder.Navigation(h => h.Installations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

### Equivalente C/C++ (e no seu v2 TS)
`DbContext` ≈ a camada `infrastructure`/repository do seu v2, mas com *change tracking*
automático — você modifica objetos em memória (`hub.Install(app)`) e o EF Core descobre
sozinho o diff a persistir, em vez de você escrever o `UPDATE`/`INSERT` manualmente como faria
com um client SQL cru.

### Armadilha comum
Mapear a entidade de domínio direto (como acima) exige truques (`HasConversion` para Value
Objects, `UsePropertyAccessMode(Field)` para coleções sem setter público) — é mais trabalho
que deixar o EF Core inferir tudo de uma classe anêmica com setters públicos, mas é isso que
preserva as invariantes do [módulo 05](05-modelagem-de-dominio.md). Não simplifique o domínio
para "facilitar" o mapeamento.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Isolamento de tenant: Global Query Filters

### Conceito
Um filtro aplicado **automaticamente** a toda query contra uma tabela, sem precisar repetir a
condição em cada `Where(...)` manualmente.

### Como funciona
```csharp
public class HubDbContext(DbContextOptions<HubDbContext> options, ICurrentWorkspace currentWorkspace)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hub>()
            .HasQueryFilter(h => h.WorkspaceId == currentWorkspace.Id);

        // ...
    }
}
```

Toda vez que alguém fizer `context.Hubs.ToListAsync()`, o SQL gerado já inclui
`WHERE workspace_id = @currentWorkspaceId` — **sem exceção**, mesmo que quem escreveu aquele
LINQ específico tenha esquecido de pensar em tenant isolation naquele momento.

`ICurrentWorkspace` é resolvido a partir do contexto da requisição autenticada (claim do JWT —
ver [módulo 10](10-autorizacao-por-politica.md)), registrado como `Scoped` no DI
([módulo 04](04-di-config-logging.md)).

### Equivalente C/C++ (e no seu v2 TS)
No seu v2, isolamento de tenant provavelmente depende de lembrar `.eq('workspace_id', ...)`
em cada query do Supabase client (ou de Row Level Security no Postgres). Query filters do EF
Core dão a mesma garantia **no nível do ORM**, de um jeito que é impossível esquecer — o
filtro está no modelo, não em cada call site.

### Armadilha comum
Query filters só se aplicam em queries **via LINQ contra o `DbSet`**. SQL bruto
(`FromSqlRaw`) ou `Include` mal configurado podem escapar do filtro — trate isso como
defesa em profundidade, não como a *única* camada de proteção (Row Level Security no Postgres
é um reforço saudável, fora do escopo desta apostila).

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Optimistic Concurrency: o `version` do Hub

### Conceito
Proteção contra duas escritas concorrentes no mesmo agregado se sobrescreverem silenciosamente
— sem usar lock pessimista (que travaria linhas do banco).

### Como funciona
```csharp
builder.Property(h => h.Version).IsConcurrencyToken();
```

Isso faz o EF Core incluir `version` na cláusula `WHERE` de todo `UPDATE`:

```sql
UPDATE hubs SET ..., version = version + 1
WHERE id = @id AND version = @versionLidaAntes;
```

Se outra transação já alterou o `Hub` (e portanto seu `version`) entre a leitura e a escrita,
**zero linhas são afetadas** — o EF Core detecta isso e lança
`DbUpdateConcurrencyException`.

```csharp
try
{
    await dbContext.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    // alguém alterou o Hub entre sua leitura e sua escrita — recarregue e tente de novo,
    // ou propague como conflito pro chamador. Aprofundado no módulo 11.
}
```

### Equivalente C/C++
Comparável a um **compare-and-swap** (CAS) que você usaria em C para uma escrita concorrente
lock-free: leia o valor, tente escrever só se ninguém mudou desde a leitura, senão recomece.
Aqui, `version` faz o papel do valor comparado, e o banco de dados faz o CAS atomicamente via
`WHERE version = @lido`.

### Armadilha comum
Confundir com lock **pessimista** (`SELECT ... FOR UPDATE`) — optimistic concurrency não
bloqueia ninguém enquanto uma transação está em andamento; ela só **detecta o conflito na
hora de escrever**. É a escolha certa aqui porque conflitos reais no `Hub` devem ser raros
(duas pessoas instalando apps ao mesmo tempo no mesmo hub) — pagar o custo de lock pessimista
o tempo todo não compensa. Aprofundado (com retry) no [módulo 11](11-concorrencia-e-workers.md).

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Migrations, Npgsql e secrets

### Conceito
Migrations são o `git` do seu schema — cada mudança de modelo vira um arquivo versionado que
sabe aplicar (`Up`) e reverter (`Down`) a alteração no banco.

```bash
dotnet add src/Hub.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Hub.Infrastructure package Microsoft.EntityFrameworkCore.Design

dotnet ef migrations add InitialCreate --project src/Hub.Infrastructure --startup-project src/Hub.Api
dotnet ef database update --project src/Hub.Infrastructure --startup-project src/Hub.Api
```

Connection string via `IOptions<DatabaseOptions>` ([módulo 04](04-di-config-logging.md)),
nunca hardcoded — em desenvolvimento, `dotnet user-secrets set "Database:ConnectionString" "..."`.

### Equivalente C/C++ (e no seu v2 TS)
Equivalente às migrations SQL versionadas que você já tem em `supabase/migrations/` no v2 —
mesma ideia, gerada a partir do modelo C# em vez de escrita manual em SQL.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## LINQ → SQL, tracking, e async

### Conceito
Uma `IQueryable<T>` do EF Core não roda nada até ser enumerada — nesse momento, o LINQ é
traduzido para SQL (retomando "deferred execution" do [módulo 02](02-csharp-avancado.md)).

### Como funciona
```csharp
// Isto NÃO toca o banco ainda — é só a construção da query:
var query = dbContext.Grants
    .Where(g => g.Status == GrantStatus.Active)
    .OrderByDescending(g => g.RequestedAt);

// Isto sim gera e executa o SQL:
var results = await query.ToListAsync();

// Para leitura pura (sem intenção de alterar), evite carregar tracking desnecessário:
var readOnly = await dbContext.Grants
    .AsNoTracking()
    .Where(g => g.Status == GrantStatus.Active)
    .ToListAsync();
```

### Armadilha comum
Toda operação de banco deve ser `async` (`ToListAsync`, `SaveChangesAsync`,
`FirstOrDefaultAsync`) — retomando o [módulo 03](03-runtime-dotnet.md), chamadas síncronas de
I/O bloqueiam uma thread do pool à toa. Use `AsNoTracking()` em queries de leitura que não
vão gerar um `SaveChangesAsync()` depois — evita o custo do change tracker para nada.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Exercício-âncora do módulo

No projeto `Hub.Infrastructure`:

1. Adicione `Npgsql.EntityFrameworkCore.PostgreSQL` e configure `HubDbContext` com
   `DbSet<Workspace>`, `DbSet<Hub>`, `DbSet<App>`, `DbSet<Grant>`, `DbSet<Session>`.
2. Escreva `IEntityTypeConfiguration<T>` para cada entidade, incluindo os `HasConversion` para
   os Value Objects de Id do [módulo 05](05-modelagem-de-dominio.md).
3. Configure `IsConcurrencyToken()` em `Hub.Version`.
4. Configure um `HasQueryFilter` em `Hub` (e nas entidades que dependem dele) filtrando por
   `WorkspaceId` a partir de um `ICurrentWorkspace` (pode começar com uma implementação fake
   fixa, já que autenticação real só chega no [módulo 10](10-autorizacao-por-politica.md)).
5. Suba um Postgres local via Docker (`docker run -e POSTGRES_PASSWORD=... -p 5432:5432
   postgres:16`), gere e aplique a migration inicial.
6. Escreva um teste de integração (pode usar o `Hub.Tests` mesmo, ou criar
   `Hub.IntegrationTests`) que:
   - Cria um `Workspace` e um `Hub`, salva, recarrega, confirma os dados batem.
   - Abre **dois `DbContext` separados**, ambos carregam o mesmo `Hub`, um deles chama
     `hub.Install(app)` e salva; o outro tenta salvar uma alteração diferente no mesmo `Hub`
     (mesmo `Version` lido) — confirme que o segundo `SaveChangesAsync()` lança
     `DbUpdateConcurrencyException`.

Critério de pronto: migrations aplicadas num Postgres real, CRUD do Hub funcionando fim a fim,
e o teste de concorrência otimista falhando exatamente como esperado (prova de que
`hubs.version` está fazendo seu trabalho).

Próximo módulo: **08 — ASP.NET Core fundamentos**.
