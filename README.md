# PONG em rede local

Projeto feito para Unity **6000.5.5f1**.

## Como jogar em dois computadores

1. Os dois computadores precisam estar na mesma rede Wi-Fi ou cabeada.
2. Abra o jogo no primeiro computador e clique em **CRIAR SERVIDOR**.
3. O jogo mostrará o IPv4 do servidor.
4. Abra o jogo no segundo computador, digite esse IPv4 e clique em
   **ENTRAR COMO CLIENTE**.
5. A partida começa automaticamente quando os dois jogadores estão conectados.

A tela de conexão fica dentro do `Canvas`, no objeto `NetworkMenu`. Os botões,
textos, campo de IP, cores e tamanhos podem ser alterados normalmente pela
Hierarchy e pelo Inspector da Unity.

Controles:

- Servidor / Jogador 1: **W** e **S**.
- Cliente / Jogador 2: **setas para cima e para baixo**.

O firewall do computador servidor precisa permitir o executável do jogo em redes
privadas. A comunicação usa a porta **UDP 7777**.

## Teste no mesmo computador

Execute uma instância pelo Editor e outra por uma build. Crie o servidor em uma
delas e use o IP `127.0.0.1` na outra.

Também é possível iniciar por linha de comando:

- Servidor: `triopong.exe -host`
- Cliente: `triopong.exe -serverIp=192.168.0.10`
