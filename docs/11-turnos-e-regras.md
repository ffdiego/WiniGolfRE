# Turnos, conclusão de tacada e pontuação

2026-10-28. Análise estática incremental; nenhuma alteração no banco. As funções principais já estavam preservadas em [reconhecimento-ghidra.json](evidencias/reconhecimento-ghidra.json). Conferências adicionais em [turnos-estado-verificado.json](evidencias/turnos-estado-verificado.json).

## Máquina de estados observada

`0x0041E654` despacha pelo byte objeto+`0xC8674`:

| Estado | Comportamento confirmado | Confiança |
| ---: | --- | --- |
| 0 | Atualização de animação/demonstração por `0x00420430`, conforme flag | Alta no caminho; média no nome humano |
| 1 | Aceita MouseDown para preparar tacada; não integra a bola no despacho | Alta |
| 2 | Chama `0x0041ECB8`: física, apresentação e avaliação de parada/captura | Alta |
| 3 | Animação de conclusão; ao contador chegar a 18 marca jogador/campo e chama preparação de turno | Alta no fluxo; média/alta na interpretação visual |
| 4 | Transição de câmera para próximo jogador; quando contador ultrapassa limiar, redesenha e grava estado 1 | Alta |
| 5 | Atualiza interface de encerramento e volta ao estado 0 | Alta no fluxo; média no comportamento completo do formulário |

Captura do alvo leva 2→3. Bola parada em superfície plana chama `0x0041F0A0`, que escolhe jogador e pode levar a 4 ou avançar campo. A transição de câmera usa um número aleatório de passos e sentido de rotação, mas isso pertence à apresentação; não deve ditar a duração da simulação física de uma tacada no servidor.

## Estado por jogador

`0x0041F0A0` percorre jogadores 1 a 8 e mantém uma fila. Há caminhos de ordem sequencial, aleatória e por comparação de valores de pontuação. Os nomes exatos das opções da interface ainda precisam de confirmação.

O estado físico é salvo/restaurado com stride de **0x50 bytes por jogador**. Campos observados, usando ID j:

- Posição: `+0xC82B0/+0xC82B4/+0xC82B8 + j*0x50`.
- Posição anterior: `+0xC82BC/+0xC82C0/+0xC82C4 + j*0x50`.
- Velocidade: `+0xC82C8/+0xC82CC/+0xC82D0 + j*0x50`.
- Superfície: `+0xC82FC + j*0x50`.

O jogador anterior vem de `+0xC9328`; o selecionado, de `+0xC9324`. **Confiança alta**, conferida no assembly de `0x0041F0A0`.

Uma discrepância importante da evidência antiga: o pseudocódigo salvo mostra em um ponto a restauração de y a partir de `+0xC82D0`, mas as instruções `0x0041FA54/0x0041FA5B` leem **+0xC82B4** e gravam **+0xC8220**, como esperado. Portanto, não registrar essa aparente troca de y/vz como bug do jogo. O assembly prevalece sobre o texto decompilado; esse caso reforça a necessidade de conferir campos críticos.

Cada jogador também possui um registro de pontuação com stride de **0x168 bytes**. Os offsets de contagem por campo usam `+0xC8680 + jogador*0x168 + campo*4`; total de tacadas em `+0xC8714 + jogador*0x168`. Totais relativos ao par são calculados a partir das contagens e da tabela de par exportada.

## O que encerra a participação no campo

No caminho de captura, `0x0041ECB8` grava estado 3 após testar proximidade x/y e limites por componente de velocidade. No despacho do estado 3, quando o contador chega a 18, grava status 2 no jogador, marca o campo como concluído e chama `0x00404498` e `0x0041F0A0`.

`0x00404498` atualiza tabelas de recordes: compara tacadas com até dez posições por campo, move nomes/valores e calcula totais para conjuntos de 6, 12, 18 e 36 campos quando as flags indicam conclusão. O ramo individual exige contagem menor que 9. Ao fim chama `0x0040341C`; o destino final dessa gravação ainda não foi consolidado nesta etapa. **Confiança alta no papel geral de recordes**, sem transcrever todos os casos de desempate a partir do pseudocódigo. Evidência em [recordes-00404498.json](evidencias/recordes-00404498.json).

No caminho de parada plana, se a contagem do jogador no campo é **exatamente 7**, `0x0041ECB8` substitui a contagem por **9**, acrescenta **2** ao total, marca conclusão e chama as mesmas rotinas antes de preparar turno. Isso indica limite de tentativas com penalidade. **Confiança alta nas escritas, média/alta na regra humana**: a condição está no caminho de bola parada, não em todo instante da sétima tacada. Uma captura pode acontecer antes desse caminho; não resumir como proibição de executar a sétima tacada.

Os valores de conclusão são usados por `0x0041F0A0` para retirar jogadores da fila. Quando não há jogador elegível, chama `0x0041FB40` para selecionar o próximo campo.

## Progressão da partida

`0x0041FB40` oferece ramos para conjuntos de 6, 12, 18 ou 36 campos e seleção personalizada. Há seleção sequencial, aleatória sem repetir concluídos e reversa. Para cada jogador ativo, inicializa a bola nas coordenadas e superfície da tabela do campo selecionado. O índice atual é `+0xC932C` e a flag de campo concluído está na região `+0xCB5D0`.

Isso descreve o controle de turnos local, não uma definição de modo multiplayer online. Para um primeiro port semelhante ao original, alternância de tacadas sobre estados individuais é a hipótese de implementação mais diretamente sustentada. Colisões entre bolas de jogadores e jogadas simultâneas não foram demonstradas nesta análise.

## Início e interrupção

Os endereços originais `StartClick=0x004213CC` e `StopClick=0x004215F4` continuam sem funções definidas no banco, portanto a decompilação foi recusada. A leitura de bytes mostra em Start reset de contagens/flags e chamada à preparação de campos; em Stop, confirmação de interface e retorno ao estado 0 em um ramo. **Confiança média nesses resumos**, sem transcrição completa dos handlers. Nenhuma definição de função foi criada para contornar a limitação.

Pendências focadas: verificar integralmente MouseUp/Start/Stop no assembly, persistência dos recordes, nomes das opções de ordem e o estado de conclusão retornado para cada regra. A especificação online deve usar esses eventos sem depender das animações ou do tempo de ociosidade da VCL.
