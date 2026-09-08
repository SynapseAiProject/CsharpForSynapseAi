# 00 — Modelo mental e setup

## 1. A pilha do .NET

Em C, você pensa em:

```text
programa
 ├── main
 ├── memória
 ├── syscalls
 ├── processos
 └── bibliotecas
```

Em .NET, a pilha é outra:

```text
C# (linguagem)
 ↓
.NET runtime (CLR)
 ↓
BCL (Base Class Library)
 ↓
ASP.NET Core (framework web)
 ↓
Application (o Hub de Apps)
```

E quando uma request HTTP chega, ela atravessa uma segunda pilha, dentro da anterior:

```text
HTTP Request
 ↓
Kestrel (servidor HTTP embutido)
 ↓
Middleware pipeline
 ↓
Routing
 ↓
Endpoint (Controller / Minimal API)
 ↓
Application layer (casos de uso)
 ↓
Domain layer (Hub, Grant, Session...)
 ↓
Infrastructure (EF Core → PostgreSQL)
```

Essa arquitetura mental importa mais, no início, do que decorar sintaxe — é ela que orienta
onde cada linha de código que você escrever deve morar.

## 2. Mapa de migração — C/C++ → C#/.NET

Referência rápida. Volte aqui sempre que um conceito novo aparecer.

| Você conhece (C/C++) | C# / .NET | Nota |
|---|---|---|
| `malloc`/`free` | Garbage Collector | ninguém libera manualmente memória gerenciada — ver [03](03-runtime-dotnet.md) |
| ponteiro | referência | referências não fazem aritmética; não apontam para qualquer endereço |
| `struct` (POD) | `struct` | mas em C# `struct` é *value type* — semântica de cópia, não de ponteiro |
| classe C++ | `class` | *reference type* — vive no heap gerenciado |
| RAII / destrutor | `IDisposable` + `using` | GC não é determinístico; `Dispose()` é seu destrutor explícito — ver [02](02-csharp-avancado.md) |
| interface/abstract class | `interface` | sem herança múltipla de implementação, mas múltiplas interfaces sim |
| template | generic | genéricos são checados na declaração, não expandidos em compile-time como templates |
| function pointer | `delegate` | tipado, multicast (pode apontar para várias funções) |
| callback manual | delegate / `event` | eventos são pub/sub de primeira classe na linguagem |
| threads (`pthread`) | `Thread` / `Task` / `async`-`await` | .NET prioriza *Task* sobre thread crua — ver [03](03-runtime-dotnet.md) |
| processo (`fork`/`exec`) | `System.Diagnostics.Process` | raramente usado num backend web |
| exceptions (C++) | exceptions | modelo parecido; `try`/`catch`/`finally` |
| Makefile | `.csproj` + MSBuild | declarativo (XML), não script imperativo |
| biblioteca estática/dinâmica | assembly (`.dll`) | unidade de deploy e versionamento do .NET |
| linker | build do `dotnet` | resolve referências entre assemblies e pacotes NuGet |
| gerenciador de pacotes (vcpkg/apt) | NuGet | integrado ao `.csproj` |
| `main()` | `Program.cs` (top-level statements) | ponto de entrada do processo |
| libc / STL | BCL (Base Class Library) | `System.*`, `System.Collections.Generic.*` etc. |
| syscalls diretas | APIs do runtime/OS | o runtime abstrai a maior parte |
| socket TCP cru | `HttpClient` / Kestrel / `System.Net.Sockets` | seu Webserver em C++ vira, em ASP.NET Core, o pipeline de middleware |

> 🔬 **Aprofundamento opcional** — Por que `struct` em C# não é "a mesma coisa" que em C++?
> Em C++, `struct` e `class` só diferem no modificador de acesso padrão. Em C#, a diferença é
> de **modelo de armazenamento**: instâncias de `struct` são copiadas por valor sempre que
> passadas ou atribuídas (como um `struct` em C, sem ponteiro). Isso é explorado a fundo em
> [03](03-runtime-dotnet.md).

## 3. Instalando e verificando o SDK

```bash
# Verifique se já existe um SDK instalado
dotnet --version
dotnet --info
```

Se não houver SDK, instale o **.NET SDK LTS mais recente** (8 ou superior — os conceitos
desta apostila valem a partir do .NET 8). O SDK inclui o runtime, o compilador (Roslyn) e o
CLI `dotnet`.

## 4. `dotnet` CLI essencial

| Comando | Equivalente mental |
|---|---|
| `dotnet new console -o MeuApp` | gerar um projeto novo a partir de um template |
| `dotnet build` | compilar (como `make`) |
| `dotnet run` | compilar + executar |
| `dotnet test` | rodar testes (xUnit/NUnit) |
| `dotnet restore` | baixar dependências do `.csproj` (como `npm install`, mas via NuGet) |
| `dotnet add package <Nome>` | adicionar dependência (NuGet) |
| `dotnet add reference <caminho>` | referenciar outro projeto da solution |
| `dotnet sln add <caminho>` | adicionar um projeto à solution |

## 5. Anatomia de um projeto .NET

**Makefile → `.csproj`.** Onde você escrevia regras de build manualmente, o `.csproj` é
declarativo:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="8.*" />
  </ItemGroup>
</Project>
```

Não existe lista explícita de arquivos `.cs` — o SDK inclui automaticamente tudo que estiver
na pasta (glob implícito). Isso substitui a parte do Makefile que lista `.c`/`.o`.

**Multi-diretório C → `.sln` (solution).** Assim como você organizava um projeto C em vários
`.c`/`.h` com um Makefile raiz, uma **solution** agrupa vários **projects** (`.csproj`), cada
um compilando para um assembly (`.dll`/`.exe`) próprio:

```text
Hub.sln
 ├── src/Hub.Domain/Hub.Domain.csproj                  (sem dependências — regras de negócio puras)
 ├── src/Hub.Application/Hub.Application.csproj        (casos de uso; depende de Domain)
 ├── src/Hub.Infrastructure/Hub.Infrastructure.csproj   (EF Core, Postgres; depende de Application)
 ├── src/Hub.Api/Hub.Api.csproj                        (ASP.NET Core; depende de Infrastructure)
 └── tests/Hub.Tests/Hub.Tests.csproj                  (xUnit; depende de tudo)
```

Essa estrutura é a mesma ideia dos seus `domain`/`application`/`infrastructure` no v2 em
TypeScript — só que aqui a fronteira entre camadas é **imposta pelo compilador**: `Hub.Domain`
literalmente não pode referenciar `Hub.Infrastructure`, porque a referência de projeto não
existe. Você constrói essa solution completa ao longo dos módulos [05](05-modelagem-de-dominio.md)
a [13](13-testes-arquitetura-deploy.md).

## 6. O Hub de Apps — visão geral

O sistema que esta apostila usa como projeto âncora: um **hub multi-tenant de aplicações**.
Cada **workspace** (tenant) tem um **Hub** — o aggregate root do domínio. O Hub instala
**Apps** do catálogo, cada instalação concede **grants** de acesso, e cada grant pode abrir
**sessions** de uso. Tudo isso precisa: expirar sozinho, resistir a escrita concorrente,
registrar quem acessou o quê de forma auditável, e decidir permissões somando várias roles.

### Schema de referência (design que vamos construir)

Este é o desenho que orienta os exercícios-âncora a partir do módulo 05. Ele **não é um
schema herdado de outro sistema** — é o design deste projeto, refinado enquanto
implementamos.

```text
workspaces
  id, name, created_at

hubs                                   -- aggregate root, 1:1 com workspace
  id, workspace_id (FK, unique), version (int, lock otimista), created_at, updated_at

apps                                   -- catálogo de apps instaláveis
  id, slug, name, description

installations                          -- invariante: 1 app = 1 instalação ativa por hub
  id, hub_id (FK), app_id (FK), status, installed_at

memberships                            -- pessoas dentro de um workspace
  id, workspace_id (FK), user_id (FK), created_at

roles                                  -- papéis definidos por workspace
  id, workspace_id (FK), name

permissions                            -- catálogo global de permissões (ex: "grants:approve")
  id, key, description

role_permissions                       -- N:N — o que cada role permite
  role_id (FK), permission_id (FK)

membership_roles                       -- N:N — um membro acumula várias roles
  membership_id (FK), role_id (FK)      -- permissão efetiva = união de tudo isso

grants                                 -- concessão de acesso a uma installation
  id, installation_id (FK), requested_by (FK membership), status (grant_status),
  requested_at, approved_at, expires_at, revoked_at

sessions                               -- uso concreto de um grant; escopo ⊆ grant
  id, grant_id (FK), status (session_status), started_at, ended_at, expires_at

impersonation_audit_events             -- append-only, nunca update/delete
  id, sequence (bigint, monotônico), actor_membership_id, target_user_id,
  event_type, occurred_at, metadata

impersonation_notifications            -- fan-out do evento de auditoria
  id, audit_event_id (FK), channel, status, sent_at

tokens
  id, session_id (FK), token_hash (nunca o token cru), created_at, expires_at
```

**Máquinas de estado** (aprofundadas no [módulo 06](06-maquinas-de-estado.md)):

```text
grant_status:   requested → approved → active → expired
                                     ↘ revoked
                          ↘ denied

session_status: active → ended
                       ↘ expired
                       ↘ revoked
```

**Invariantes que o código precisa impedir, não só documentar** (módulo [05](05-modelagem-de-dominio.md)):
- Um `Hub` pertence a exatamente um `Workspace`, e vice-versa.
- No máximo uma `Installation` ativa por (`hub_id`, `app_id`).
- Uma `Session` nunca tem escopo maior que o `Grant` que a originou.
- Duas escritas concorrentes no mesmo `Hub` não podem se sobrepor silenciosamente — daí `version`.
- `impersonation_audit_events` é *append-only*: sem `UPDATE`, sem `DELETE`.

## 7. Exercício — setup

1. Instale o .NET SDK e confirme com `dotnet --version` (8.0 ou superior).
2. Crie a estrutura de solution do Hub (vazia, só o esqueleto de projetos):
   ```bash
   dotnet new sln -n Hub
   dotnet new classlib -o src/Hub.Domain
   dotnet new classlib -o src/Hub.Application
   dotnet new classlib -o src/Hub.Infrastructure
   dotnet new webapi -o src/Hub.Api --use-minimal-apis
   dotnet new xunit -o tests/Hub.Tests

   dotnet sln add src/Hub.Domain src/Hub.Application src/Hub.Infrastructure src/Hub.Api tests/Hub.Tests

   dotnet add src/Hub.Application reference src/Hub.Domain
   dotnet add src/Hub.Infrastructure reference src/Hub.Application
   dotnet add src/Hub.Api reference src/Hub.Infrastructure
   dotnet add tests/Hub.Tests reference src/Hub.Domain src/Hub.Application
   ```
3. Confirme que tudo compila: `dotnet build`.
4. Tente adicionar uma referência **na direção errada** (`Hub.Domain` → `Hub.Infrastructure`)
   e observe: nada te impede *no `dotnet add reference`*, mas o objetivo é nunca fazer isso —
   guarde essa disciplina para o módulo 05, onde ela vira regra de arquitetura.

Próximo módulo: [01 — C# essencial](01-csharp-essencial.md).
