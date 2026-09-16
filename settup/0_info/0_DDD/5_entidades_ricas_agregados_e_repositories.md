## Entidades Anêmicas vs. Entidades Ricas

**Anêmica**: só guarda dado (getters/setters públicos). Nenhuma regra mora nela — tudo é decidido em Services externos que leem e escrevem seus campos livremente.

**Rica**: guarda dado *e* comportamento. As regras que protegem esse dado (invariantes) ficam dentro dela, expostas como métodos — você não seta estado, você pede uma ação.

```csharp
// Anêmica — nada impede um estado inválido
public class Assinatura { public string Status { get; set; } }
assinatura.Status = "Cancelada"; // ninguém validou nada

// Rica — a regra vive junto do dado
public class Assinatura
{
    public string Status { get; private set; } = "Ativa";
    public void Cancelar()
    {
        if (Status == "Cancelada") throw new InvalidOperationException();
        Status = "Cancelada";
    }
}
```

Rica é preferível porque é **impossível** mudar o estado sem passar pela regra — o compilador te obriga. No modelo anêmico, a mesma validação tende a se espalhar (e divergir) por vários Services diferentes, cada um podendo esquecer um caso.

## Agregados

Um agregado é um conjunto de entidades e value objects que precisam mudar **juntos, de forma consistente**, tratado como uma unidade única.

Propósito: dar um limite claro de consistência. Dentro do agregado, toda regra é garantida sempre. Fora dele, entre agregados diferentes, você aceita consistência eventual (via evento, não via transação).

Invariante = regra que sempre precisa ser verdadeira (ex: "total = soma dos itens"). O agregado é exatamente o escopo dentro do qual essa regra é validada — e a transação de banco deve cobrir esse escopo inteiro, nem mais, nem menos. Se sua transação toca dois agregados ao mesmo tempo, é sinal de fronteira mal desenhada.

## Aggregate Root

É a entidade que representa o agregado inteiro pro mundo de fora. Nem toda Entity é Root — as internas (ex: `ItemPedido`) continuam sendo entidades normais, só que inacessíveis e não-persistíveis isoladamente.

```csharp
public class Pedido // Aggregate Root
{
    private readonly List<ItemPedido> _itens = new();
    public IReadOnlyCollection<ItemPedido> Itens => _itens.AsReadOnly();
    public decimal Total { get; private set; }

    public void AdicionarItem(Guid produtoId, decimal preco, int qtd)
    {
        if (qtd <= 0) throw new ArgumentException("Quantidade inválida.");
        _itens.Add(new ItemPedido(produtoId, preco, qtd));
        Total = _itens.Sum(i => i.Subtotal);
    }
}
```

É o único ponto de entrada porque, se código externo pudesse mexer em `_itens` direto, ninguém garantiria o `Total` recalculado — a mesma falha da entidade anêmica, só que em escala de agregado.

## Repositories

Responsabilidade: carregar e salvar o Aggregate Root **inteiro**, como se fosse uma coleção em memória — não persistir entidades internas isoladamente.

```csharp
public interface IPedidoRepository
{
    Pedido? ObterPorId(Guid id);
    void Salvar(Pedido pedido); // itens vêm junto, sempre
}
```

Só o Root tem Repository porque o agregado é a unidade transacional. Se `ItemPedido` tivesse seu próprio Repository, alguém poderia salvá-lo sozinho, sem passar pelas regras do Root — quebrando exatamente a garantia que o agregado existe pra dar. Regra prática: se você sente vontade de criar `IItemPedidoRepository`, ou `ItemPedido` não devia ser entidade separada, ou seu agregado devia ser dois.

## Watched Lists

É uma coleção interna do Aggregate Root que sabe rastrear, sozinha, o que foi adicionado, removido ou alterado desde que o agregado foi carregado.

Problema que resolve: sem isso, salvar mudanças numa coleção (ex: itens de pedido) exige comparar tudo com o banco na hora de persistir, ou apagar e reinserir — caro e propenso a erro. A Watched List já sabe o delta, porque registrou a mudança no momento em que ela aconteceu.

```csharp
public class Pedido
{
    private readonly List<ItemPedido> _itens = new();
    private readonly List<ItemPedido> _adicionados = new();
    private readonly List<ItemPedido> _removidos = new();

    public void AdicionarItem(ItemPedido item)
    {
        _itens.Add(item);
        _adicionados.Add(item); // rastreado no ato
    }
}
```

O Repository, ao salvar, olha só `_adicionados`/`_removidos` — não recalcula nada do zero. E como toda mudança passa pelos métodos do Root, rastrear e validar invariante viram o mesmo passo — não dois processos que podem divergir. (O EF Core, via change tracker, faz algo parecido automaticamente; Watched List manual é pra quando você quer esse controle explícito dentro do próprio domínio.)

## Resumo Mental

- **Entidade** → comportamento e identidade: carrega o dado e a regra que o protege.
- **Aggregate** → fronteira de consistência: o que muda junto, é validado junto.
- **Aggregate Root** → porta de entrada do agregado: só por ele se entra e sai.
- **Repository** → persistência do agregado: salva/carrega o Root inteiro, nunca peças soltas.
- **Watched List** → acompanhamento das mudanças nas coleções: sabe o delta sem recalcular tudo.
