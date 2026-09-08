# Core Domain

Core Domain (ou "subdomínio core") é a parte do negócio que é o **motivo pelo qual a empresa compete e ganha**. É onde está a complexidade que, se resolvida bem, gera vantagem competitiva real — e que, se terceirizada ou mal modelada, tira a razão de existir do negócio.

Os critérios que definem se algo é core:

- **Diferenciação estratégica**: é o que separa você dos concorrentes que fazem algo parecido. Se um concorrente pode copiar essa parte comprando uma solução pronta, provavelmente não é core.
- **Complexidade de negócio (não técnica)**: é onde as regras são mais sutis, mudam mais, e exigem entendimento profundo de especialistas do domínio — não onde o código é mais difícil de escrever tecnicamente.
- **Onde investir o melhor esforço**: Eric Evans recomenda colocar aí os desenvolvedores mais experientes, o modelo mais cuidadoso, e a colaboração mais próxima com especialistas de negócio. É onde vale a pena "sofrer" modelando com calma.

Exemplo: numa empresa de logística, o algoritmo de roteirização de entregas provavelmente é core — é o que define se ela é mais rápida e barata que os concorrentes. Já numa loja online que só usa uma transportadora terceirizada, "logística" nem aparece como subdomínio relevante, ou é genérico.

## **O que ele não é**

- **Não é o "maior" pedaço do sistema.** Às vezes o core domain é uma fração pequena do código total — o resto pode ser CRUD de cadastro, autenticação, relatórios. Tamanho não indica importância estratégica.
- **Não é "a parte tecnicamente mais difícil de construir".** Integrar um gateway de pagamento pode ser um inferno técnico e ainda assim ser genérico — porque não é isso que diferencia sua empresa das outras.
- **Não é fixo pra sempre.** O que é core pode virar genérico com o tempo (ex: autenticação já foi diferencial de segurança, hoje é "usa Auth0 e pronto"), e o contrário também acontece conforme a estratégia da empresa muda.
- **Não é igual pra todo mundo no mesmo ramo.** "Pagamento" pode ser core pra uma fintech e genérico pra uma loja de roupas — depende de onde *aquela* empresa específica compete.
- **Não é sinônimo de Bounded Context ou Shared Kernel.** Core domain é uma classificação do espaço do problema (lembra da nossa conversa anterior?). Pode virar um BC, vários BCs, ou parte de um — a classificação estratégica não decide sozinha a fronteira de código.
- **Não é "o módulo mais usado" ou "a tela principal".** Frequência de uso não é o critério — vantagem competitiva é.
- **Não é ordem de construção obrigatória.** Muita gente confunde "core = constrói primeiro". Na prática, o que a recomendação diz é: dedique mais atenção de modelagem e talento ali — não necessariamente que seja o primeiro código a existir.
