# Mira e coordenadas: especificação estática

**Errata incorporada:** a normalização final da mira foi corrigida na [etapa 20](20-correcao-da-curva-de-tacada.md). As divisões anteriormente descritas eram multiplicações por sqrt(L) e 0,1. A interpretação independente dos bytes sustenta a fórmula agora mostrada abaixo.

Nota de consolidação: a identificação de asin/atan e a ordem completa das colisões foram fechadas posteriormente em [13-especificacao-fisica.md](13-especificacao-fisica.md). As pendências abaixo refletem esta etapa histórica.

2026-10-28. Este documento aprofunda a transformação antes deixada pendente em [03-eventos-e-tacada.md](03-eventos-e-tacada.md). Sem alterações no Ghidra ou executável. Fontes: bytes dos handlers em [eventos-fisica-ghidra.json](evidencias/eventos-fisica-ghidra.json), assembly da projeção em [campo-basic-ghidra.json](evidencias/campo-basic-ghidra.json) e [mira-projecao-memoria.json](evidencias/mira-projecao-memoria.json).

## Identificação das funções trigonométricas

`0x00485398` chama a entrada `0x004854B0`, que seleciona CL=2 no núcleo compartilhado. `0x00486A14` chama `0x004854AC`, que seleciona CL=0. No tratamento de argumento zero, CL=2 devolve FLD1, enquanto a outra entrada conserva zero: são respectivamente **cosseno e seno**. A terceira entrada CL=4, usada por `0x00486A94`, participa do caminho de tangente. A identificação de cos/sin tem **confiança alta**, apoiada também pelo padrão completo da rotação da projeção.

Os ângulos são convertidos de graus por produto de pi e 1/180. Exemplo de constantes: double pi em `0x0041CA64`, extended 1/180 em `0x0041CA6C`. Não interpretar os campos de ângulo como radianos armazenados.

## Projeção da bola

Sejam a=`objeto+0x81B10`, b=`+0x81B14`, c=`+0x81B18`, em graus. `0x0041C804` aplica estas etapas na ordem, com seno/cosseno avaliados após conversão para radianos:

```text
X1 = cos(a)*x - sin(a)*y
Y1 = sin(a)*x + cos(a)*y

Z2 = cos(b)*z - sin(b)*X1
X2 = sin(b)*z + cos(b)*X1

Y3 = cos(c)*Y1 - sin(c)*Z2
Z3 = sin(c)*Y1 + cos(c)*Z2
```

Depois `0x0041CA78` calcula:

```text
telaX = origemX + X2
telaY = origemY - Y3
```

O sinal negativo foi confirmado pela constante float **-1** em `0x0041CE3C`. Há arredondamentos e offsets de sprite em outras etapas, não incluídos aqui. É uma projeção por rotações e descarte de profundidade, sem divisão perspectiva nesse caminho. **Confiança alta nas fórmulas**, conferidas nas instruções de `0x0041C804` e no código de `0x0041CA78`.

Consequência: no campo e vista sem rotação a/b, x positivo projeta à direita e y positivo projeta para cima. A planta de Basic usa essa convenção matemática; ela não reproduz a rotação visual da partida. `0x0041F0A0` grava c=-60 graus durante preparação de turno; os demais ângulos e eventuais alterações de vista ainda não foram rastreados integralmente.

## Arraste para impulso

No MouseUp (`0x0041DF0C`), considerar:

```text
dx = pontaTelaX - ancoraTelaX
dy = pontaTelaY - ancoraTelaY   // positivo para baixo na tela
L = sqrt(dx*dx + dy*dy)
k = modo 1 ? 0.1 : 0.8         // modos efetivamente ativados pelo MouseDown

u = k*dx
w = k*dy / cos(c)

vxBruto = -cos(a)*u + sin(a)*w
vyBruto =  sin(a)*u + cos(a)*w

vx = vxBruto * sqrt(L) * 0.1
vy = vyBruto * sqrt(L) * 0.1
```

**Confiança média/alta:** transcrição manual dos bytes x87 nos handlers, agora relacionando as chamadas a cos/sin. A operação FSUBR foi levada em conta; não se deve trocar a ordem de subtração como se fosse FSUB. A fórmula usa o comprimento em pixels antes da compensação de c. Ela não inclui b, embora a projeção visual inclua b. Não afirmar que é uma inversa geral da câmera em todas as orientações.

O MouseMove limita L a 250 no modo 1 e 50 no modo 2, reconstruindo a ponta limitada e arredondando para coordenadas inteiras. MouseUp usa os pontos guardados no objeto; o parâmetro da posição de soltura não é a origem do vetor no trecho de cálculo. Assim, arredondamentos e a frequência de eventos podem afetar o último pixel do arraste.

Para a=0 e c=-60, sem saturação e com L>0:

```text
vx = -k*dx * 0.1*sqrt(L)
vy =  2*k*dy * 0.1*sqrt(L)
```

Um arraste para a esquerda produz vx positivo; um arraste para baixo produz vy positivo, que projeta para cima nessa vista. Isso corresponde a puxar a linha no sentido oposto ao lançamento. Um arraste horizontal de 100 pixels para a esquerda no modo 1 resulta em vx≈10 unidades/passo. No modo 2, 50 pixels horizontais resultam em módulo ≈28,2843. Para uma direção fixa a curva é L^1,5. São exemplos matemáticos da fórmula corrigida, não medições de jogabilidade.

Pontos ainda pendentes antes de um port definitivo:

- L=0 e cos(c) próximo de zero não têm proteção explícita na sequência transcrita; a reconstrução deve definir um comportamento robusto, sem reproduzir NaN/divisão por zero.
- Confirmar o valor/uso de b nas vistas disponíveis e se os limites de câmera evitam singularidades.
- Conferir os exemplos em execução do original e a convenção humana dos botões 0/1. Os endereços e os modos numéricos estão confirmados.

## Convenção angular de aresta

O assembly de `0x0041D1E0` recupera passos que o decompilador omite na representação C: inicializa ângulo em zero, calcula comprimento do vetor, avalia a função em `0x004852E4` com dy/comprimento, converte para graus, corrige quadrante quando dx<0 e normaliza usando 180/360. Evidência em [angulo-aresta-asm.json](evidencias/angulo-aresta-asm.json).

O padrão é compatível com direção por arco-seno corrigido por quadrante; a semântica exata de `0x004852E4` ainda não foi conferida no runtime. Para comparação futura, não tomar o pseudocódigo curto dessa função como implementação completa. A ordenação dos vértices de Basic e as ligações recíprocas dão um caso concreto para validar o teste interior/exterior e a reflexão documentada em [04-fisica-base.md](04-fisica-base.md).
