# C# / .NET Core — Apostila para o Hub de Apps

Apostila pessoal de transição **C/C++ (42) → C# / .NET Core**, construída para te dar
confiança para implementar o back-end do **Hub de Apps** — o projeto que substitui o
ft_transcendence.

Não é um curso de C# do zero. Você já domina o difícil: memória e lifetime, stack/heap,
ponteiros e referências, OO, SOLID, design patterns, concorrência, parsing, sockets/HTTP,
build systems. Esta apostila **traduz** o que você já sabe para o idioma C#/.NET e aprofunda
só onde o projeto exige.

## Filosofia

- **Migração, não introdução.** Cada conceito novo é ancorado num equivalente que você já
  conhece de C/C++ (ver o mapa completo em [00](00-modelo-mental-e-setup.md)).
- **Profundidade equilibrada.** A espinha do texto é o suficiente para construir o Hub de
  Apps com confiança. Curiosidades sobre internals (GC, JIT, máquina de estados do
  `async`/`await`, Kestrel) ficam em caixas `> 🔬`, sempre opcionais — leia quando bater a
  curiosidade, não por obrigação. Seu maior risco não é aprender pouco, é aprender demais
  antes de começar a construir.
- **Ciclo iterativo.** `estuda → implementa → encontra uma dúvida → volta pra apostila →
  atualiza → implementa de novo.` A apostila é um documento vivo — edite-a quando um módulo
  ficar defasado do que você realmente entendeu implementando.
- **Exercícios progressivos.** Os primeiros módulos usam exercícios neutros e rápidos (Todo
  CLI) só para fixar mecânica de linguagem. A partir do módulo 05, todo exercício é uma fatia
  real do Hub de Apps, e os exercícios **acumulam** — no final você tem o back-end rodando.

## Como cada módulo é organizado

Por tópico, dentro de cada módulo:

| Campo | O que é |
|---|---|
| Conceito | o que é, em 1 frase |
| Como funciona | o mecanismo real — "o que acontece por baixo" |
| Equivalente C/C++ | a ponte com o que você já sabe |
| Exemplo mínimo | o menor C# que demonstra o conceito |
| Armadilha comum | onde quem vem de C/C++ costuma errar |
| 🔬 Pergunta profunda | opcional — internals, só se bater curiosidade |
| Exercício | neutro (mecânica) ou âncora (Hub de Apps) |

## Índice

### Fundação — C# e a plataforma (rápido; você troca a "roupa", não aprende a programar de novo)
- [x] [00 — Modelo mental e setup](00-modelo-mental-e-setup.md)
- [x] [01 — C# essencial](01-csharp-essencial.md)
- [x] [02 — C# avançado](02-csharp-avancado.md)

### Runtime & plataforma
- [x] [03 — Runtime .NET](03-runtime-dotnet.md) (CLR, GC, async por baixo dos panos)
- [x] [04 — DI, Configuration e Logging](04-di-config-logging.md)

### Domínio do Hub em C# puro
- [x] [05 — Modelagem de domínio](05-modelagem-de-dominio.md)
- [x] [06 — Máquinas de estado](06-maquinas-de-estado.md)

### Persistência
- [x] [07 — EF Core multi-tenant](07-ef-core-multitenant.md)

### Camada web
- [x] [08 — ASP.NET Core fundamentos](08-aspnet-core-fundamentos.md)
- [x] [09 — Fronteiras de API](09-fronteiras-de-api.md)

### Autorização, concorrência, segurança
- [x] [10 — Autorização por política](10-autorizacao-por-politica.md)
- [x] [11 — Concorrência e workers](11-concorrencia-e-workers.md)
- [x] [12 — Segurança, auditoria e mensageria](12-seguranca-auditoria-mensageria.md)

### Testes, arquitetura e deploy
- [x] [13 — Testes, arquitetura e deploy](13-testes-arquitetura-deploy.md)

Todos os 14 módulos estão escritos. O trabalho que resta é seu: **estuda → implementa →
dúvida → volta pra apostila → atualiza**. Edite qualquer módulo livremente conforme for
implementando — a apostila é sua, não um material fechado.

## Seus 7 tópicos → onde cada um é coberto

| Tópico | Prioridade | Módulo(s) |
|---|---|---|
| 1. Modelagem de domínio (DDD) | 🟢 fundação | [05](05-modelagem-de-dominio.md) |
| 2. Máquinas de estado | 🟢 fundação | [06](06-maquinas-de-estado.md) |
| 3. EF Core multi-tenant | 🟢 fundação | [07](07-ef-core-multitenant.md) |
| 4. Autorização por política | 🟡 antes do 1º release | [10](10-autorizacao-por-politica.md) |
| 5. Concorrência, async, workers | 🟡 antes do 1º release | [11](11-concorrencia-e-workers.md) |
| 6. Segurança, auditoria, mensageria | 🟡 antes do 1º release | [12](12-seguranca-auditoria-mensageria.md) |
| 7. Fronteiras de API e testes | 🔵 contínuo | [09](09-fronteiras-de-api.md) + [13](13-testes-arquitetura-deploy.md) |

## O projeto âncora

O **Hub de Apps** é um sistema multi-tenant: workspaces contratam um Hub, instalam Apps,
concedem **grants** de acesso que abrem **sessions**, tudo isso com RBAC por acúmulo de
roles, impersonation auditada de forma imutável e proteção contra escrita concorrente no
mesmo agregado. O schema de referência completo está no [módulo 00](00-modelo-mental-e-setup.md).

## Pré-requisitos (o que você já traz da 42)

Memória e lifetime · stack/heap · ponteiros e referências · gerenciamento de recursos ·
abstração · OO · herança/polimorfismo · interfaces · SOLID · design patterns · concorrência ·
processos · parsing · estruturas de dados e algoritmos · debugging · build systems ·
separação de responsabilidades · sockets e HTTP (Webserver em C++).

Esta apostila assume tudo isso como dado — se um módulo estiver explicando algo dessa lista,
é bug, me avise (ou edite direto).
