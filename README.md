# Coop Launcher

Launcher de coop local para Windows, com BattleBlock Theater e Hollow Knight Family Coop.

## Versão 0.6.4

- O executável inclui o pacote do mod BattleBlock 1.3.0 e a DLL Family Coop 0.2.1. A primeira instalação desses mods funciona sem baixar o arquivo do mod.
- O botão Atualizar continua buscando versões futuras no servidor.
- Detecta o mod BattleBlock instalado mesmo quando o backup antigo não está na pasta; só recupera arquivos quando os hashes originais conferem.
- O botão de atualização do BattleBlock baixa o pacote mesmo quando a versão do pacote já está em cache.
- Detecta o mod Hollow Knight na pasta ativa ou no perfil Family Coop e oferece o arquivo completo verificado por SHA-256.
- Reconhece os builds 23591838 e 25916588 do BattleBlock Theater.
- Corrige a abertura do executável selecionado, inclusive quando o jogo está em outra pasta.
- Atualiza o mod do BattleBlock por um botão próprio. O pacote de patch é verificado por SHA-256 e o executável original é preservado.
- Mantém as atualizações do aplicativo e do Hollow Knight Family Coop.

[Baixar o Coop Launcher](https://nick-chagas.github.io/coop-launcher/). Cada pessoa precisa ter o próprio jogo instalado. Este repositório não distribui o executável do jogo.

## Publicar atualizações

O launcher lê `updates-v3.json` do site. Ao publicar uma nova versão do aplicativo ou do mod, adicione o arquivo com um nome novo em `downloads/`, calcule o SHA-256, atualize a versão, a URL e o hash nos manifestos, e publique o commit. O manifesto legado `updates.json` continua sem oferecer patch do Family Coop ao launcher 0.5.0.

O pacote `BattleBlock-mod-*.xml` descreve patches por hash do executável original. Builds não reconhecidos não são modificados. O código fonte do launcher está em `src/`.

Arte promocional de Hollow Knight: [Team Cherry](https://www.hollowknight.com/). A capa de BattleBlock Theater vem da biblioteca Steam. Projeto independente dos criadores dos jogos.

O Family Coop é uma adaptação do [HollowKnight-Local8](https://github.com/RaulSB-code/HollowKnight-Local8). O arquivo completo requer uma instalação existente com o Modding API; o launcher guarda a DLL anterior com sufixo `.previous`.
