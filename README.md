# Coop Launcher

Site de download do Coop Launcher para Windows. O aplicativo organiza jogos com coop local e confere novas versões em `updates.json`.

## Versão 0.5.1

- Biblioteca com cartões equilibrados para Hollow Knight e BattleBlock Theater.
- Family Coop e jogo original como ações do Hollow Knight.
- Atalho para adicionar skins e verificação de atualizações.

O download da release inclui apenas o launcher. Cada pessoa precisa ter os jogos instalados. O Family Coop está em beta. A release distribui apenas um patch para quem já tem a versão anterior instalada; não inclui o mod completo.

## Publicar uma atualização

1. Publique `CoopLauncher-X.Y.Z.exe` em uma release `vX.Y.Z`.
2. Atualize a versão, o URL e o SHA-256 em `updates.json`.
3. Publique a mudança no site. O launcher verifica esse manifesto ao abrir.

Arte promocional de Hollow Knight: [Team Cherry](https://www.hollowknight.com/). A capa de BattleBlock Theater vem da biblioteca Steam. Este projeto é independente dos criadores dos jogos.
