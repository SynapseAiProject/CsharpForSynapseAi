# Hub: aprendendo a pensar em arquitetura com a CLI do dotnet

Este material não é referência. É uma sequência de passos pequenos — cada um com um comando real, um resultado real e uma pergunta arquitetural por trás dele. Quase todos os comandos abaixo foram executados de verdade antes de este texto ser escrito, e os resultados que você vê são reais (só cortados quando o terminal repetia informação óbvia). A única exceção é sinalizada explicitamente no texto, no ponto em que acontece — o ambiente usado para escrever isto não tem acesso à internet para baixar pacotes do NuGet, o que impediu de rodar só uma parte específica.

O ciclo é sempre o mesmo:

```text
Conceito → Comando → Resultado → Explicação → Próximo conceito
```

E a pergunta que importa mais do que qualquer comando, repetida ao longo do texto:

> **Antes de criar uma referência entre dois projetos, essa dependência deveria existir?**

---

## Nível 0 — só a Solution

**Quero** um contêiner vazio para organizar o que vou construir.

```bash
dotnet new sln -n Hub
```

```text
The template "Solution File" was created successfully.
```

Isso criou um arquivo `Hub.sln` (em SDKs mais novos, `Hub.slnx` — mesmo papel, formato mais enxuto; não muda nada do que vem a seguir). Repare: **não existe nenhum código ainda**. A Solution não compila nada sozinha — ela só vai, daqui a pouco, passar a listar projetos.

Não vou te pedir para decorar as opções de `dotnet new sln`. Se quiser ver todas, pergunte à própria ferramenta:

```bash
dotnet new sln --help
```

---

## Nível 1 — um projeto dentro dela

**Quero** algo que eu já consiga rodar.

```bash
dotnet new console -o Hub.App
dotnet sln add Hub.App
dotnet run --project Hub.App
```

```text
Project `Hub.App/Hub.App.csproj` added to the solution.
...
Hello, World!
```

Três comandos, três efeitos diferentes: `new` criou o projeto; `sln add` disse "este projeto pertence a este contêiner"; `run` compilou e executou. Note que `run` funcionaria **mesmo sem o `sln add`** — ele não depende da Solution, só precisa saber qual `.csproj` rodar. Guarde essa observação; ela volta lá na frente.

```bash
dotnet sln list
```
```text
Project(s)
----------
Hub.App/Hub.App.csproj
```

---

## Nível 2 — Domain e Application: a primeira decisão de dependência

`Hub.App` vai crescer e, junto, a tentação de colocar regra de negócio dentro do próprio `Program.cs`. Antes que isso aconteça, separamos "o que o sistema sabe fazer" de "como isso é exposto".

```bash
dotnet new classlib -o Hub.Domain
dotnet new classlib -o Hub.Application
dotnet sln add Hub.Domain Hub.Application
```

Agora vem a pergunta, não o comando:

```text
Hub.Application
        ↓
   Hub.Domain
```

**Quem depende de quem?** `Application` conhece `Domain` — ela orquestra regras que o Domain define. O contrário não faz sentido: o Domain é a parte mais estável do sistema, e não deveria saber que existe uma camada de orquestração em volta dele.

```bash
dotnet add Hub.Application reference Hub.Domain
dotnet add Hub.App reference Hub.Application
```

```text
Reference `..\Hub.Domain\Hub.Domain.csproj` added to the project.
Reference `..\Hub.Application\Hub.Application.csproj` added to the project.
```

Repare que **não** criamos `Hub.App → Hub.Domain`. `Hub.App` só fala com `Hub.Application`; se ele também enxergasse o `Domain` diretamente, teria dois jeitos de fazer a mesma coisa, e qualquer um dos dois poderia mudar sem o outro saber.

Com um pouco de código real (`Order.Total()` no Domain, `PlaceOrderUseCase` no Application chamando o Domain, `Program.cs` chamando o UseCase):

```bash
dotnet build
```
```text
Hub.Domain -> .../Hub.Domain.dll
Hub.Application -> .../Hub.Application.dll
Hub.App -> .../Hub.App.dll

Build succeeded.
```

Note a ordem: **o MSBuild compilou na ordem certa sozinho**, só olhando as referências que criamos. Ninguém disse "compile Domain primeiro" — isso está implícito no grafo.

```bash
dotnet run --project Hub.App
```
```text
Total do pedido: 60
```

### Pare e pense: o que acontece se a seta for invertida?

```bash
dotnet add Hub.Domain reference Hub.Application
```
```text
Reference `..\Hub.Application\Hub.Application.csproj` added to the project.
```

O comando **aceitou** — o `dotnet add reference` não julga se a dependência faz sentido, só cria a referência. Agora as duas setas apontam uma para a outra. Vamos ver o que a ferramenta de build acha disso:

```bash
dotnet build
```
```text
error MSB4006: There is a circular dependency in the target dependency graph
involving target "_GenerateRestoreProjectPathWalk".

Build FAILED.
```

Isso é o ponto central deste material: **a CLI deixou você criar a referência errada; foi o `build` que recusou.** A ferramenta não substitui o raciocínio — ela só é implacável quando o raciocínio falhou. Desfazendo:

```bash
dotnet remove Hub.Domain reference Hub.Application/Hub.Application.csproj
dotnet build
```
```text
Build succeeded.
```

(Repare que `remove` pede o caminho completo do `.csproj`, enquanto `add` aceitava a pasta. Pequena inconsistência da ferramenta — mais um motivo para não decorar sintaxe e sim ler o `--help` quando algo não se comportar como esperado.)

---

## Nível 3 — dois Bounded Contexts

O sistema cresce: agora existe "Pedidos" e "Expedição", e são áreas de negócio genuinamente diferentes — vocabulário diferente, regras diferentes, times diferentes talvez. `Hub.Domain` sozinho já não representa isso direito.

```bash
dotnet new classlib -o src/BC1/BC1.Domain
dotnet new classlib -o src/BC1/BC1.Application
dotnet new classlib -o src/BC2/BC2.Domain
dotnet new classlib -o src/BC2/BC2.Application

dotnet sln add src/**/*.csproj
```

```text
Project `src/BC1/BC1.Domain/BC1.Domain.csproj` added to the solution.
Project `src/BC1/BC1.Application/BC1.Application.csproj` added to the solution.
Project `src/BC2/BC2.Domain/BC2.Domain.csproj` added to the solution.
Project `src/BC2/BC2.Application/BC2.Application.csproj` added to the solution.
```

(`**` é expansão do seu shell, não do `dotnet` — no bash é preciso `shopt -s globstar` antes; no zsh e no PowerShell já funciona por padrão. Sem isso, passe os caminhos um a um.)

Dentro de cada BC, a mesma regra do Nível 2 se repete — é a mesma pergunta, aplicada de novo:

```bash
dotnet add src/BC1/BC1.Application reference src/BC1/BC1.Domain
dotnet add src/BC2/BC2.Application reference src/BC2/BC2.Domain
```

### Pare e pense: `BC1.Application` deveria referenciar `BC2.Domain`?

Tecnicamente, `dotnet add reference` deixaria você fazer isso agora mesmo. A pergunta não é "consigo?", é:

- Essa relação entre `BC1` e `BC2` é **direta ou deveria ser mediada**?
- Se `BC1` referenciar `BC2.Domain` diretamente, toda mudança no modelo interno de `BC2` pode quebrar `BC1` sem aviso — isso é acoplamento de **modelo**, não de **contrato**.
- Estou compartilhando um modelo ou apenas um contrato?

Por enquanto, **não criamos essa referência**. Guardamos a pergunta — ela volta no fechamento deste material, quando "Pedidos" precisar mesmo assim saber que uma "Expedição" existe.

---

## Nível 4 — o que é realmente compartilhado

Antes de resolver a comunicação entre BCs, existe uma pergunta menor e mais honesta: **existe algo que os dois genuinamente têm em comum**, não por conveniência, mas porque é o mesmo conceito? Aqui, `Money` — tanto o valor de um pedido quanto o custo de um frete são a mesma ideia.

```bash
dotnet new classlib -o src/SharedKernel
dotnet add src/BC1/BC1.Domain reference src/SharedKernel
dotnet add src/BC2/BC2.Domain reference src/SharedKernel
```

```text
Reference `..\..\SharedKernel\SharedKernel.csproj` added to the project.
Reference `..\..\SharedKernel\SharedKernel.csproj` added to the project.
```

Um Shared Kernel é uma decisão cara, não barata: os dois BCs agora dependem fisicamente do mesmo artefato, e mudar `Money` é uma mudança que afeta os dois ao mesmo tempo. Por isso ele deve conter o mínimo possível — não é "biblioteca de utilitários comuns", é "o punhado de conceitos que realmente não fazem sentido duplicados".

---

## Nível 5 — invertendo a dependência

`BC1.Application` sabe *que* precisa salvar um pedido, mas não deveria saber *como* — se é banco relacional, arquivo, memória. Essa é a fronteira entre regra de negócio e detalhe técnico.

```text
Quem define o contrato?      BC1.Domain      (interface IOrderRepository)
Quem implementa o contrato?  BC1.Infrastructure
```

```bash
dotnet new classlib -o src/BC1/BC1.Infrastructure
dotnet add src/BC1/BC1.Infrastructure reference src/BC1/BC1.Domain
```

Repare a direção: `Infrastructure → Domain`, nunca o contrário. O Domain nem sabe que `BC1.Infrastructure` existe. Isso é **inversão de dependência**: o código de baixo nível (acesso a dados) depende da abstração definida pelo código de alto nível (regra de negócio) — não o inverso, que seria o natural se você só fosse "chamando as coisas na ordem que usa".

```bash
dotnet build
```
```text
Build succeeded.
```

---

## Nível 6 — o composition root

Alguém, em algum lugar, precisa conhecer todo mundo e decidir qual implementação concreta usar. Esse é o único projeto do sistema com permissão para isso.

```bash
dotnet new web -o Hub.Api --no-https
dotnet add Hub.Api reference src/BC1/BC1.Application src/BC2/BC2.Application src/BC1/BC1.Infrastructure
dotnet sln add Hub.Api
```

```text
Reference `..\src\BC1\BC1.Application\BC1.Application.csproj` added to the project.
Reference `..\src\BC2\BC2.Application\BC2.Application.csproj` added to the project.
Reference `..\src\BC1\BC1.Infrastructure\BC1.Infrastructure.csproj` added to the project.
```

Dentro de `Hub.Api/Program.cs`, é o único lugar do sistema onde isto pode aparecer:

```csharp
IOrderRepository orderRepository = new InMemoryOrderRepository();
var placeOrder = new PlaceOrderUseCase(orderRepository);
```

`BC1.Application` só conhece a interface `IOrderRepository`. Só o `Hub.Api` sabe que, hoje, ela é `InMemoryOrderRepository`. Trocar para um banco de verdade amanhã significa mudar **uma linha, em um projeto só** — nenhuma outra parte do sistema percebe.

```bash
dotnet build && dotnet run --project Hub.Api
```
```text
Build succeeded.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5299
```

(a porta exata vem do `launchSettings.json` gerado no seu projeto — use a que aparecer no seu terminal)

```bash
curl -X POST "http://localhost:5299/orders?amount=60"
```
```text
HTTP/1.1 200 OK
```

Funciona de ponta a ponta: HTTP → `Hub.Api` → `BC1.Application` → `BC1.Domain`, gravando via `BC1.Infrastructure`.

---

## Verificando a estrutura até aqui

Depois de tudo isso, duas perguntas — "o que existe?" e "compila?" — têm um comando cada, e nenhum dos dois pede para você lembrar de nada:

```bash
dotnet sln list
```
```text
Project(s)
----------
Hub.Api/Hub.Api.csproj
Hub.App/Hub.App.csproj
Hub.Application/Hub.Application.csproj
Hub.Domain/Hub.Domain.csproj
src/BC1/BC1.Application/BC1.Application.csproj
src/BC1/BC1.Domain/BC1.Domain.csproj
src/BC1/BC1.Infrastructure/BC1.Infrastructure.csproj
src/BC2/BC2.Application/BC2.Application.csproj
src/BC2/BC2.Domain/BC2.Domain.csproj
src/SharedKernel/SharedKernel.csproj
```

```bash
dotnet build
```
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

10 projetos, uma Solution, zero avisos. O `dotnet build`, sozinho, computou a ordem certa a partir das referências — a mesma ordem que você foi decidindo, decisão por decisão, do Nível 2 em diante.

---

## Mais uma peça: Tests, o nó-folha

```bash
dotnet new xunit -o Hub.Tests
dotnet sln add Hub.Tests
dotnet add Hub.Tests reference src/BC1/BC1.Application
dotnet test
```

*(Esta é a exceção citada na abertura: o ambiente usado para escrever este material não tem acesso à internet para baixar os pacotes do xUnit, então não consegui rodar estes quatro comandos de verdade aqui. No seu ambiente, com internet — que é o caso normal —, eles funcionam exatamente como qualquer outro `dotnet new`/`add`/comando de build que você já viu até aqui, e `dotnet test` produz algo como `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`.)*

O que importa arquiteturalmente, e isso não depende de internet para ser verdade: `Hub.Tests` referencia o que precisa testar. **Nada referencia `Hub.Tests`** — ele é uma ponta solta no grafo, de propósito. Se algum outro projeto o referenciasse, o código de teste (e suas dependências, como o próprio xUnit) acabaria empacotado dentro do artefato de produção.

---

## O que fica para depois

Ficou pendente a pergunta do Nível 3: **como `BC1` e `BC2` conversam, se não pode ser referência direta de Domain para Domain?** Não vou construir a resposta aqui — só nomear as próximas perguntas que ela abre, porque é isso que você vai precisar saber procurar:

- **Contracts**: um projeto pequeno, sem regra de negócio, com apenas interfaces/DTOs que um BC expõe para fora. É o que substitui "referenciar o Domain do outro" por "referenciar o que ele decidiu publicar".
- **ACL (Anti-Corruption Layer)**: quando `BC1` consome algo de `BC2`, um adaptador dentro do próprio `BC1` traduz o modelo alheio para a linguagem do `BC1`, para que um `Order` nunca precise "saber" como um `Shipment` é modelado.
- **Integration**: o mecanismo concreto por trás do Contract — uma chamada HTTP, uma fila, um evento. É detalhe de infraestrutura, então mora perto da `Infrastructure`, não do `Domain`.

Cada um desses é uma decisão de custo/benefício, não uma regra fixa. A pergunta certa, de novo, não é "qual padrão uso", é: **essa dependência deveria existir, e se sim, direta ou mediada?**

---

## O que você deveria conseguir fazer sozinho agora

Não decorou nenhum comando — e não precisa. O que muda é isto: diante de um problema, você sabe o que procurar.

| Você pensa... | Você procura... |
|---|---|
| "Preciso de um contêiner para os projetos" | `dotnet new sln` |
| "Preciso materializar uma parte da arquitetura" | `dotnet new classlib` / `console` / `web` |
| "Este projeto depende daquele" | `dotnet add reference` |
| "Será que essa dependência já existe / está certa?" | `dotnet sln list`, ler o `.csproj`, `dotnet build` |
| "Não lembro a sintaxe exata" | `dotnet <comando> --help` |

E o mais importante ficou provado na prática, não só dito: a ferramenta constrói o que você mandar, inclusive os erros. `dotnet add reference` não impediu a referência circular do Nível 2 — quem impediu foi o `dotnet build`, e só depois que você já tinha tomado a decisão errada. A pergunta *"essa dependência deveria existir?"* precisa ser seu primeiro passo, não uma correção depois do terceiro.
