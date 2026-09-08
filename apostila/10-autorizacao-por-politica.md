# 10 — Autorização por política

Seu tópico 4. O ponto central: **a permissão efetiva de um membro é a união de todas as suas
roles** — não um único `Role` gravado no token. `roles`, `permissions`, `role_permissions` e
`membership_roles` ([módulo 00](00-modelo-mental-e-setup.md)) formam esse sistema.

## Authentication vs Authorization

### Conceito
**Authentication (authn)**: quem é você? **Authorization (authz)**: o que você pode fazer?
São camadas distintas do pipeline ([módulo 08](08-aspnet-core-fundamentos.md)).

### Como funciona
```text
UseAuthentication   -- lê o JWT do header, popula HttpContext.User com as claims
UseAuthorization     -- checa se HttpContext.User satisfaz a policy exigida pelo endpoint
```

### Equivalente C/C++
Sem equivalente direto no seu Webserver em C++ (a menos que você tenha implementado algo
manualmente) — mas o conceito é o mesmo de qualquer sistema de auth: primeiro identificar,
depois decidir.

### Exercício
Sem exercício isolado — usado abaixo.

---

## JWT e claims

### Conceito
Um JWT carrega **claims** — pares chave/valor assinados (não criptografados) que identificam o
usuário autenticado e seu contexto.

### Como funciona
```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
        };
    });
```

Claims típicas no Hub: `sub` (user id), `workspace_id` (para o `ICurrentWorkspace` do
[módulo 07](07-ef-core-multitenant.md)), `membership_id`.

### Equivalente C/C++
Comparável a um token assinado (HMAC) que você validaria manualmente checando a assinatura
antes de confiar no payload — só que com biblioteca e middleware prontos, incluindo checagem
de expiração.

### Armadilha comum
**Nunca** coloque a lista de permissões inteira dentro do JWT esperando que ela fique
"sempre atualizada" — revogar uma role no meio da validade do token não afeta um JWT já
emitido (ele é auto-contido). Para o Hub, o JWT carrega só identidade (`sub`,
`membership_id`), e a policy handler (abaixo) consulta o banco a cada request para saber as
roles/permissões **atuais**. Isso é mais caro que confiar cegamente no token, mas é
correto — revogação de acesso precisa ter efeito imediato num sistema de grants/impersonation.

### Exercício
Sem exercício isolado — usado abaixo.

---

## Policy-based authorization

### Conceito
Em vez de checar papéis (`[Authorize(Roles = "Admin")]`) — rígido demais para "permissão =
união de roles" — ASP.NET Core permite declarar **policies** com lógica arbitrária via
`IAuthorizationHandler`.

### Como funciona
```csharp
// Requirement: só descreve O QUE precisa ser verdade
public class HasPermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}

// Handler: COMO verificar — aqui mora a união de roles
public class HasPermissionHandler(IPermissionsRepository permissionsRepo)
    : AuthorizationHandler<HasPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, HasPermissionRequirement requirement)
    {
        var membershipId = context.User.FindFirst("membership_id")?.Value;
        if (membershipId is null) return;

        // união de todas as permissions de todas as roles do membro:
        var effectivePermissions = await permissionsRepo.GetEffectivePermissionsAsync(
            new MembershipId(Guid.Parse(membershipId)));

        if (effectivePermissions.Contains(requirement.PermissionKey))
            context.Succeed(requirement);
    }
}
```

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanApproveGrant", policy =>
        policy.Requirements.Add(new HasPermissionRequirement("grants:approve")));
});

builder.Services.AddScoped<IAuthorizationHandler, HasPermissionHandler>();

app.MapPost("/grants/{id}/approve", ApproveGrantHandler)
   .RequireAuthorization("CanApproveGrant");
```

A query de "permissões efetivas" reflete exatamente o schema do
[módulo 00](00-modelo-mental-e-setup.md):

```csharp
public async Task<HashSet<string>> GetEffectivePermissionsAsync(MembershipId membershipId, CancellationToken ct)
{
    var keys = await dbContext.MembershipRoles
        .Where(mr => mr.MembershipId == membershipId)
        .SelectMany(mr => mr.Role.RolePermissions)
        .Select(rp => rp.Permission.Key)
        .Distinct()
        .ToListAsync(ct);

    return keys.ToHashSet();
}
```

`SelectMany` "achata" a relação `membership → roles → permissions` numa única lista — é
literalmente a união de permissões de todas as roles do membro, expressa como uma LINQ query
que o EF Core traduz para um `JOIN` no Postgres.

### Equivalente C/C++ (e no seu v2 TS)
`IAuthorizationHandler` é o Strategy Pattern do seu módulo Object/SOLID aplicado a "como
decidir se uma requisição é permitida" — cada policy é uma estratégia plugável, resolvida via
DI. Comparável a um middleware de authz que você escreveria manualmente checando permissões
num sistema RBAC.

### Armadilha comum
Implementar autorização como `if (user.Role == "Admin")` espalhado pelos endpoints reproduz
exatamente o problema do enum-sem-máquina-de-estado do
[módulo 06](06-maquinas-de-estado.md): a regra "o que conta como permitido" fica duplicada e
sujeita a divergir. Centralize em policies + handlers.

> 🔬 **Aprofundamento opcional** — Para permissões que dependem do **recurso específico**
> sendo acessado (ex: "só pode aprovar grants do próprio workspace"), use
> `IAuthorizationService.AuthorizeAsync(user, resource, policy)` — autorização baseada em
> recurso, não só em policy global. Vale explorar quando o Hub precisar disso na prática.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Exercício-âncora do módulo

1. Modele `Role`, `Permission`, `RolePermission`, `MembershipRole` em `Hub.Domain` (entidades
   simples — a regra de negócio real está na composição, não em invariantes complexas aqui).
2. Mapeie-as no EF Core ([módulo 07](07-ef-core-multitenant.md)).
3. Implemente `IPermissionsRepository.GetEffectivePermissionsAsync` como no exemplo acima.
4. Configure JWT bearer authentication (pode gerar tokens de teste manualmente para
   desenvolvimento — um endpoint `/dev/token` que emite um JWT fake com `membership_id`
   arbitrário, claramente marcado para não existir em produção).
5. Crie a policy `"CanApproveGrant"` (permissão `grants:approve`) e proteja o endpoint
   `POST /grants/{id}/approve` do [módulo 09](09-fronteiras-de-api.md) com ela.
6. Teste manualmente: um membro com uma role que tem `grants:approve` consegue aprovar; um
   membro sem essa permissão em nenhuma de suas roles recebe `403 Forbidden`; um membro com
   **duas** roles, nenhuma isolada tendo `grants:approve`, mas a união das duas tendo,
   consegue aprovar — prova que a união está funcionando, não só uma role isolada.

Critério de pronto: o cenário do item 6 (permissão só existe na união de duas roles) funciona
de ponta a ponta via HTTP.

Próximo módulo: **11 — Concorrência e workers**.
