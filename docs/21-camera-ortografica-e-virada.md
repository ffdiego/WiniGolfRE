# Projeção ortográfica e virada entre tacadas

Implementado em 28/10/2026 a pedido do usuário, após aprovação da curva de tacada corrigida. O cliente jogável usa **sempre projeção ortográfica**, tanto na vista inclinada quanto na planta. A opção de perspectiva e o atalho P foram removidos. Laboratório, núcleo de física e dados do executável permanecem inalterados.

## Comportamento

- Ao preparar o primeiro campo, reiniciar, selecionar outra pista ou terminar uma tacada com possibilidade de continuar, o cliente prepara uma nova orientação da vista inclinada.
- A câmera vira **quando a bola já parou e antes da próxima tacada**. Não gira automaticamente durante o movimento da bola. Captura mantém sua animação; a próxima preparação ocorre ao abrir o campo seguinte.
- Sorteia 10 a 90 passos equivalentes de rotação, a 2° por passo, com sentido positivo ou negativo: amplitude de 20° a 180° e duração nominal de 0,20 a 1,80 s.
- O ângulo é interpolado por smoothstep em tempo de apresentação, em vez de saltar em incrementos de 2° por quadro. O enquadramento continua ajustado aos vértices projetados.
- A mira é bloqueada durante a virada. O status informa a preparação da próxima tacada. Ao concluir, o arraste usa a nova orientação da câmera.
- Espaço pausa tanto a reprodução quanto a virada. Q/E e botão do meio continuam oferecendo rotação manual fora da transição automática.
- A planta permanece estável e não impõe espera por uma virada invisível. O próximo ângulo inclinado é atualizado sem animar a planta. Trocar de vista cancela uma transição em andamento.

A força do esquerdo mantém padrão 1× da fórmula corrigida da [etapa 20](20-correcao-da-curva-de-tacada.md). Não se acrescentou multiplicador para compensar a troca de projeção.

## Evidências e decisões

| Aspecto | Base | Confiança |
|---|---|---|
| Projeção original sem divisão por profundidade | Rotação e descarte de profundidade em `0x0041C804` e `0x0041CA78`, documento 06 | Alta no caminho recuperado |
| Preparação aleatória entre turnos | `0x0041F0A0`: contador derivado de `%0x51 + 10`, incremento angular ±2; estado 4 de `0x0041E654` atualiza `+0x81B10`, documentos 11/17 | Alta nas operações estáticas |
| Amplitude e duração escolhidas no cliente | Faixas acima derivadas do mecanismo observado e do passo nominal de 20 ms | Alta na implementação; aproximação explícita do fluxo legado, sem reproduzir possíveis diferenças de contagem/atrasos |
| Smoothstep, PRNG do .NET e planta estável | Decisões modernas de apresentação | Explícitas; não atribuídas ao original |

`TurnPresentation` observa mudanças de campo/tacada e inicia uma única transição por estado pronto. `SceneCamera` sorteia e interpola o ângulo, e monta exclusivamente `CameraProjection.Orthographic`. A aleatoriedade fica nessa camada: não entra em `GameSession`, no impulso já aceito, nos quadros físicos ou nos resultados de pontuação.

Não houve novas mutações ou análise extensa no Ghidra: as referências existentes já sustentavam o mecanismo. Não se afirma reprodução do PRNG original, igualdade do ângulo sorteado ou duração idêntica ao Windows antigo.

## Verificação

**567 verificações aprovadas** no [relatório do cliente](../artifacts/game/checks.json), preservando os testes anteriores de mira, sons, física e renderização. Os novos casos verificam:

- Faixas de duração/amplitude e ambos os sentidos de rotação.
- Independência da divisão dos intervalos de relógio e chegada ao ângulo final.
- Uma virada por estado pronto, sem repetir a cada quadro.
- Ausência de virada automática com bola em movimento ou durante captura.
- Pausa, retomada, preservação exata do estado físico e da contagem.
- Mira bloqueada durante a virada e liberada ao terminar.
- Projeção ortográfica efetiva nos 36 campos e nas duas vistas, com leitura de pixels reais.

Capturas do cliente no [início](../artifacts/game/turn-start.png), [meio](../artifacts/game/turn-middle.png) e [fim](../artifacts/game/turn-end.png) de uma transição. Exemplos atuais: [Basic inclinado](../artifacts/game/01-orthographic.png), [Dip inclinado](../artifacts/game/03-orthographic.png), [Splitt inclinado](../artifacts/game/25-orthographic.png).

Os testes de transição usam seeds fixas para verificação; a janela normal usa `Random.Shared`. As capturas antigas com nome `perspective` preservam a primeira etapa do cliente e não representam mais sua projeção atual.
