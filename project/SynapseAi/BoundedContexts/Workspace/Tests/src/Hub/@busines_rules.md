- [x] Criar um Hub para um Workspace funciona.
- [x] Instalar um App novo funciona.
- [x] Instalar o mesmo App duas vezes (enquanto a primeira instalação está ativa) lança
	`DomainException`.
- [x] Tentar construir um `Hub`/`Installation` fora dos métodos de fábrica não compila (isso é
	verificado só lendo o código — não tem como escrever um teste que "falha ao compilar",
	mas confirme manualmente que não existe outro construtor público acessível).
