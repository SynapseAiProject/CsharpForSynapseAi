Deixa eu verificar rapidamente o comportamento real do `dotnet new` com post-actions de referência, porque a documentação é um pouco ambígua nesse ponto específico — prefiro confirmar antes de te passar algo que pode não funcionar.A ideia central (multi-projeto num único `dotnet new`, com `-n` parametrizando o nome) é totalmente viável. Fui conferir a documentação oficial do template engine porque a parte de "criar referências automaticamente" tem uma pegadinha que vale a pena evitar — vou te mostrar o caminho que fica mais previsível.

## A estrutura do template

```
templates/bc-module/
├── .template.config/
│   └── template.json
├── setup-references.sh
└── BcName/
    ├── Domain/
    │   ├── BcName.Domain.csproj
    │   └── Aggregates/.gitkeep
    ├── Application/
    │   ├── BcName.Application.csproj
    │   ├── UseCases/.gitkeep
    │   ├── DTOs/.gitkeep
    │   ├── Interfaces/.gitkeep
    │   └── Validators/.gitkeep
    └── Infrastructure/
        ├── BcName.Infrastructure.csproj
        ├── Persistence/.gitkeep
        └── Adapters/.gitkeep
```

`BcName` é só um token — ele nunca aparece no resultado final. O template engine faz busca-e-substitui em **nomes de pastas, nomes de arquivos e conteúdo dos arquivos**, trocando `BcName` pelo valor que a pessoa passar em `-n`. Então `dotnet new bcmodule -n Faturamento` gera `Faturamento/Domain/Faturamento.Domain.csproj` etc., e `dotnet new bcmodule -n Estoque` gera a mesma coisa para "Estoque". Nenhum nome fica hardcoded.

## template.json

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Seu time",
  "classifications": ["DDD", "Modular Monolith"],
  "identity": "Hub.BoundedContextModule",
  "name": "Bounded Context (Domain/Application/Infrastructure)",
  "shortName": "bcmodule",
  "sourceName": "BcName",
  "preferNameDirectory": false,
  "tags": { "language": "C#", "type": "project" },
  "primaryOutputs": [
    { "path": "BcName/Domain/BcName.Domain.csproj" },
    { "path": "BcName/Application/BcName.Application.csproj" },
    { "path": "BcName/Infrastructure/BcName.Infrastructure.csproj" }
  ],
  "postActions": [
    {
      "description": "Adiciona os 3 projetos à solution",
      "actionId": "D396686C-DE0E-4DE6-906D-291CD29FC5DE",
      "continueOnError": true,
      "manualInstructions": [{ "text": "Rode 'dotnet sln add' manualmente para cada csproj gerado" }]
    },
    {
      "description": "Cria as referências entre os projetos (internas e com o SharedKernel)",
      "actionId": "3A7C4B45-1F5D-4A30-959A-51B88E82B5D2",
      "continueOnError": false,
      "args": {
        "executable": "bash",
        "args": "setup-references.sh",
        "redirectStandardOutput": false,
        "redirectStandardError": false
      },
      "manualInstructions": [{ "text": "Rode ./setup-references.sh manualmente" }]
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

**A decisão importante aqui:** para adicionar à solution eu uso o post-action nativo `AddProjectToSolutionFile` (bem documentado, sem ambiguidade). Mas para criar as *referências entre projetos* eu **não** confiei no post-action nativo `AddReference` (`B17581D1...`). A documentação dele é ambígua justamente no caso que você quer — referenciar um projeto que *já existe fora do template* (o `SharedKernel`) — e a forma "oficial" de resolver isso envolve símbolos com `fileRename`, o que fica frágil e difícil de depurar quando dá erro. Como não tenho um ambiente com o SDK aqui para testar esse caminho ao vivo, preferi te dar algo que eu tenho certeza que funciona: um script bash simples, que é **gerado junto com o projeto** (então o `BcName` dentro dele também vira `Faturamento` automaticamente) e disparado pelo post-action `RunScript`, que é bem simples e testado há anos.

## setup-references.sh

```bash
#!/usr/bin/env bash
set -e

NAME="BcName"
SHARED="SharedKernel"

dotnet add "$NAME/Application/$NAME.Application.csproj" \
  reference "$NAME/Domain/$NAME.Domain.csproj"

dotnet add "$NAME/Infrastructure/$NAME.Infrastructure.csproj" \
  reference "$NAME/Application/$NAME.Application.csproj"

dotnet add "$NAME/Domain/$NAME.Domain.csproj" \
  reference "$SHARED/Domain/$SHARED.Domain.csproj"

dotnet add "$NAME/Application/$NAME.Application.csproj" \
  reference "$SHARED/Application/$SHARED.Application.csproj"

dotnet add "$NAME/Infrastructure/$NAME.Infrastructure.csproj" \
  reference "$SHARED/Infrastructure/$SHARED.Infrastructure.csproj"

# limpa o script gerado — ele já cumpriu sua função
rm -- "$0"
```

Esse script assume que você roda o `dotnet new` **de dentro da pasta `src/`** (porque o `RunScript` executa com o working directory igual à raiz do conteúdo gerado, e `SharedKernel/...` só resolve corretamente se `SharedKernel` for irmão de `Faturamento/` nesse nível).

## Os 3 `.csproj` do template

```xml
<!-- BcName/Domain/BcName.Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>BcName.Domain</RootNamespace>
  </PropertyGroup>
</Project>
```

(idem para `Application` e `Infrastructure`, trocando o `RootNamespace` — ajuste o `TargetFramework` pra versão do .NET que vocês usam).

## Como instalar e usar

```bash
# instala localmente a partir da pasta (bom pra desenvolver/testar o template)
dotnet new install ./templates/bc-module

# uso
cd src
dotnet new bcmodule -n Faturamento --allow-scripts yes
```

O `--allow-scripts yes` é necessário porque `RunScript` pede confirmação Y/N por padrão (é uma proteção de segurança — a Microsoft não quer que qualquer template baixado da internet rode código arbitrário sem consentimento). Sem essa flag, o CLI vai perguntar interativamente.

Pra reinstalar depois de editar o template:
```bash
dotnet new uninstall ./templates/bc-module
dotnet new install ./templates/bc-module
```

## Distribuindo pro time (nupkg)

Se quiser publicar num feed NuGet interno em vez de cada um clonar a pasta:

```xml
<!-- templates.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageType>Template</PackageType>
    <PackageId>Hub.Templates.BcModule</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <TargetFramework>net9.0</TargetFramework>
    <IncludeContentInPack>true</IncludeContentInPack>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <ContentTargetFolders>content</ContentTargetFolders>
  </PropertyGroup>
  <ItemGroup>
    <Content Include="bc-module\**\*" Exclude="bc-module\**\bin\**;bc-module\**\obj\**" />
  </ItemGroup>
</Project>
```

```bash
dotnet pack
dotnet new install ./bin/Release/Hub.Templates.BcModule.1.0.0.nupkg
# ou, com um feed interno configurado:
dotnet new install Hub.Templates.BcModule
```

## Duas ressalvas

1. **Teste o template uma vez antes de distribuir pro time.** Eu não consegui rodar `dotnet new` de verdade aqui pra validar o fluxo ponta a ponta (não tenho o SDK disponível neste ambiente), então baseei o design em documentação oficial + no que é comportamento bem estabelecido dos post-actions — mas vale rodar `dotnet new bcmodule -n Teste --allow-scripts yes` numa pasta descartável e abrir os `.csproj` gerados pra conferir se as `<ProjectReference>` saíram certas antes de confiar nele em produção.

2. **O template resolve a criação, não a manutenção.** Ele garante que todo BC *novo* nasça com a topologia certa, mas nada impede alguém de rodar `dotnet add BC1 reference BC2` manualmente seis meses depois. Isso reforça o ponto que eu levantei antes: vale complementar com um teste de arquitetura (NetArchTest/ArchUnitNET) no CI que falhe se essa regra for violada — o template cuida do "nascimento", o teste de arquitetura cuida da vida inteira do projeto.
