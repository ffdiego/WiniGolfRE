# Cena na janela inteira e captura com velocidade de entrada

A renderização 3D ocupa toda a área cliente da janela. Não há mais um retângulo reservado entre cabeçalho e placar. Os elementos da interface são cartões sobrepostos com fundo translúcido; o placar tem altura proporcional ao número de jogadores. A roda e os cliques no jogo são bloqueados sobre os cartões e o rodapé. O acompanhamento da bola considera esses elementos para não a esconder atrás do placar.

A resolução lógica da câmera e as coordenadas da mira usam a janela inteira; a textura de renderização mantém a amostragem 2× existente. [Captura da interface](../artifacts/game/full-window.png).

## Animação após a captura

`CaptureMotion.Sample` recebe posição, superfície e velocidade do último estado físico capturado. Converte velocidade de unidades por passo para unidades por segundo usando `Simulator.StepSeconds`. A posição inicial é a posição física de captura, com a altura visual corrigida para o suporte da superfície.

Um modelo visual integrado em passos de até 1/240 s conserva a direção de entrada, amortece o movimento horizontal e aplica gravidade vertical. A geometria de apoio da esfera sobre a borda mantém a bola apoiada até haver espaço para cair; contatos com as paredes e o fundo do copo são inelásticos. O raio permanece 4, as marcas acompanham o deslocamento e a parte abaixo da boca é ocultada pelo recorte existente.

As regras do jogo legado reconhecem captura numa região quadrada, incluindo casos em que uma esfera física ainda estaria sobre a borda. Para preservar todas essas capturas, uma mola visual horizontal conduz a bola ao copo. Portanto, esta é uma aproximação de apresentação com movimento dependente da entrada, não uma simulação completa de contato esfera/borda. Uma física integral de putt, escape e balanço na borda exigiria mudar o critério de captura e poderia mudar resultados de tacadas.

O modelo não escreve no estado físico, não modifica pontuação e não usa aleatoriedade. Amostrar novamente o mesmo instante produz a mesma posição. A pausa preserva o instante. A troca de jogador aguarda 0,8 s após a captura, respeitando a queda.

Capturas: [entrada](../artifacts/game/capture-entry.png), [passagem pela borda](../artifacts/game/capture-lip.png), [queda](../artifacts/game/capture-fall.png) e [ocultação](../artifacts/game/capture-hidden.png).

## Validação

Build da solução Release sem avisos nem erros. **1628 verificações aprovadas** no [relatório](../artifacts/game/full-window-checks.json), incluindo:

- Continuidade da posição inicial e influência da direção e velocidade de chegada.
- 81 combinações de posição e velocidade nos limites da captura quadrada: sem salto horizontal inicial, resultados finitos e bola oculta antes da troca de jogador.
- Amostragem reproduzível e preservação do estado de origem.
- Resolução da câmera igual à janela e bloqueio de entrada sobre a interface.
- Regressões existentes de física, câmera, áudio, captura e partida local.

As capturas da cena e dos estágios da animação foram inspecionadas. Os parâmetros de gravidade, amortecimento e centralização são escolhas visuais em unidades do jogo, sem calibração contra uma bola real.
