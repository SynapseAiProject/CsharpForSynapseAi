Dá, mas **eu não faria exatamente assim**. Você está tentando inferir as referências a partir da estrutura de diretórios, e isso pode funcionar, mas fica frágil.

 Além disso, pelo que entendi, você quer automatizar esta regra:

```
SharedKernel.Domain
        ↓
BCx.Domain

SharedKernel.Application
        ↓
BCx.Application

SharedKernel.Infrastructure
        ↓
BCx.Infrastructure
```

 e também:

```
BCx.Domain
      ↓
BCx.Application
      ↓
BCx.Infrastructure
```

 ## Uma forma simples

 Se a convenção dos nomes for consistente, dá para fazer Bash.

 Por exemplo:

```
for bc in src/*/; do
    [ -d "$bc/Domain" ] || continue

    name=$(basename "$bc")

    dotnet add "$bc/Application/$name.Application.csproj" \
        reference "$bc/Domain/$name.Domain.csproj"

    dotnet add "$bc/Infrastructure/$name.Infrastructure.csproj" \
        reference "$bc/Application/$name.Application.csproj"

    dotnet add "$bc/Domain/$name.Domain.csproj" \
        reference "src/SharedKernel/Domain/SharedKernel.Domain.csproj"

    dotnet add "$bc/Application/$name.Application.csproj" \
        reference "src/SharedKernel/Application/SharedKernel.Application.csproj"

    dotnet add "$bc/Infrastructure/$name.Infrastructure.csproj" \
        reference "src/SharedKernel/Infrastructure/SharedKernel.Infrastructure.csproj"
done
```

 Isso já automatiza **qualquer BC novo** que siga sua convenção:

```
src/
├── SharedKernel/
├── BC1/
├── BC2/
├── BC3/
└── BC4/
```

 Você não precisa escrever nada específico para `BC3` ou `BC4`.

---

 ## Mas existe uma abordagem ainda melhor

 Se você quer realmente **automatizar a arquitetura**, eu evitaria descobrir dependências por `find`.

 Defina a arquitetura explicitamente em um script:

```
SHARED_DOMAIN="src/SharedKernel/Domain/SharedKernel.Domain.csproj"
SHARED_APP="src/SharedKernel/Application/SharedKernel.Application.csproj"
SHARED_INFRA="src/SharedKernel/Infrastructure/SharedKernel.Infrastructure.csproj"

for bc in src/*/; do
    name=$(basename "$bc")

    case "$name" in
        SharedKernel|Host) continue ;;
    esac

    domain="$bc/Domain/$name.Domain.csproj"
    app="$bc/Application/$name.Application.csproj"
    infra="$bc/Infrastructure/$name.Infrastructure.csproj"

    dotnet add "$app" reference "$domain"
    dotnet add "$infra" reference "$app"

    dotnet add "$domain" reference "$SHARED_DOMAIN"
    dotnet add "$app" reference "$SHARED_APP"
    dotnet add "$infra" reference "$SHARED_INFRA"
done
```

 E depois você pode ter:

```
dotnet sln Hub.sln add $(find src tests -name "*.csproj")
```

 ou a versão mais robusta:

```
find src tests -name "*.csproj" -exec dotnet sln Hub.sln add {} \;
```

 ### A grande vantagem

 Seu diretório passa a funcionar como uma **convenção arquitetural**:

```
src/BC_X/
    Domain/
    Application/
    Infrastructure/
```

 O script sabe automaticamente:

```
Application → Domain
Infrastructure → Application

Domain → SharedKernel.Domain
Application → SharedKernel.Application
Infrastructure → SharedKernel.Infrastructure
```

 Então criar um novo BC vira praticamente:

```
mkdir -p src/BC3

dotnet new classlib -n BC3.Domain -o src/BC3/Domain
dotnet new classlib -n BC3.Application -o src/BC3/Application
dotnet new classlib -n BC3.Infrastructure -o src/BC3/Infrastructure

./setup.sh
```

 E o `setup.sh` configura **solution + referências automaticamente**.

 Só uma observação importante: **não recomendo usar "se o caminho contém X, então crie referência para Y" como regra geral**. A estrutura de diretórios é uma boa convenção, mas as dependências arquiteturais devem ser determinadas pela regra que você definiu, como no script acima. Isso evita referências acidentais conforme o projeto crescer.
