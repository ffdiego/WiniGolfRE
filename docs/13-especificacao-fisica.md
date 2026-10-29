# Especificação física consolidada — versão estática 1

**Errata incorporada à seção 2:** a fórmula de mira e sua constante final foram corrigidas após decodificação independente e interpretação dos bytes x87. Ver [20-correcao-da-curva-de-tacada.md](20-correcao-da-curva-de-tacada.md). A versão anterior confundia multiplicações com divisões e interpretava a constante extended 0,1 como 0,4. Essa correção não altera as seções de movimento e colisão.

2026-10-28. **Pronta para orientar a implementação de um simulador de referência. Ainda não validada por medições no jogo original.** Este documento consolida os anteriores e prevalece sobre descrições preliminares quando explicita uma conferência posterior. O alvo é reproduzir uma sensação próxima, sem exigir bugs ou igualdade numérica com x87.

São distinguidos dois níveis:

- **Referência observada:** algoritmo recuperado do executável para entradas finitas e geometria válida. Confiança alta nas operações e ordem descritas, salvo indicação explícita.
- **Perfil proposto para o port:** decisões documentadas para robustez e separação de simulação/apresentação. Não são fatos atribuídos ao original e ainda precisam de comparação visual.

Não se iniciou uma implementação de jogo nem se alterou o executável/banco. Evidência adicional: [fechamento-fisica-asm.json](evidencias/fechamento-fisica-asm.json).

## 1. Unidades, geometria e estado

O passo nominal é **20 ms**, aproximadamente 50 passos/s. Velocidades são deslocamentos em unidades internas **por passo**, não metros/segundo. Não multiplicar novamente por 0,02 ao somar velocidade à posição. Se uma engine exigir unidades/segundo, a conversão precisa ser explícita e consistente.

Cada campo contém vértices x/y/z, polígonos ordenados e ligação por aresta. IDs exportados começam em 1. Ligação zero é parede; valor não zero indica superfície vizinha. Preservar paredes internas como as de Splitt.

A auditoria dos **265 polígonos** encontrou:

- ordem horária em x/y, polígonos convexos e área não nula;
- para os três primeiros vértices A/B/C, `B.x-A.x > 0` e `C.y-B.y < 0`;
- todos os demais vértices no mesmo plano calculado por A/B/C;
- 61 superfícies inclinadas, distribuídas em 19 campos.

Essas propriedades foram verificadas nos dados extraídos por [audit-physics-geometry.cjs](scripts/audit-physics-geometry.cjs), relatório [auditoria-geometria-fisica.json](dados/auditoria-geometria-fisica.json). Não são suposições genéricas sobre qualquer campo futuro. Novos campos devem passar pelas mesmas verificações ou usar outro cálculo de suporte.

Estado mínimo explícito para o algoritmo observado:

```text
BallState:
    position: x, y, z
    velocity: vx, vy, vz
    surfaceId
    bounceCount            acumulado durante a tacada

ShotContext:
    courseData/version
    strokeCount, totalStrokes
    stepIndex
    soundEnabled           dependência legada; excluir do núcleo moderno
```

A posição anterior é uma cópia local feita no início de cada passo, antes do cálculo de altura. Ângulos e módulo são temporários derivados da velocidade, não entradas independentes que o cliente deva escolher. A animação de câmera e o estado de interface ficam fora desse núcleo.

## 2. Puxada para velocidade inicial

A especificação de compatibilidade com os handlers usa pontos em pixels do formulário: âncora guardada no MouseDown e ponta guardada no MouseMove. O MouseUp não usa sua própria posição como origem do vetor.

```text
dx = pontaX - ancoraX
dy = pontaY - ancoraY          // tela: positivo para baixo
L = sqrt(dx² + dy²)

modo 1: k = 0.1, limite de L = 250 pixels
modo 2: k = 0.8, limite de L = 50 pixels

u = k * dx
w = k * dy / cos(c)

vx = (-cos(a)*u + sin(a)*w) * sqrt(L) * 0.1
vy = ( sin(a)*u + cos(a)*w) * sqrt(L) * 0.1
```

`a` é o ângulo em objeto+`0x81B10`; `c`, em `+0x81B18`, convertido de graus para radianos antes das funções trigonométricas. c=-60° é gravado na preparação do turno. O ângulo intermediário b da projeção não participa desta fórmula. O limite de L é aplicado no MouseMove, que reconstrói e arredonda a ponta; portanto a fórmula recebe os pontos já limitados, não um vetor contínuo de comprimento ilimitado.

**Confiança alta no cálculo corrigido:** decodificação independente Iced e interpretação de 234 casos da sequência original, além da leitura no Ghidra. Constantes em `0x0041E624` (double 0,1), `0x0041E640` (double 0,8), `0x0041E648` (extended 0,1, bytes `CD CC CC CC CC CC CC CC FB 3F`). `0x0041E1DD/0x0041E202` são FMUL e `0x0041E1EC/0x0041E211` são FMULP. A política exata de arredondamento do endpoint ainda não foi reconstruída integralmente; não reivindicar reprodução do último pixel.

Para a=0 e c=-60°, puxar 100 pixels para a esquerda no modo 1 gera `(vx,vy)=(10,0)`; 50 pixels à esquerda no modo 2 geram aproximadamente `(28.2842712,0)`. Para direção e câmera fixas, o impulso cresce como **L elevado a 1,5**, pois o vetor de entrada já é proporcional a L e depois é multiplicado por sqrt(L). A compensação da câmera torna o efeito dependente da direção na tela.

**Perfil proposto:** vetor zero não inicia nem conta tacada; rejeitar entradas não finitas e câmera singular; inicializar vz=0. A interface transforma a puxada em comando canônico antes de rede/replay. O protocolo e a quantização de rede continuam separados desta especificação de física; a origem e surfaceId são autoritativos no servidor.

## 3. Altura e aceleração em rampa

Sejam A/B/C os três primeiros vértices da superfície selecionada na entrada do passo:

```text
dx = B.x - A.x         dzx = B.z - A.z
dy = C.y - B.y         dzy = C.z - B.z

height(x,y) = A.z + dzx*(x-A.x)/dx + dzy*(y-A.y)/dy
ax = -0.5 * dzx / sqrt(dx² + dzx²)
ay = +0.5 * dzy / sqrt(dy² + dzy²)
```

**Confiança alta**, instruções `0x0041D387–0x0041D476`. O sinal positivo em ay é consistente com a ordenação recuperada: dy é negativo em todos os polígonos. Em termos de gradientes, ambas as componentes aceleram para baixo da inclinação. Não substituir o modelo por gravidade de corpo rígido sem assumir uma mudança de comportamento.

Não há aceleração gravitacional vertical nem cálculo de rotação de esfera nesta rotina. A altura vem da superfície de suporte; o raio do sprite não desloca o limite de colisão observado.

## 4. Passo observado, na ordem exata de alto nível

Fonte principal: `0x0041D2A4`, conferida nas 272 instruções definidas.

```text
previousPosition = position
sourceSurface = surfaceId

// altura sob a posição de entrada, antes do deslocamento
z = height(x,y) usando sourceSurface
vx = vx + ax
vy = vy + ay

x = x + vx
y = y + vy
z = z + vz

vx = vx * 0.98
vy = vy * 0.98

savedVz = vz
vz = 0
(speed, alpha, beta) = direção/módulo derivados da velocidade
pendingSurface = 0

para cada aresta de sourceSurface, na ordem dos dados:
    edgeAngle = direction(aresta.inicio, aresta.fim)
    pointAngle = direction(aresta.inicio, position.xy)
    delta = normalize180(pointAngle - edgeAngle)
    se delta >= 0:
        se neighbor == 0:
            se som habilitado: bounceCount++ e emitir som
            alpha = normalize180(2*edgeAngle - alpha)
            position = previousPosition
        senão:
            pendingSurface = neighbor

se pendingSurface != 0: surfaceId = pendingSurface
velocity = reconstruir(speed, alpha, beta)
vz = savedVz

se bounceCount > 50:
    velocity = zero
    position = partida do campo
```

Consequências que fazem parte da referência:

- A altura pode ficar um passo atrás de x/y. Não projetar automaticamente z sobre a nova posição antes de comparar resultados.
- Não há cálculo de instante de impacto nem varredura do raio da bola.
- O loop **não interrompe na primeira parede**. Depois de um rebote, as próximas arestas testam a posição restaurada e recebem o alpha já refletido.
- A superfície usada pelo loop continua sendo a original. A troca só é aplicada depois de todas as arestas; não percorrer imediatamente as arestas do vizinho no mesmo passo.
- Se mais de uma passagem for selecionada, prevalece a última na ordem dos dados. Um rebote posterior não limpa pendingSurface no trecho observado.
- O reposicionamento após mais de 50 rebotes não restaura explicitamente surfaceId neste trecho. Dependência do som e possível inconsistência de superfície são detalhes legados, não requisitos de fidelidade desejada.

## 5. Direções, fronteiras e reflexão

`0x0041D1E0` calcula a direção com arco-seno e correção de quadrante. `0x004852E4` foi confirmado como **asin**: o wrapper passa a string `asin` em `0x00495C07` e o núcleo `0x004851E0` usa sqrt(1-u²)/FPATAN, com tratamento das extremidades. `0x004852FC` usa núcleo `0x0048544C` de **atan**, confirmado por FPATAN e seleção de quadrante.

Para valores finitos ordinários, a função de direção pode ser descrita sem artefatos x87:

```text
direction(A,P):
    dx = P.x-A.x; dy = P.y-A.y
    r = sqrt(dx²+dy²)
    se r == 0: retornar 0 graus
    t = asin(dy/r) * 180/pi
    se dx < 0: t = 180-t
    se t > 180: t = t-360
    retornar t

normalize180(t):
    se t > 180: t -= 360
    se t < -180: t += 360
    retornar t
```

Para o intervalo de ângulos produzido, uma correção por lado basta. Em casos comuns direction equivale a atan2(dy,dx), normalizando a direção horizontal negativa para +180. Os detalhes de zero com sinal e arredondamento não são equivalência bit a bit.

Como os polígonos são horários, os pontos interiores ordinários produzem delta negativo. O original trata `delta >= 0` como limite atingido. **Não trocar silenciosamente por teste com epsilon ou por ponto-dentro genérico:** pontos exatamente na aresta, no vértice e diferenças de ±180 podem mudar de classificação. A ordem das arestas é parte da referência.

A reflexão altera a direção para `2*edgeAngle-alpha` e preserva o módulo já amortecido. Portanto não existe perda extra de energia por coeficiente de restituição nesse ramo. A restauração da posição anterior faz perder o deslocamento daquele passo, inclusive sua componente tangencial.

## 6. Conversão velocidade ↔ ângulos: tratamento legado

Em `0x0041D80C`:

```text
speed = sqrt(vx²+vy²+vz²)
alpha = atan(vy/vx) em graus, com correção por sinal de vx
beta  = atan(vz/vx) em graus, com correção por sinal de vx
```

Quando vx=0, cada ângulo é +90 ou -90 segundo a componente correspondente, e zero quando essa componente também é zero. Em vx<0, acrescenta 180 quando a outra componente é não negativa, ou subtrai 180 quando é negativa. Durante as colisões, vz foi temporariamente zerado; beta não representa a inclinação do terreno.

Em `0x0041D9DC`:

```text
t = tan(alpha); u = tan(beta)
vx = sqrt(speed² / (1+t²+u²))
se alpha > 90 ou alpha < -90: vx = -vx
vy = t*vx
vz = u*vx
se t==0 e u==0: vz = speed
```

As constantes de ±90, -1 e 1 foram lidas em `0x0041DB0C–0x0041DB1C`. O último ramo é peculiar, mas vz é depois restaurado pelo chamador. Não copiá-lo como uma regra de salto da bola. Próximo de ±90, tangente e arredondamento podem produzir componentes residuais.

**Perfil proposto:** como o núcleo de suporte é planar, preservar diretamente `(vx,vy)` quando não há reflexão; em colisão calcular direção refletida e recompor `vx=speed*cos(alpha)`, `vy=speed*sin(alpha)`, com speed=hypot(vx,vy) antes do loop e atualizado apenas por amortecimento/aceleração do passo. Isso remove a conversão com tangentes e sua singularidade. Manter a ordem de reflexões e restauração de posição. É uma simplificação matemática intencional, a comparar em cantos e tiros exatamente verticais, não transcrição literal do executável.

## 7. Parada, buraco e fim de tacada

Após a atualização, `0x0041ECB8` usa:

```text
stopped = superfície plana pelos z de A/B/C
          e abs(vx)<0.05 e abs(vy)<0.05

captured = abs(x-hole.x)<8 e abs(y-hole.y)<8
           e abs(vx)<2 e abs(vy)<2
```

Todos os limites são **estritos**. Captura é um quadrado no plano x/y; não usa distância radial nem teste explícito de altura nesse bloco. A condição de parada não deve ser aplicada em rampa. O original não impõe limiar de vz aqui.

**Ordem legada:** o teste de parada vem primeiro; pode aplicar penalidade 7→9 tacadas, atualizar recordes e chamar a preparação do próximo turno. O código então continua para o teste de captura, sem retorno após essa chamada. Isso pode testar o estado carregado pela preparação do turno; não fundir os blocos em uma função pura presumindo que usam invariavelmente a mesma bola.

**Perfil proposto:** avaliar captura primeiro sobre o resultado físico da bola atual; se capturada, retornar evento Captured. Senão, se stopped, zerar velocidade e retornar Stopped ou StrokeLimitReached; na sétima tentativa sem captura, registrar 9 e penalidade +2. A camada de partida altera turno/campo após receber esse evento, fora do passo físico. Essa ordem elimina a dependência do teste final em uma troca de jogador no meio da avaliação.

Ao capturar, conservar posição física de captura e gerar evento; animação de entrada no buraco pode usar uma posição visual no centro. Não confundir snapping gráfico com resultado físico. A interface pode aguardar a animação antes de exibir o próximo turno sem atrasar o cálculo autoritativo.

## 8. Perfil proposto para o primeiro simulador

Este é o ponto de partida implementável para uma versão próxima e robusta; mudanças ficam versionadas.

| Aspecto | Decisão inicial proposta |
| --- | --- |
| Unidades e passo | Dados originais, 20 ms fixos, velocidade por passo |
| Números | double persistente no protótipo, ordem explícita de operações; sem alegação de identidade x87 ou entre plataformas |
| Dados | JSON extraído, IDs e ordem originais, slot não utilizado de Splitt preservado/ignorado sem preencher coordenadas |
| Altura/aceleração | Fórmulas e ordem da seção 3/4, incluindo z calculado antes do deslocamento |
| Colisão | Ponto sobre polígonos de suporte, teste angular e percurso ordenado; não acrescentar raio ou swept collision nesta primeira comparação |
| Direção | Reflexão angular e recomposição planar robusta, sem conversão por tangentes |
| Paredes internas | Todas as ligações zero preservadas, inclusive duas faces de uma parede compartilhada |
| Som e câmera | Apenas consumidores de eventos; sem escrever no estado físico |
| Proteção de rebotes | Contar toda parede, independente do som; acima de 50, retornar à partida com velocidade zero **e superfície inicial restaurada**, emitindo evento de reposicionamento |
| Parada/captura | Limiares originais, captura antes de parada, evento puro antes da troca de turno |
| Entrada inválida | Rejeitar vetor não finito/zero e campos inválidos antes de contar tacada |
| Limite de duração | Parâmetro obrigatório maxSteps, sem valor alegadamente recuperado; se atingido, devolver SimulationLimitReached para tratamento explícito pela partida |

`maxSteps` precisa ser configurado e registrado por quem chama o simulador. Não transformar esse limite operacional em penalidade automática ou vitória. O resultado inclui estado e motivo da interrupção; a política de partida/rede poderá cancelar/repetir a jogada ou aplicar regra própria, depois de testes. A simulação não pode rodar indefinidamente em uma rampa oscilante.

Não há necessidade de engine gráfica neste núcleo. Para online, mesmas versões de física/dados e checkpoint completo; cliente pode prever e servidor confirma, conforme [12-proposta-online-e-determinismo.md](12-proposta-online-e-determinismo.md).

## 9. Casos de aceitação para o futuro simulador

Os valores abaixo são expectativas derivadas e têm tolerância de cálculo; não são gravações do jogo. Comparar fatos físicos, não pixels de sprite.

| Caso | Entrada | Resultado esperado |
| --- | --- | --- |
| Impulso horizontal | a=0,c=-60,modo1,dx=-100,dy=0 | vx=10, vy=0 |
| Impulso modo2 | a=0,c=-60,dx=-50,dy=0 | vx≈28,2842712, vy=0 |
| Plano, um passo | Basic, (-250,0,10), v=(10,0,0), superfície1 | x=-240, vx=9,8, mesma superfície |
| Passagem | Basic, (99,0,10), v=(2,0,0), superfície1 | x=101, vx=1,96, superfície2 |
| Parede reta | Basic, (-299,0,10), v=(-2,0,0), superfície1 | posição restaurada, vx≈+1,96 |
| Descida | Dip, (-125,0,50), v=(2,0,0), superfície2 | x≈-122,7647059, z=50, vx≈2,1905882 |
| Subida | Dip, (75,0,50), v=(2,0,0), superfície4 | x≈76,7647059, z=50, vx≈1,7294118 |
| Parede interna | Splitt, (0,201,10), v=(0,-2,0), superfície4 | posição restaurada, vy≈+1,96; não passar à superfície5 |
| Reversão em rampa | Dip, (75,0,50), v=(0,0,0), superfície4 | acelera para -x; não retornar Stopped |
| Captura | Basic, próximo ao alvo, com abs(vx),abs(vy)<2 após passo | Captured se também distâncias por componente <8 |
| Limite estrito | Avaliação direta com abs(vx)=2 ou abs(x-hole.x)=8 | Não capturar por esse teste |
| Plano lento | Avaliação direta em plano, `v=(0.049,0.049,0)` | Stopped se não houver captura |
| Rampa lenta | Mesma velocidade em superfície inclinada | Não encerrar somente pelo limiar de velocidade |

Adicionar casos de canto, limite ±180, ponto sobre portal, proteção de rebotes e tiros oblíquos antes de estabilizar uma versão de rede. O trajeto Basic com vx=10 tem previsão de captura no passo 107, descrita em [05-campo-basic-e-formato.md](05-campo-basic-e-formato.md).

## 10. O que está fechado e o que resta validar

**Fechado estaticamente:** formato e propriedades dos campos, unidades, constantes, equações de impulso/altura/aceleração/amortecimento, sentido dos ângulos, ordem de colisões e trocas de superfície, limiares e relação com a conclusão da tacada. Os casos legados problemáticos e as mudanças propostas estão explícitos, sem ocultá-los em pseudocódigo corrigido.

**Pendente de comparação prática:** sensação da força, alcance, comportamento de cantos e fronteiras, singularidades numéricas, e impacto das simplificações propostas. A reprodução exata do runtime e do arredondamento de endpoint não é objetivo obrigatório, mas seus efeitos não foram medidos. Nenhum teste atual comprova igualdade cliente/servidor entre plataformas.

O próximo trabalho concreto pode ser um simulador sem gráficos que carregue os JSON e execute esses casos, seguido de comparação com jogadas observadas no original. A escolha de engine gráfica pode continuar adiada.
