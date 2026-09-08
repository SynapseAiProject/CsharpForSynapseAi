# 05 — Modelagem de domínio

Ponto de virada da apostila: a partir daqui, todo exercício é uma fatia real do **Hub de
Apps**, e os exercícios acumulam num único projeto. Este módulo cobre seu tópico 1
(modelagem de domínio) — a fundação de tudo que vem depois.

O tema central: **objetos que se protegem, não que só armazenam.** Uma classe com
`{ get; set; }` público em tudo é uma struct disfarçada — qualquer código pode colocá-la num
estado inválido. O objetivo aqui é fazer o **compilador e o construtor** impedirem estados
ilegais, não a documentação.

## Entidades: identidade + invariantes protegidas no construtor

### Conceito
Uma **entidade** tem identidade própria (um `Id`) que persiste ao longo do tempo, mesmo que
seus outros campos mudem. Duas entidades com o mesmo `Id` são "a mesma coisa", mesmo com
estado diferente.

### Como funciona
```csharp
public sealed class Hub
{
    public HubId Id { get; }
    public WorkspaceId WorkspaceId { get; }
    public int Version { get; private set; }   // lock otimista — ver 07

    private readonly List<Installation> _installations = new();
    public IReadOnlyCollection<Installation> Installations => _installations.AsReadOnly();

    private Hub(HubId id, WorkspaceId workspaceId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Version = 0;
    }

    public static Hub CreateFor(WorkspaceId workspaceId)
        => new(HubId.New(), workspaceId);

    public Installation Install(App app)
    {
        if (_installations.Any(i => i.AppId == app.Id && i.Status == InstallationStatus.Active))
            throw new DomainException($"App {app.Id} já está instalado e ativo neste hub.");

        var installation = Installation.CreateFor(Id, app.Id);
        _installations.Add(installation);
        return installation;
    }
}
```

Note: **nenhum setter público**. A única forma de criar um `Hub` é `Hub.CreateFor(...)` (uma
*factory*), e a única forma de instalar um app é `hub.Install(app)` — que já garante a
invariante "1 app = 1 instalação ativa" **dentro do método**, não como uma checagem externa
que alguém pode esquecer de chamar.

### Equivalente C/C++
Isto é exatamente a disciplina de encapsulamento que você já pratica em C++: construtor
privado + factory estática ≈ um construtor que valida e lança exception em vez de deixar
o objeto existir em estado inconsistente (o "objeto nasce sempre válido" do RAII, aplicado a
regras de negócio, não só a recursos).

### Armadilha comum
Expor `List<Installation>` diretamente (em vez de `IReadOnlyCollection` via uma cópia/wrapper)
permite que código externo faça `hub.Installations.Add(...)` e burle a invariante do
`Install()`. Sempre exponha coleções internas como somente-leitura.

### 🔬 Pergunta profunda (opcional)
> Por que `sealed`? Porque herança em entidades de domínio tende a vazar invariantes — uma
> subclasse pode alterar comportamento herdado de forma que quebra uma garantia que a classe
> base pensava estar protegendo. Prefira composição; reserve herança para hierarquias que o
> próprio domínio exige (não é o caso do Hub).

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Value Objects: identidade não importa, só o conteúdo

### Conceito
Um **Value Object** não tem identidade — dois VOs com os mesmos valores são intercambiáveis.
`record`/`record struct` (do [módulo 01](01-csharp-essencial.md)) são a ferramenta natural.

### Como funciona
```csharp
public readonly record struct HubId(Guid Value)
{
    public static HubId New() => new(Guid.NewGuid());
}

public readonly record struct WorkspaceId(Guid Value)
{
    public static WorkspaceId New() => new(Guid.NewGuid());
}
```

Isso parece verboso comparado a usar `Guid` cru em todo lugar — mas o ganho é o compilador
impedir, por exemplo, passar um `WorkspaceId` onde um `HubId` era esperado. Sem esse
"strongly-typed ID", `CreateGrant(Guid workspaceId, Guid installationId)` deixa passar os
dois `Guid` trocados de posição sem nenhum erro — o compilador não sabe a diferença. Com
strongly-typed IDs, `CreateGrant(WorkspaceId, InstallationId)` torna essa troca um erro de
compilação.

### Equivalente C/C++
Comparável ao padrão de criar `typedef`/`using` distintos para IDs em C++ (`using UserId =
StrongType<int, struct UserIdTag>`) para evitar misturar `int`s que representam coisas
diferentes — só que em C# é nativo e ergonômico via `record struct`, sem biblioteca extra.

### Armadilha comum
Modelar um Value Object como `class` (reference type) sem cuidado quebra a expectativa de
igualdade por valor — dois `GrantScope` "iguais" criados separadamente pareceriam diferentes
numa comparação `==` sem `record`. Sempre prefira `record`/`record struct` para VOs.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Aggregate Root e fronteiras do agregado

### Conceito
Um **aggregate** é um grupo de entidades/VOs tratado como uma unidade de consistência. O
**aggregate root** é a única porta de entrada — código externo nunca modifica uma entidade
"filha" diretamente, sempre passa pelo root.

### Como funciona
No Hub de Apps: **`Hub` é o aggregate root**, 1:1 com `Workspace`. `Installation` é uma
entidade filha do agregado `Hub` — só existe através dele (como no método `Install()` acima).

```text
Hub (root)
 └── Installation[]      -- só criada/alterada via métodos do Hub
```

`Grant` e `Session`, no design deste projeto, são **agregados próprios** (não filhos do
`Hub`) — eles referenciam uma `Installation` por `InstallationId`, mas têm seu próprio ciclo
de vida e consistência (aprofundado no [módulo 06](06-maquinas-de-estado.md)). Essa é uma
decisão de design: um agregado pequeno erra para o lado de menos contenção/lock em vez de um
agregado gigante "Hub com tudo dentro".

### Equivalente C/C++ (e no seu v2 TS)
Comparável à fronteira que você já desenha nos seus `modules/*/domain` do v2 — cada módulo
(`identity`, `work`, `collaboration`...) tem uma fronteira clara de responsabilidade. Aggregate
root é essa mesma ideia aplicada a **uma única transação/consistência**, não a um módulo
inteiro.

### Armadilha comum
Deixar qualquer camada superior (ex: um endpoint da API) buscar uma `Installation` direto do
banco e modificá-la sem passar pelo `Hub` — isso quebra a invariante "1 app = 1 instalação
ativa", porque ninguém checou a regra. A regra de ouro: **se está dentro do agregado, só o
aggregate root modifica.**

### 🔬 Pergunta profunda (opcional)
> Por que `Grant`/`Session` não são filhos do `Hub`? Porque agregados grandes viram gargalo de
> concorrência (lock otimista — [módulo 07](07-ef-core-multitenant.md) — falharia toda vez que
> duas sessions diferentes fossem criadas "ao mesmo tempo" se tudo dependesse de escrever o
> `Hub"). Agregados pequenos, referenciando uns aos outros por Id, é o padrão recomendado por
> Eric Evans e reforçado por Vaughn Vernon ("effective aggregate design").

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Domain exceptions

### Conceito
Uma exception específica do domínio, lançada quando uma invariante seria violada — não um
`Exception` genérico.

```csharp
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

### Equivalente C/C++
Parecido com definir um tipo de exception próprio em C++ (`class DomainError : public
std::runtime_error`) para diferenciar erros de regra de negócio de erros de infraestrutura
(rede, arquivo). Isso importa na [camada web](08-aspnet-core-fundamentos.md): um
`DomainException` vira `400 Bad Request`; uma falha de banco vira `500`.

### Exercício
Ver exercício-âncora no fim do módulo.

---

## Exercício-âncora do módulo

No projeto `Hub.Domain` (criado no [módulo 00](00-modelo-mental-e-setup.md)):

1. Modele `record struct WorkspaceId`, `HubId`, `AppId`, `InstallationId` (Value Objects
   de identidade).
2. Modele `Workspace` (entidade simples: `Id`, `Name`).
3. Modele `App` (entidade simples: `Id`, `Slug`, `Name`) — o catálogo, sem regra especial.
4. Modele `Hub` como aggregate root: `CreateFor(WorkspaceId)`, `Install(App)` garantindo a
   invariante de instalação única ativa, `Installations` como coleção somente-leitura.
5. Modele `Installation` como entidade filha, só instanciável via `Hub.Install(...)`
   (construtor `internal` ou `private` + factory acessível só pelo `Hub`).
6. Escreva testes xUnit no projeto `Hub.Tests` (já referenciando `Hub.Domain` desde o módulo
   00):
   - Criar um Hub para um Workspace funciona.
   - Instalar um App novo funciona.
   - Instalar o mesmo App duas vezes (enquanto a primeira instalação está ativa) lança
     `DomainException`.
   - Tentar construir um `Hub`/`Installation` fora dos métodos de fábrica não compila (isso é
     verificado só lendo o código — não tem como escrever um teste que "falha ao compilar",
     mas confirme manualmente que não existe outro construtor público acessível).

Critério de pronto: `dotnet test` verde, e **nenhuma classe em `Hub.Domain` tem um setter
público que permita violar uma invariante listada no [módulo 00](00-modelo-mental-e-setup.md#6-o-hub-de-apps--visão-geral)**.

Próximo módulo: **06 — Máquinas de estado**.
