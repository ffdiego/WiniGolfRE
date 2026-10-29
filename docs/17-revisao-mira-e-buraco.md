# Revisão da mira, captura e apresentação

Revisão motivada pela experiência do usuário no laboratório: puxadas pequenas muito fortes, ausência do modo com botão direito e pouca percepção da interação com o buraco. Não se alterou o executável nem o banco Ghidra.

## Diagnóstico e evidências

| Descoberta | Evidência | Confiança |
|---|---|---|
| O laboratório aplicava o modo 2 mesmo ao botão esquerdo, tornando a puxada muito sensível | `CourseView.Mode` inicializado com `Mode2` e seletor iniciando nessa opção, antes desta revisão | Alta; inspeção da implementação |
| O original associa Button ordinal 0 ao modo 1 e ordinal 1 ao modo 2 | Nova leitura de `FormMouseDown`, `0x0041DB20`; testa CL=0/1 e grava respectivamente 1/2 em `+0xC8644` | Alta nos ordinais; identificação esquerdo/direito também corroborada pelo relato do usuário |
| O modo 2 dá oito vezes o impulso do modo 1 para a mesma puxada não saturada | Ganhos 0,8/0,1 no MouseUp `0x0041DF0C`, especificados no documento 13 | Alta na fórmula; não é medição dinâmica |
| A captura já era calculada, mas a bola permanecia desenhada normalmente após `Captured` | `Simulator.Evaluate`, `LabSession.Outcome` e desenho incondicional anterior em `CourseView` | Alta; reproduzido com tacada em Basic |
| O buraco exige proximidade e baixa velocidade, sem atração nessa rotina | `0x0041ECB8`: limites estritos por eixo, constantes em `0x0041F098`=8 e `0x0041F09C`=2; ao capturar grava estado 3 | Alta nesse caminho; não implica um modelo físico de cavidade/esfera |
| Há aleatoriedade na transição da câmera | `0x0041F0A0`, já registrado no documento 11: chamadas a `0x004859A4`, contador derivado de `%0x51 + 10`, incremento angular +2/−2. Despacho `0x0041E654`, estado 4, soma o incremento em `+0x81B10` | Alta no mecanismo estático; frequência percebida e experiência visual não medidas |

Novas leituras preservadas em [revisao-mouse-buraco-camera.json](evidencias/revisao-mouse-buraco-camera.json); o trecho de preparação de turno permanece em [turnos-estado-verificado.json](evidencias/turnos-estado-verificado.json).

O relato de uma “corda maior” no modo forte não deve ser convertido automaticamente em limite de arraste maior. O MouseMove recuperado limita modo 1 a 250 e modo 2 a 50; o modo forte tem ganho maior apesar do limite menor. O MouseDown também passa valores 1/3 à mesma rotina de propriedade de desenho `0x00441DE4`, conforme o modo. A identificação exata dessa propriedade como espessura não foi fechada nesta revisão. O laboratório usa linha mais grossa no modo forte como indicação visual explícita, sem reivindicar equivalência de renderização.

## Correções do laboratório

- Esquerdo inicia puxada de precisão, modo 1. Direito inicia puxada forte, modo 2. Soltar outro botão não dispara a bola. Esc cancela; o direito deixou de ser um atalho de cancelamento.
- Removido o seletor de modo que aplicava força alta ao esquerdo. Mostram-se as instruções dos dois botões e o modo durante a mira. A força não recebeu multiplicadores arbitrários: conserva-se `Aim.FromDrag` do núcleo, incluindo a normalização de escala já documentada.
- Ao chegar ao quadro de captura, a bola deixa de ser desenhada na pista; o alvo ganha um contorno verde e aparece “EMBOCOU!”. Isso é apresentação, sem reposicionar a bola física. Voltar a um quadro anterior ou repetir restaura a visibilidade corretamente.
- O desenho do buraco acompanha a escala e a inclinação. Com superfícies/portais ativados, um contorno pontilhado representa a região quadrada de captura x/y de ±8 unidades. O círculo escuro é uma representação visual; a condição física continua quadrada e estrita.
- Um impulso forte pode atravessar o alvo sem captura enquanto `abs(vx)>=2` ou `abs(vy)>=2`. Não foi acrescentada sucção nem alterado o limiar para disfarçar isso. A captura é testada em passos discretos, conforme o núcleo atual.

## Verificação

**101 verificações da interface aprovadas**, relatório [checks.json](../artifacts/viewer/checks.json). Novos casos exercitam os dois botões, razão 8:1 sem saturação, soltura do botão incorreto e tacada real pelas entradas de mouse em Basic com ambos os perfis: captura, ocultação da bola, retorno a quadro anterior e repetição. A apresentação não modifica a posição/velocidade final retornada pela física.

Captura de tela do resultado: [Basic embocado](../artifacts/viewer/basic-captured.png). Os 92 casos do núcleo permanecem independentes; nenhum arquivo de `Winigolf.Physics` mudou nesta revisão. Isso não substitui a comparação dinâmica das puxadas e tempos com o jogo original.

## Direção gráfica

O núcleo permanece em C# e pode ser usado por um cliente C# com raylib, mantendo o laboratório separado. Os polígonos recuperados já fornecem geometria para uma malha; desenhar essa malha e a bola a cada quadro dispensa reproduzir a baixa frequência visual percebida no original. A física pode continuar com passo fixo de 20 ms e o cliente interpolar a apresentação entre estados, sem alterar a trajetória autoritativa.

A projeção original recuperada é ortográfica com rotações, sem divisão por profundidade no caminho estudado, embora a vista inclinada produza a sensação de perspectiva. Ver documento 06. Uma câmera inclinada giratória, uma câmera realmente perspectiva e uma vista de cima são alternativas de apresentação; o ângulo deve participar da conversão de mira, sem girar a geometria física ou introduzir aleatoriedade no resultado da tacada. Não se implementou migração para raylib nem câmera aleatória nesta correção.
