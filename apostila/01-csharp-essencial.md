# 01 — C# essencial

Sintaxe e modelo da linguagem — o suficiente para escrever C# confortavelmente. Você já
conhece os conceitos; aqui você aprende como eles "vestem a roupa" do C#.

## Program.cs e o entry point

### Conceito
O ponto de entrada de um executável .NET.

### Como funciona
Desde o C# 9/10, um `Program.cs` com **top-level statements** dispensa a classe/`Main`
explícita — o compilador gera isso por trás:

```csharp
// Program.cs — isto É o programa inteiro
Console.WriteLine("Hello, Hub");
```

equivale a:

```csharp
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, Hub");
    }
}
```

### Equivalente C/C++
`int main(int argc, char **argv)`. `args` é o `argv` sem o nome do programa.

### Armadilha comum
Só pode existir **um** arquivo com top-level statements por projeto executável.

### Exercício
`dotnet new console -o scratch/hello`, rode com `dotnet run`, confirme que os dois estilos
(com e sem `Main` explícito) compilam igual.

---

## Value types vs reference types

### Conceito
Todo tipo em C# é **value type** (copiado por valor) ou **reference type** (copiado por
referência ao objeto no heap).

### Como funciona
- `struct`, `enum`, e os tipos primitivos (`int`, `bool`, `double`...) são **value types**.
  Vivem tipicamente na stack (ou inline dentro do objeto que os contém) e são **copiados**
  inteiros a cada atribuição ou passagem de parâmetro.
- `class`, `string`, arrays, delegates são **reference types**. A variável guarda uma
  referência para um objeto no heap gerenciado; atribuir ou passar copia a *referência*, não
  o objeto.

```csharp
struct Point { public int X, Y; }
class Box { public int Value; }

var p1 = new Point { X = 1, Y = 2 };
var p2 = p1;             // CÓPIA COMPLETA — p2 é independente
p2.X = 99;                // p1.X continua 1

var b1 = new Box { Value = 1 };
var b2 = b1;              // MESMA REFERÊNCIA — b1 e b2 apontam pro mesmo objeto
b2.Value = 99;             // b1.Value também é 99 agora
```

### Equivalente C/C++
Value type ≈ passar um `struct` por valor em C. Reference type ≈ sempre trabalhar com um
ponteiro para o `struct`/objeto — exceto que em C# você nunca vê o `*`/`&`; a referência é
implícita e o GC garante que o objeto vive enquanto houver referência para ele.

### Armadilha comum
Vindo de C++, é natural esperar que `class` se comporte como um `struct` empilhado. Em C#, é
o contrário: `class` sempre vai para o heap gerenciado; `struct` é que imita o comportamento
de valor que você já conhece de C.

> 🔬 **Aprofundamento opcional** — Isso é aprofundado com `stackalloc`, boxing/unboxing e o
> layout real de memória em [03 — Runtime .NET](03-runtime-dotnet.md).

### Exercício
Escreva uma função que recebe um `struct Point` e um `class Box` por parâmetro, modifica um
campo de cada um dentro da função, e imprima os valores antes/depois da chamada no chamador —
confirme na prática qual "vaza" a mudança e qual não.

---

## struct vs class — quando usar cada um

### Conceito
`struct` para pequenos agregados de dados imutáveis sem identidade própria (ex: `Point`,
`Money`). `class` para tudo que tem identidade, ciclo de vida, ou é "grande" o suficiente para
copiar ser caro.

### Equivalente C/C++
Regra prática: se em C++ você o passaria por `const&` para evitar cópia cara, provavelmente
deveria ser `class` em C#. Se copiar é barato e você *quer* semântica de valor (como um
`struct` pequeno em C), use `struct`.

### Armadilha comum
`struct` grande (muitos campos) é copiado por completo a cada passagem — pode ficar mais
lento que a `class` equivalente. No Hub de Apps, use `struct`/`record struct` só para tipos
pequenos (ex: um `GrantId` que embrulha um `Guid` — ver [05](05-modelagem-de-dominio.md));
entidades como `Hub`, `Grant`, `Session` são `class`.

### Exercício
Nenhum além do anterior — a decisão struct/class fica mais concreta no módulo 05, quando você
modela os value objects do Hub.

---

## Strings

### Conceito
`string` é um reference type, mas **imutável**: toda "modificação" cria uma nova string.

```csharp
string name = "Hub";
string greeting = $"Olá, {name}!";     // interpolação — substitui sprintf/concatenação manual
string multi = """
    texto
    de várias linhas
    """;                                 // raw string literal (C# 11+)
```

### Equivalente C/C++
Nada de `char*`/`strcpy`/`strlen` manuais — nem buffer overflow. Para concatenar em loop, use
`StringBuilder` (equivalente a montar um buffer você mesmo, mas seguro).

### Armadilha comum
Concatenar strings em loop com `+=` é O(n²) — cada `+=` aloca uma string nova. Para loops,
use `StringBuilder`.

### Exercício
Sem exercício isolado — usado nos próximos.

---

## Collections essenciais

### Conceito
`T[]` (array de tamanho fixo), `List<T>` (array dinâmico), `Dictionary<TKey,TValue>` (hash
map), `IEnumerable<T>` (a abstração que todas implementam — "algo que pode ser iterado").

### Equivalente C/C++
`List<T>` ≈ `std::vector<T>`. `Dictionary<K,V>` ≈ `std::unordered_map<K,V>`. `IEnumerable<T>`
não tem equivalente direto em C — é o contrato mínimo por trás do `foreach` (comparável a um
iterador de STL, mas como *interface*, não como conceito de template).

```csharp
var todos = new List<string> { "estudar", "codar" };
todos.Add("revisar");

var byId = new Dictionary<Guid, string>();
byId[Guid.NewGuid()] = "primeiro";

foreach (var todo in todos) Console.WriteLine(todo);
```

### Armadilha comum
`Dictionary<K,V>` não garante ordem de iteração (na prática costuma preservar inserção, mas
isso **não é contrato** — não confie nisso).

### Exercício
Parte do exercício do fim do módulo (Todo CLI).

---

## Classes — construtores, encapsulamento

### Conceito
Igual ao que você já sabe de C++: campos, métodos, construtores, modificadores de acesso.

```csharp
public class Todo
{
    private readonly Guid _id;
    public string Title { get; private set; }
    public bool IsDone { get; private set; }

    public Todo(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title não pode ser vazio.", nameof(title));

        _id = Guid.NewGuid();
        Title = title;
        IsDone = false;
    }

    public void Complete() => IsDone = true;
}
```

### Equivalente C/C++
Praticamente idêntico a uma classe C++, com duas diferenças de vocabulário: `internal`
(visível só dentro do assembly — sem equivalente direto em C++) e ausência de destrutor
determinístico (ver `IDisposable` no [módulo 02](02-csharp-avancado.md)).

### Armadilha comum
Não existe `const` de método (`void Foo() const` em C++). O equivalente é disciplina de
design: exponha só `get` quando o objeto não deve ser mutável de fora.

### Exercício
Parte do exercício do fim do módulo.

---

## Properties (`get; set;`)

### Conceito
Açúcar sintático para um par getter/setter, usado como se fosse um campo público.

### Como funciona
```csharp
public string Title { get; private set; }   // auto-property — o compilador gera o campo oculto

public string Email
{
    get => _email;
    set => _email = value.Trim().ToLowerInvariant();   // property "de verdade", com lógica
}
```

### Equivalente C/C++
Em C++ você escreveria `GetTitle()`/`SetTitle()` manualmente, ou exporia o campo direto.
Properties dão a sintaxe de campo público (`todo.Title`) com o controle de um getter/setter.

### Armadilha comum
`{ get; set; }` público em uma entidade de domínio permite que qualquer código mude o estado
sem passar pelas regras de negócio. No Hub, prefira `{ get; private set; }` e métodos que
representam a intenção (`Complete()`, não `IsDone = true` de fora) — isso é o coração do
[módulo 05](05-modelagem-de-dominio.md).

### Exercício
Parte do exercício do fim do módulo.

---

## Records

### Conceito
Um tipo (`class` ou `struct`) com **igualdade por valor** e imutabilidade "de fábrica" —
ideal para Value Objects e DTOs.

### Como funciona
```csharp
public record Point(int X, int Y);

var a = new Point(1, 2);
var b = new Point(1, 2);
a == b;                        // true — compara VALORES, não referência (diferente de class)

var c = a with { X = 99 };     // cria uma cópia com X alterado — a continua intacto
```

Por padrão `record` é reference type (compila para `class` com igualdade/`ToString`/`with`
gerados). Existe `record struct` para o mesmo comportamento como value type.

### Equivalente C/C++
Não existe equivalente direto — o mais próximo é um `struct` em C com uma função `equals`
escrita à mão comparando campo a campo, mais um "cópia com alteração" manual. `record` gera
tudo isso automaticamente.

### Armadilha comum
Achar que `record` é só uma `class` com sintaxe mais curta — a igualdade por valor é a
diferença que importa, e será a base de **Value Objects** no [módulo 05](05-modelagem-de-dominio.md)
(ex: um `GrantScope` comparado por conteúdo, não por identidade).

### Exercício
Modele `record TodoId(Guid Value)` e use-o no lugar de `Guid` cru no exercício do Todo CLI.

---

## Enums

### Conceito
Um conjunto nomeado de constantes inteiras, com verificação de tipo.

```csharp
public enum Priority { Low, Medium, High }

var p = Priority.High;
```

### Equivalente C/C++
Parecido com `enum` em C, mas **tipado**: `Priority p = 3;` não compila (precisa de cast
explícito) — evita o erro clássico de C de misturar `int` e enum livremente.

### Armadilha comum
Enums em C# **não são um bom modelo para status com regras de transição** (`grant_status`,
`session_status`) — eles não impedem transições inválidas por si só. Isso é resolvido no
[módulo 06](06-maquinas-de-estado.md).

### Exercício
Nenhum isolado.

---

## Pattern matching e switch expressions

### Conceito
Uma forma declarativa de testar "formato" e extrair dados de um valor — vai muito além do
`switch` de C.

### Como funciona
```csharp
string Describe(object value) => value switch
{
    int n when n < 0 => "negativo",
    0 => "zero",
    int => "positivo",
    string s when string.IsNullOrEmpty(s) => "string vazia",
    null => "nulo",
    _ => "outra coisa"
};

// pattern matching em if:
if (todo is { IsDone: true, Title.Length: > 0 } completo)
    Console.WriteLine($"{completo.Title} concluído");
```

### Equivalente C/C++
Sem equivalente direto — o mais próximo seria uma cadeia de `if`/`else if` fazendo checagem de
tipo manual (com RTTI) e cast. Pattern matching faz isso de forma segura e exaustiva (o
compilador avisa se faltar caso, no `switch` *expression*).

### Armadilha comum
Esquecer o caso `_` (default) em um `switch` *expression* é erro de compilação — diferente do
`switch` *statement* de C, que deixa passar silenciosamente.

### Exercício
Parte do exercício do fim do módulo (dispatch de comandos do Todo CLI via pattern matching em
vez de `if`/`else` encadeado).

---

## Exceptions

### Conceito
Modelo praticamente igual ao de C++: `throw`, `try`/`catch`/`finally`, hierarquia de tipos.

### Equivalente C/C++
Mesmo modelo mental de exceptions em C++. Diferença prática: em .NET, exceptions são o
mecanismo padrão até para bibliotecas do framework (ex: `Dictionary` lança se a chave não
existe) — muito mais idiomático usá-las do que costuma ser em C++.

### Armadilha comum
Não use exceptions para controle de fluxo comum (ex: "grant não encontrado" numa consulta
opcional) — são caras em performance e o idiomático é `TryGetValue`/tipos que representam
ausência de valor. Use exceptions para violação de invariante ou erro excepcional de verdade.

### Exercício
Parte do exercício do fim do módulo (validar título vazio lançando `ArgumentException`).

---

## Nullable reference types

### Conceito
Desde C# 8, o compilador rastreia **estaticamente** se uma referência pode ser `null`,
avisando em tempo de compilação.

### Como funciona
Com `<Nullable>enable</Nullable>` no `.csproj` (já ligado no template do módulo 00):

```csharp
string nome = null;           // warning do compilador: nome não deveria ser null
string? talvezNome = null;    // ok — declarado explicitamente como "pode ser null"

void Cumprimentar(string nome)      // contrato: nunca null
{
    Console.WriteLine(nome.Length);    // sem necessidade de checar null aqui
}
```

### Equivalente C/C++
Em C, todo ponteiro pode ser `NULL` e o compilador não ajuda em nada — a checagem é 100%
disciplina do programador (e é a causa clássica de segfault). Nullable reference types trazem
parte da segurança de `std::optional`/`Option<T>` para o tipo de referência comum, **em tempo
de compilação**, sem custo de runtime.

### Armadilha comum
É um aviso do compilador, **não uma garantia em runtime** — dá para forçar com `!` (operador
null-forgiving) e continuar tomando `NullReferenceException`. Use `!` só quando você tem
certeza que o compilador não conseguiu provar sozinho.

### Exercício
Ligue `Nullable` no projeto do Todo CLI (já vem ligado se você usou o template do módulo 00)
e resolva todos os warnings de nulidade antes de seguir.

---

## Exercício do módulo — Todo CLI

Console app com CRUD de tarefas **em memória**, reunindo tudo acima:

- `record TodoId(Guid Value)`
- `class Todo` com `Title` (`{ get; private set; }`), `IsDone`, construtor validando título
  vazio (`throw new ArgumentException`)
- `enum Priority { Low, Medium, High }`
- Armazenamento: `List<Todo>` ou `Dictionary<TodoId, Todo>`
- Loop de comandos lido do `Console.ReadLine()`, despachado com `switch` expression /
  pattern matching (`add`, `list`, `done <id>`, `remove <id>`, `exit`)
- Nenhuma dependência externa — só C# essencial

Critério de pronto: `dotnet run` no projeto, consegue adicionar, listar, completar e remover
tarefas numa sessão interativa, sem exceptions não tratadas.

Próximo módulo: [02 — C# avançado](02-csharp-avancado.md).
