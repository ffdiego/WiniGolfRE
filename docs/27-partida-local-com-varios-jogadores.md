# Partida local com vários jogadores

Ao abrir o cliente aparece o cadastro de 1 a 6 jogadores. Cada nome aceita até 18 caracteres da faixa de glifos da interface (inclui acentos portugueses). Clique no campo para substituir o nome, use Tab para alternar campos, Backspace para apagar e Enter ou o botão para começar. Cores distintas são atribuídas pela posição na lista. N abre o cadastro para começar outra partida quando a bola não está em movimento; confirmar descarta os pontos da partida anterior.

Cada jogador tem uma `GameSession` própria. `LocalMatch` coordena a rotação de turnos: uma tacada por vez, espera o repouso por 0,5 s ou a captura por 0,8 s e passa ao próximo jogador que ainda não concluiu. Quem concluiu é ignorado nos próximos turnos. Com um jogador, o comportamento de tacadas sucessivas é preservado. Pausar também impede a troca de vez.

As bolas inativas permanecem nas posições salvas e são desenhadas com transparência, sem escrita no depth buffer, ordenadas da mais distante para a mais próxima. A bola ativa é opaca, tem a cor do jogador e conserva as marcas de giro. Bolas capturadas ficam ocultas. Não há colisão entre jogadores: cada simulação usa apenas a pista, mantendo o contrato do núcleo físico existente.

O painel destaca o jogador ativo e mostra tacadas na pista e total acumulado de cada participante. O total inclui as tacadas em andamento, sem duplicar resultados já registrados. Enter avança quando todos concluem. O seletor de pistas muda a pista para todos; voltar/reiniciar uma pista descarta sua pontuação anterior, preservando os resultados das outras. O atalho F2 de depuração altera o perfil e limpa os pontos de todos para evitar mistura de perfis.

## Código e validação

- `LocalMatch.cs`: jogadores, cores, pontuação e alternância.
- `GameApp.Players.cs`: cadastro e placar.
- `CourseScene.cs` / `BallVisual.cs`: bolas transparentes e cor da bola ativa.
- `LocalMatchChecks.cs`: nomes, cores, alternância, posições independentes, captura, pausa, conclusão e pontuação.

Build Release sem avisos ou erros; **1459 verificações aprovadas**, incluindo contexto gráfico real. [Relatório](../artifacts/game/multiplayer-checks.json). Capturas inspecionadas: [cadastro com seis jogadores na janela mínima](../artifacts/game/player-setup.png) e [partida com bolas transparentes e placar](../artifacts/game/multiplayer.png). A validação da entrada de nomes foi feita pela implementação e inspeção da tela; a bateria não automatiza a digitação pelo sistema operacional.

O protótipo é local, sem persistência entre execuções ou comunicação em rede.
