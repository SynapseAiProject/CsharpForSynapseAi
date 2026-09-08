# 02 — C# avançado

Os recursos que fazem C# parecer "outra linguagem" pra quem vem de C/C++: generics de
verdade, funções como valores, LINQ, e o mecanismo de limpeza de recursos.

## Generics

### Conceito
Tipos e métodos parametrizados por tipo, verificados em tempo de compilação.

### Como funciona
```csharp
public class Repository<T> where T : class
{
    private readonly List<T> _items = new();
    public void Add(T item) => _items.Add(item);
    public IEnumerable<T> All() => _items;
}

var todoRepo = new Repository<Todo>();
var grantRepo = new Repository<Grant>();
```

`where T : class` é uma **constraint** — restringe quais tipos `T` pode ser (aqui, só
reference types). Outras constraints comuns: `where T : new()`, `where T : IComparable<T>`.

### Equivalente C/C++
Mentalmente parecido com templates, mas a mecânica é diferente: templates em C++ são
**expandidos em compile-time** (cada instanciação gera código próprio, erros aparecem só ao
instanciar). Generics em C# são **verificados na declaração** (o `Repository<T>` já precisa
compilar sozinho, valendo para qualquer `T` que satisfaça as constraints) e o JIT especializa
o código em runtime para value types, compartilhando código entre reference types.

### Armadilha comum
Esperar que erros de uso apareçam só na instanciação, como em C++ (SFINAE, etc.) — em C# o
`Repository<T>` inteiro já precisa ser válido para *qualquer* `T` compatível com a constraint,
antes mesmo de alguém usar `Repository<Todo>`.

> 🔬 **Aprofundamento opcional** — a diferença entre reified generics (Java, com type erasure)
> e o modelo do .NET (specialization real para value types, compartilhamento para reference
> types) é aprofundada em [03](03-runtime-dotnet.md).

### Exercício
Torne o armazenamento do Todo CLI genérico: `Repository<T>` reutilizável, e confirme que
funciona tanto para `Todo` quanto para um segundo tipo qualquer.

---

## Delegates

### Conceito
Um tipo que representa "uma função com essa assinatura" — uma variável pode guardar uma
referência a um método.

### Como funciona
```csharp
public delegate bool TodoFilter(Todo todo);

TodoFilter isDone = t => t.IsDone;
bool result = isDone(someTodo);
```

Delegates em C# são **multicast**: uma variável de delegate pode referenciar várias funções
ao mesmo tempo (`+=` adiciona, `-=` remove) — é a base dos `event`s.

### Equivalente C/C++
Function pointer, mas com tipo seguro (a assinatura é checada) e suporte nativo a múltiplos
alvos (multicast) — em C você simularia isso com um array de ponteiros de função.

### Armadilha comum
Vindo de C, é tentador pensar em guardar métodos de instância como se fossem só o endereço de
uma função — mas um delegate de método de instância também guarda a referência ao objeto
(`this`) implicitamente. Isso é ótimo (evita passar contexto manualmente), mas segura o objeto
vivo enquanto o delegate existir — cuidado com vazamento de memória por assinatura de evento
nunca removida.

### Exercício
Sem exercício isolado — usado abaixo.

---

## Func, Action, Predicate

### Conceito
Delegates genéricos prontos da BCL, para não precisar declarar um `delegate` toda vez.

| Tipo | Assinatura | Equivalente mental |
|---|---|---|
| `Action` | `void Foo()` | procedimento sem retorno |
| `Action<T>` | `void Foo(T x)` | idem, com 1+ parâmetros (até `Action<T1..T16>`) |
| `Func<TResult>` | `TResult Foo()` | função sem parâmetro, com retorno |
| `Func<T, TResult>` | `TResult Foo(T x)` | idem, com parâmetros (último tipo é sempre o retorno) |
| `Predicate<T>` | `bool Foo(T x)` | equivalente a `Func<T, bool>`, usado por convenção em buscas |

```csharp
Func<Todo, bool> isDone = t => t.IsDone;
Action<Todo> print = t => Console.WriteLine(t.Title);
```

### Exercício
Sem exercício isolado — usado no exercício de módulo.

---

## Lambdas

### Conceito
Sintaxe curta para criar um delegate/`Func`/`Action` inline, com **captura de variáveis do
escopo** (closure).

```csharp
int threshold = 3;
Func<int, bool> aboveThreshold = n => n > threshold;   // captura 'threshold' por referência ao contexto
```

### Equivalente C/C++
Comparável a uma lambda de C++11+ (`[&]` ou `[=]`), mas em C# a captura **é sempre por
referência à variável** (não uma cópia) — se `threshold` mudar depois, a lambda enxerga o
valor novo.

### Armadilha comum
Capturar a variável de controle de um `for` clássico dava bug clássico em versões antigas de
C# (todas as lambdas viam o valor final); em C# moderno (5+), cada iteração de `foreach` tem
sua própria variável — mas vale confirmar esse detalhe se vir código legado.

### Exercício
Sem exercício isolado — usado no exercício de módulo.

---

## LINQ

### Conceito
Consultas sobre qualquer `IEnumerable<T>` (coleções em memória) ou `IQueryable<T>` (traduzido
para SQL — ver [07](07-ef-core-multitenant.md)), com sintaxe de método ou de query.

### Como funciona
```csharp
var pendentes = todos
    .Where(t => !t.IsDone)
    .OrderBy(t => t.Title)
    .Select(t => t.Title)
    .ToList();

var algumUrgente = todos.Any(t => t.Priority == Priority.High);
var total = todos.Count(t => t.IsDone);
var porPrioridade = todos.GroupBy(t => t.Priority);
```

**Execução adiada (deferred execution)**: `Where`/`Select`/`OrderBy` não rodam imediatamente —
só quando você itera o resultado (`foreach`, `ToList()`, `Count()`...). Isso importa muito com
EF Core: uma `IQueryable` só vira SQL de verdade no momento da execução.

### Equivalente C/C++
Sem equivalente direto — o mais próximo mentalmente são os algoritmos de `<algorithm>`
(`std::find_if`, `std::sort`, `std::accumulate`) encadeados, mas LINQ é muito mais expressivo
e, com EF Core, o mesmo código também vira SQL.

### Armadilha comum
Encadear várias operações e esquecer que nada rodou ainda até um `.ToList()`/`foreach` —
inclui o risco de reexecutar a query inteira sempre que você iterar a mesma `IEnumerable` sem
materializar o resultado uma vez.

> 🔬 **Aprofundamento opcional** — LINQ é "só" um conjunto de extension methods sobre
> `IEnumerable<T>` — ver extension methods, abaixo, para entender como isso é possível sem
> mudar a linguagem.

### Exercício
Parte do exercício do fim do módulo.

---

## IDisposable e using

### Conceito
O mecanismo de C# para liberar recursos não-gerenciados (conexões, arquivos, handles) de
forma determinística — porque o GC **não é determinístico** e não sabe fechar uma conexão de
banco na hora certa.

### Como funciona
```csharp
public class DbConnection : IDisposable
{
    public void Dispose() { /* fecha o recurso */ }
}

using (var conn = new DbConnection())
{
    // usa conn
}   // Dispose() chamado automaticamente aqui, mesmo se der exception

// ou, forma moderna (using declaration):
using var conn2 = new DbConnection();
// Dispose() chamado no fim do escopo (do método, aqui)
```

### Equivalente C/C++
Isso É o RAII que você já usa em C++ — só que explícito e opt-in por `using`, em vez de
automático em todo destrutor de objeto na stack. Em C#, só objetos que implementam
`IDisposable` têm essa garantia, e só dentro de um bloco `using`.

### Armadilha comum
Achar que o **GC** cuida de fechar conexões/arquivos como um destrutor de C++ cuidaria — não
cuida. Sem `using`/`Dispose()` explícito, um recurso não-gerenciado (handle de arquivo,
conexão de socket) pode ficar aberto por muito tempo até o GC coletar o objeto (se coletar).
`DbContext` do EF Core, `HttpClient` de vida curta, `FileStream` — todos exigem `using`.

### Exercício
Implemente uma classe `TodoFileLogger : IDisposable` que escreve cada ação do Todo CLI num
arquivo, e use `using` para garantir que o arquivo feche mesmo se o programa lançar exception
no meio.

---

## async/await — o suficiente para não travar agora

### Conceito
`Task`/`Task<T>` representam uma operação que ainda não terminou. `async`/`await` é a sintaxe
para "esperar" por ela sem bloquear a thread.

### Como funciona
```csharp
public async Task<string> BuscarConteudoAsync(string url)
{
    using var client = new HttpClient();
    string body = await client.GetStringAsync(url);   // libera a thread enquanto espera a rede
    return body;
}
```

### Equivalente C/C++
Diferente de bloquear uma thread esperando I/O (`read()` síncrono) ou de gerenciar um loop de
eventos manual (como no seu Webserver em C++). `await` é tratado a fundo, com "o que
realmente acontece por baixo", no [módulo 03](03-runtime-dotnet.md) — aqui você só precisa
saber ler e escrever o padrão.

### Armadilha comum
Nunca use `.Result` ou `.Wait()` para "forçar" uma `Task` a terminar de forma síncrona num
código async — isso pode travar a aplicação (deadlock). Use `await` até o topo da pilha de
chamadas.

### Exercício
Adiado para o próximo módulo — aqui só familiarize-se lendo/escrevendo um método `async Task`
simples que aguarda `Task.Delay(1000)`.

---

## Extension methods

### Conceito
Um jeito de "adicionar" um método a um tipo que você não controla (inclusive tipos da BCL),
sem herdar ou modificar o original.

### Como funciona
```csharp
public static class StringExtensions
{
    public static bool IsValidEmail(this string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@');
}

"a@b.com".IsValidEmail();   // chamado como se fosse método de instância de string
```

O `this` no primeiro parâmetro é o que marca o método como extensão. É assim que todo o LINQ
(`.Where`, `.Select`...) é implementado sobre `IEnumerable<T>` — não é mágica da linguagem, é
biblioteca.

### Equivalente C/C++
Sem equivalente direto — o mais próximo seria escrever uma função livre
(`bool is_valid_email(const char*)`), mas sem o açúcar de sintaxe de chamar como método.

### Armadilha comum
Abusar de extension methods para "pendurar" comportamento de domínio em tipos genéricos
(`string`, `int`) deixa o código difícil de navegar — use com moderação, principalmente para
utilitários (como o LINQ faz) e não para regra de negócio central.

### Exercício
Sem exercício isolado.

---

## Exercício do módulo — Todo CLI, versão avançada

Evolua o Todo CLI do [módulo 01](01-csharp-essencial.md):

1. Troque o armazenamento por `Repository<Todo>` genérico.
2. Reescreva os filtros/listagens usando LINQ (`Where`, `OrderBy`, `GroupBy` por `Priority`).
3. Adicione um mini "event manager": um `event Action<Todo> OnTodoCompleted` na classe do
   repositório ou serviço, disparado quando um todo é marcado como concluído — e um
   `Console.WriteLine` assinando esse evento para imprimir "✔ concluído: {title}".
4. Envolva a leitura/escrita de um arquivo de persistência simples (texto ou JSON) numa classe
   `IDisposable`, usada com `using`.

Critério de pronto: o Todo CLI mantém todo o comportamento do módulo 01, agora usando
generics, LINQ, delegates/events e `IDisposable` — sem nenhuma dependência externa além da
BCL.

Próximo módulo: **03 — Runtime .NET** *(próximo lote da apostila)*.
