Sim, e a boa notícia é que esse "hub" fica mais simples que o `bcmodule`, porque aqui você não precisa lutar com nenhuma questão de referência a projeto pré-existente — tudo que ele cria é novo. Vou seguir o mesmo padrão: um script bash embutido no template (que herda a substituição de nome automaticamente), disparado via post-action `RunScript`.

## A ideia

O `bchub` não gera arquivos de projeto prontos (como o `bcmodule` fazia com os `.csproj`) — ele só carrega um script que **reproduz exatamente a sequência de comandos** que vocês já validaram na primeira mensagem (criar sln, SharedKernel, Host, Tests). Isso evita ter que "hand-craft" um `.sln` com GUIDs manualmente, que é a parte mais frágil de tentar templatizar isso via arquivos estáticos.

## Estrutura do template

```
templates/bc-hub/
├── .template.config/
│   └── template.json
└── setup-hub.sh
```

## template.json

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Seu time",
  "classifications": ["DDD", "Modular Monolith", "Solution"],
  "identity": "Hub.SolutionScaffold",
  "name": "Hub - Modular Monolith Scaffold",
  "shortName": "bchub",
  "sourceName": "Hub",
  "preferNameDirectory": true,
  "tags": { "language": "C#", "type": "solution" },
  "postActions": [
    {
      "description": "Cria a solution, o SharedKernel, o Host e os testes",
      "actionId": "3A7C4B45-1F5D-4A30-959A-51B88E82B5D2",
      "continueOnError": false,
      "args": {
        "executable": "bash",
        "args": "setup-hub.sh",
        "redirectStandardOutput": false,
        "redirectStandardError": false
      },
      "manualInstructions": [{ "text": "Rode ./setup-hub.sh manualmente" }]
    },
    {
      "description": "Restaura os pacotes NuGet",
      "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
      "continueOnError": true,
      "manualInstructions": [{ "text": "Rode 'dotnet restore'" }]
    }
  ]
}
```

`"preferNameDirectory": true` é o detalhe que faz esse template se comportar como "criador de repositório": quando a pessoa roda `dotnet new bchub -n MeuSistema` sem passar `-o`, o engine cria sozinho a pasta `MeuSistema/` e joga o conteúdo gerado lá dentro — você não precisa simular isso manualmente como fez no `bcmodule` (lá o "BcName/" precisava existir como pasta dentro do template porque ele nasce *dentro* de um `src/` que já existe).

## setup-hub.sh

```bash
#!/usr/bin/env bash
set -e

NAME="Hub"

# 1. Solution
dotnet new sln -n "$NAME"

mkdir -p src tests

# 2. SharedKernel
dotnet new classlib -n SharedKernel.Domain -o src/SharedKernel/Domain
dotnet new classlib -n SharedKernel.Application -o src/SharedKernel/Application
dotnet new classlib -n SharedKernel.Infrastructure -o src/SharedKernel/Infrastructure

dotnet sln "$NAME.sln" add \
  src/SharedKernel/Domain/SharedKernel.Domain.csproj \
  src/SharedKernel/Application/SharedKernel.Application.csproj \
  src/SharedKernel/Infrastructure/SharedKernel.Infrastructure.csproj

dotnet add src/SharedKernel/Application/SharedKernel.Application.csproj \
  reference src/SharedKernel/Domain/SharedKernel.Domain.csproj

dotnet add src/SharedKernel/Infrastructure/SharedKernel.Infrastructure.csproj \
  reference src/SharedKernel/Application/SharedKernel.Application.csproj

# 3. Host
dotnet new webapi -n "$NAME.Api" -o "src/Host/$NAME.Api"
dotnet sln "$NAME.sln" add "src/Host/$NAME.Api/$NAME.Api.csproj"

# 4. Tests
dotnet new xunit -n "$NAME.Tests" -o "tests/$NAME.Tests"
dotnet sln "$NAME.sln" add "tests/$NAME.Tests/$NAME.Tests.csproj"

# limpa o script gerado
rm -- "$0"
```

Como esse arquivo vive **fora** de `.template.config/`, ele passa pelo mesmo mecanismo de substituição que os `.csproj` do `bcmodule`: o literal `"Hub"` na linha `NAME="Hub"` vira `"MeuSistema"` (ou o que for passado em `-n`) antes do script rodar — todo o resto do script usa `$NAME`, então não precisa repetir a substituição em cada linha.

Reparem que eu **não** referenciei nada dentro de `SharedKernel` a partir de `Host` ou `Tests` aqui — isso é intencional, o "berço" fica neutro; as referências específicas (Host → BCx.Infrastructure, Tests → BCx.Domain/Application) só fazem sentido depois que existe pelo menos um BC, e aí é o `bcmodule` que cuida disso.

## Instalando os dois templates juntos

Como `bchub` e `bcmodule` são dois templates numa mesma família, faz sentido empacotá-los na mesma pasta e instalar de uma vez:

```
templates/
├── bc-hub/
│   ├── .template.config/template.json
│   └── setup-hub.sh
└── bc-module/
    ├── .template.config/template.json
    ├── setup-references.sh
    └── BcName/...
```

```bash
dotnet new install ./templates
```

Esse comando varre a pasta e instala **todos** os templates que encontrar dentro dela — não precisa instalar um por um.

## Uso ponta a ponta

```bash
dotnet new bchub -n MeuSistema --allow-scripts yes
cd MeuSistema/src
dotnet new bcmodule -n Faturamento --allow-scripts yes
dotnet new bcmodule -n Estoque --allow-scripts yes
cd ..
dotnet build
```

## Uma ressalva igual à anterior

Não tenho o SDK disponível neste ambiente pra rodar essa sequência de ponta a ponta e confirmar 100% (por exemplo, o comportamento exato do working directory quando `preferNameDirectory` cria a pasta automaticamente é algo que eu recomendaria conferir na prática). Vale rodar esse fluxo uma vez numa pasta descartável, olhar se o `Hub.sln list` sai igual ao que vocês esperavam, antes de distribuir pro time — mas a lógica em si (comandos `dotnet new`/`dotnet sln`/`dotnet add reference` puros) é exatamente a mesma que vocês já validaram manualmente na primeira mensagem, só que agora dirigida por um script em vez de ser digitada à mão.
