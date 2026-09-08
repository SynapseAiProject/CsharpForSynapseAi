# 03 — Runtime .NET

Essa parte rende mais para você do que para quem está começando: você já vai naturalmente
perguntar "onde está a memória, quem é dono dela, e quem agenda essa execução?". Este módulo
responde isso.

## CLR, assemblies, IL e JIT

### Conceito
O CLR (Common Language Runtime) é a máquina virtual do .NET. Código C# não compila direto
para código de máquina — compila para **IL** (Intermediate Language), empacotado num
**assembly** (`.dll`/`.exe`), que o **JIT** (Just-In-Time compiler) traduz para código de
máquina nativo **na hora de rodar**.

```text
código C#
  ↓ (compilador Roslyn, em "dotnet build")
IL (dentro do assembly .dll)
  ↓ (JIT, em tempo de execução)
código de máquina nativo
```

### Equivalente C/C++
Seu fluxo de C era `código → objeto (.o) → linker → executável nativo`, tudo antes de rodar.
Em .NET, o "linker" final acontece **em runtime**: o assembly carrega outros assemblies
(bibliotecas) dinamicamente e o JIT compila cada método na primeira vez que é chamado (com
cache do resultado).

### Armadilha comum
Achar que .NET é "interpretado" como um script — não é. Depois do JIT compilar um método, ele
roda como código de máquina nativo, igual ao seu C compilado — só que a compilação final
acontece um pouco mais tarde (e há AOT — Ahead-Of-Time — como alternativa para cenários que
não podem pagar esse custo de startup, fora do escopo do Hub).

> 🔬 **Aprofundamento opcional** — Tiered compilation (JIT rápido e pouco otimizado primeiro,
> recompilação otimizada depois para código "quente"), ReadyToRun, e Native AOT são temas de
> performance avançada — não necessários para começar o Hub.

### Exercício
Sem exercício isolado — contexto para o resto do módulo.

---

## Managed vs unmanaged memory

### Conceito
**Managed memory** é alocada e liberada pelo runtime (GC). **Unmanaged memory** é qualquer
recurso fora do alcance do GC — handles de arquivo, sockets, conexões de banco, memória
alocada via interop nativo.

### Equivalente C/C++
Managed ≈ "o runtime faz o `free` por você, eventualmente". Unmanaged ≈ exatamente como em C —
alguém precisa liberar explicitamente (`Dispose()`, ver [02](02-csharp-avancado.md)).

### Armadilha comum
`DbContext`, `HttpClient`, `FileStream` seguram recursos unmanaged por baixo — sempre exigem
`using`/`Dispose()`, o GC sozinho não resolve isso no tempo certo.

---

## O Garbage Collector

### Conceito
Um coletor **geracional** que libera automaticamente objetos managed que não têm mais
referências alcançáveis.

### Como funciona
- Objetos novos nascem na **Gen 0**. Coletas de Gen 0 são frequentes e baratas.
- Um objeto que sobrevive a uma coleta é promovido para **Gen 1**, depois **Gen 2**
  (long-lived — ex: caches, singletons).
- O algoritmo básico é *mark and sweep*: o GC parte das **raízes** (variáveis locais na
  stack, campos static, registradores) e marca tudo que é alcançável; o resto é lixo.
- Coleta de Gen 0/1 costuma ser *compactante* (move objetos vivos, atualiza referências) —
  diferente de `malloc`/`free`, que deixa buracos (fragmentação) no heap.

### Equivalente C/C++
Não existe `free()` — você nunca libera um objeto managed manualmente. O trade-off: você
ganha ausência de use-after-free e double-free (para memória managed), perde controle fino
sobre *quando* a memória é liberada (não é determinístico — daí `IDisposable` para recursos
que precisam de timing preciso).

### Armadilha comum
Vindo de C, é natural desconfiar do GC e tentar "ajudar" com `GC.Collect()` manual — **não
faça isso** em código de aplicação. Isso força uma coleta completa fora de hora e geralmente
piora a performance. Confie no GC; otimize alocação (menos objetos por request) só se um
profiler mostrar que é o gargalo real.

> 🔬 **Aprofundamento opcional** — Server GC vs Workstation GC, Large Object Heap (objetos
> ≥ 85KB não vão para Gen 0), e `Span<T>`/`stackalloc` para alocação sem heap em hot paths são
> temas de performance avançada, não necessários para o Hub inicialmente.

### Exercício
Sem exercício isolado.

---

## Value types e reference types, revisitado

### Conceito
Retomando o [módulo 01](01-csharp-essencial.md), agora com o quadro completo de memória.

### Como funciona
- Um value type **local** (variável de método) vive na stack — some quando o método retorna,
  sem intervenção do GC.
- Um value type **campo de uma class** vive *dentro* do objeto no heap (inline, sem alocação
  própria) — é liberado junto com o objeto que o contém.
- **Boxing**: quando um value type precisa ser tratado como `object` (ex: guardado numa
  `ArrayList` não-genérica, ou passado para um parâmetro `object`), o runtime aloca uma cópia
  dele no heap e a embrulha numa "caixa". **Unboxing** é o processo inverso.

```csharp
int x = 42;          // value type, provavelmente na stack
object boxed = x;     // BOXING — aloca uma cópia de x no heap
int y = (int)boxed;   // UNBOXING — copia o valor de volta
```

### Equivalente C/C++
Boxing não tem equivalente direto em C — é como se o runtime automaticamente fizesse um
`malloc(sizeof(int))` e copiasse o valor para lá, te devolvendo um "ponteiro" opaco (`object`).

### Armadilha comum
Genéricos evitam boxing (por isso `List<int>` é muito mais eficiente que uma coleção não
genérica de `object`) — uma das razões pelas quais generics existem no .NET, além de type
safety. Prefira sempre APIs genéricas modernas; se você vir boxing acontecendo num hot path
(ex: um `int` sendo passado como `object` num log), é sinal de código antigo ou mal otimizado.

### Exercício
Sem exercício isolado.

---

## Task e o ThreadPool

### Conceito
Uma `Task` representa uma operação assíncrona — **não é uma thread**. A maioria das `Task`s
de I/O (chamadas de rede, banco de dados) não ocupa thread nenhuma enquanto está "esperando".

### Como funciona
Existem dois tipos de trabalho assíncrono:
- **I/O-bound** (rede, disco, banco): o SO notifica o runtime quando a operação termina
  (via completion ports/epoll por baixo) — nenhuma thread fica bloqueada esperando.
- **CPU-bound** (processamento pesado): `Task.Run(...)` agenda o trabalho no **ThreadPool**,
  um conjunto de threads reutilizadas geridas pelo runtime (custo de criar thread é alto —
  o pool evita recriar a cada operação).

```csharp
// I/O-bound: nenhuma thread fica "presa" esperando a rede
var body = await httpClient.GetStringAsync(url);

// CPU-bound: aqui sim vale ocupar uma thread do pool
var result = await Task.Run(() => CalculoPesado());
```

### Equivalente C/C++
Nada como `pthread_create` por chamada. O ThreadPool é comparável a um pool de threads que
você mesmo teria que implementar em C para não pagar o custo de criar/destruir threads o
tempo todo — só que já pronto, e integrado à sintaxe `async`/`await`.

### Armadilha comum
`Task.Run` para trabalho I/O-bound é desperdício — você ocupa uma thread do pool só para ela
ficar esperando a rede, quando `await httpClient...` já faz isso sem gastar thread nenhuma.
`Task.Run` é para trabalho de CPU de verdade.

### Exercício
Neutro: escreva um método que faz N requisições HTTP em paralelo a uma API pública usando
`HttpClient` e `Task.WhenAll`, limitando a concorrência com `SemaphoreSlim` (ex: no máximo 5
por vez).

---

## A máquina de estados do `async`/`await`

> 🔬 **Aprofundamento opcional** — isto é o "o que realmente acontece" por trás do
> `async`/`await`. Não é necessário decorar para usar a sintaxe corretamente, mas é o tipo de
> pergunta que vale a pena responder uma vez, com calma.

### Conceito
O compilador transforma um método `async` numa **máquina de estados**: uma classe gerada que
guarda onde a execução parou e retoma dali quando o `await` termina.

### Como funciona
```csharp
public async Task<string> BuscarAsync(string url)
{
    using var client = new HttpClient();
    var body = await client.GetStringAsync(url);
    return body.ToUpper();
}
```

Isso vira, conceitualmente, algo como:

```text
estado 0: chama GetStringAsync(url)
          se já terminou → vai pro estado 1 direto
          se não → registra um callback (continuation) pro estado 1 e DEVOLVE a Task
                   pro chamador — a thread atual fica LIVRE pra fazer outra coisa

estado 1: recebe 'body', executa body.ToUpper(), completa a Task com o resultado
```

O `await` **não bloqueia a thread**. Ele suspende a execução do método, registra "quando essa
operação terminar, retome daqui", e devolve controle para quem chamou. Quando a operação
original termina (a resposta HTTP chega), o runtime agenda a continuação — possivelmente numa
thread diferente do pool.

### Equivalente C/C++
Comparável a implementar manualmente um loop de eventos com callbacks e estado explícito
salvo à mão (o que você fez no Webserver em C++ com `epoll`/`select`) — só que o compilador
gera essa máquina de estados para você, e você escreve código que *parece* síncrono e
sequencial.

### Armadilha comum
`await` devolve a **thread**, não "pausa o tempo". Código depois do `await` pode rodar numa
thread diferente da que chamou — por isso não se deve depender de estado de thread local
(como afinidade de thread) através de um `await`.

### CancellationToken

### Conceito
O mecanismo padrão para **pedir** o cancelamento cooperativo de uma operação assíncrona — não
força parada, propaga um sinal que o código verifica.

```csharp
public async Task ProcessarAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await Task.Delay(1000, ct);   // lança OperationCanceledException se cancelado
        // ...
    }
}
```

### Equivalente C/C++
Parecido com checar uma flag `volatile sig_atomic_t stop` num loop, ou tratar um sinal — mas
padronizado e integrado a toda a BCL (`HttpClient`, EF Core, `Task.Delay` etc. já aceitam um
`CancellationToken`).

### Exercício
No exercício de requisições paralelas acima, adicione um `CancellationTokenSource` com timeout
(`CancelAfter(TimeSpan.FromSeconds(5))`) e propague o `CancellationToken` até o `HttpClient`.

---

## Exercício do módulo — Todo CLI + SQLite

Evolua o Todo CLI: troque o armazenamento em memória por **SQLite** (`Microsoft.Data.Sqlite`,
sem EF Core ainda — isso fica para o [módulo 07](07-ef-core-multitenant.md)):

1. Todas as operações de banco (`Add`, `List`, `Complete`, `Remove`) devem ser `async Task` /
   `async Task<T>`, usando os métodos assíncronos do driver ADO.NET.
2. O loop de comandos principal deve `await` essas chamadas — sem `.Result`/`.Wait()` em
   lugar nenhum.
3. Adicione um `CancellationToken` que cancela uma operação longa se o usuário digitar `cancel`
   durante a execução (pode ser simulado com um delay artificial numa operação).

Critério de pronto: o Todo CLI persiste em SQLite entre execuções, todo I/O é assíncrono de
ponta a ponta, e você consegue explicar por que nenhuma thread fica bloqueada esperando o
disco durante uma consulta.

Próximo módulo: **04 — DI, Configuration e Logging**.
