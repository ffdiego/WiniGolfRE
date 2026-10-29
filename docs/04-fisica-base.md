# Física base para uma reconstrução semelhante

Nota de consolidação: este documento preserva a etapa inicial. A especificação atual e as conferências posteriores estão em [13-especificacao-fisica.md](13-especificacao-fisica.md).

Data: 2026-10-28. Este documento descreve o que foi observado no executável, separando parâmetros confirmados de decisões futuras. Fontes: pseudocódigo de `0x0041D2A4`, disassembly completo dessa função (272 instruções), trecho de `0x0041ECB8`, bytes das constantes e das rotinas matemáticas. Ver [evidências desta etapa](evidencias/eventos-fisica-ghidra.json) e [reconhecimento](01-reconhecimento.md).

## Campos relevantes do objeto principal

Os nomes abaixo são descritivos, usados apenas na documentação. Todos os offsets são relativos ao objeto do formulário, não endereços globais.

| Offset | Interpretação | Confiança/evidência |
| --- | --- | --- |
| `+0xC8218` | Índice de superfície atual | Alta: seleciona geometria e muda nas ligações entre arestas |
| `+0xC821C/+0xC8220/+0xC8224` | Posição x, y, z | Alta: integração, altura de superfície e comparação com alvo |
| `+0xC8228/+0xC822C/+0xC8230` | Posição anterior | Alta: cópia na entrada e restauração na colisão |
| `+0xC8234/+0xC8238/+0xC823C` | Componentes vx, vy, vz | Alta: somadas à posição e usadas no módulo |
| `+0xC8258/+0xC825C` | Ângulos derivados da velocidade | Alta no papel, convenção exata pendente |
| `+0xC8264` | Módulo da velocidade | Alta: soma de quadrados seguida de raiz |
| `+0xC828C/+0xC8290` | Coordenadas x/y do alvo, aparentemente buraco | Média/alta: proximidade leva ao estado 3 |
| `+0xC8674` | Estado do jogo | Alta: 1 permite tacada, 2 integra bola, 3 inicia conclusão |
| `+0xCDBD0` | Contador incrementado no ramo de som de colisão | Alta: não é contador independente do som |

## Passo temporal e amortecimento

O callback em `0x0041D78C` usa intervalos de GetTickCount/20 e chama o despacho uma vez ao mudar de intervalo. Isso indica passo nominal de **20 ms / 50 Hz**, sem catch-up físico no trecho observado. A integração não multiplica velocidade por dt: componentes são deslocamentos por atualização em unidades internas do campo.

Em `0x0041D47C–0x0041D4D0`, a posição recebe a velocidade e depois vx/vy são multiplicadas por **0,98**, double em `0x0041D770`. Isso é amortecimento proporcional à velocidade, não desaceleração constante. **Confiança alta**, conferido nas instruções FADD/FMUL/FSTP e nos bytes da constante.

Em trecho plano, sem colisões ou outras transições, a consequência matemática é:

```text
p[k+1] = p[k] + v[k]
v[k+1] = 0.98 * v[k]
v[k]   = v[0] * 0.98^k
```

A 50 Hz nominais, sobra aproximadamente 36,4% da velocidade após um segundo e a meia-vida é aproximadamente 0,686 s. A distância teórica até velocidade tender a zero seria 50*v[0], por componente; o jogo encerra o turno antes disso por limiar. Esses números são deduções das equações, não medições do jogo.

## Rampas e altura

`0x0041D2A4` consulta três vértices da superfície, identificados por índices em registros de 32 bytes. Há arrays separados de coordenadas inteiras em bases relativas `+0xB9CFC`, `+0xBA01C` e `+0xBA33C`. Os índices usados apontam para os dados efetivos dentro desses arrays; não assumir base zero do formato original.

Chamando os três vértices usados de A, B e C, os deltas observados são:

```text
dx = B.x - A.x     dzx = B.z - A.z
dy = C.y - B.y     dzy = C.z - B.z

z = A.z + dzx*(x - A.x)/dx + dzy*(y - A.y)/dy

vx -= 0.5 * dzx / sqrt(dx*dx + dzx*dzx)
vy += 0.5 * dzy / sqrt(dy*dy + dzy*dzy)

x += vx
y += vy
z += vz
vx *= 0.98
vy *= 0.98
```

**Confiança alta na transcrição local:** altura calculada em `0x0041D387–0x0041D404`; alterações de velocidade em `0x0041D418–0x0041D476`; constante float **0,5** em `0x0041D76C`. O sinal diferente de vy é o observado nas instruções; a orientação dos eixos e a ordem geométrica dos vértices precisam ser relacionadas ao campo antes de converter para coordenadas modernas. Não normalizar silenciosamente esse sinal.

O modelo parece considerar duas inclinações a partir de uma ordem específica de vértices, em vez de um corpo rígido rolando livremente em uma malha geral. Falta verificar quais restrições os campos impõem a A/B/C e como evitam denominadores nulos. Não há fundamento para interpretar 0,5 como m/s²: são unidades por atualização.

## Colisões e passagem entre superfícies

Após integrar e amortecer, a rotina deriva ângulos/magnitude por `0x0041D80C`, temporariamente zerando vz. Para cada aresta da superfície atual, chama `0x0041D1E0` para comparar direção da aresta com direção até a posição da bola. A diferença angular é normalizada usando **180 e 360 graus**; diferença não negativa seleciona um ramo de limite. **Confiança alta no teste angular**, mas a convenção geométrica interior/exterior ainda depende da ordem dos vértices.

- Aresta com ligação zero: trata como limite sólido. Atualiza ângulo horizontal para `2*anguloAresta - anguloVelocidade`, normaliza e restaura a posição anterior. O fator 2 está em `0x0041D788`; instruções em `0x0041D630–0x0041D6B2`. Não há coeficiente separado de restituição nesse ramo; a velocidade já recebeu o fator 0,98 do passo.
- Aresta com ligação não zero: armazena o índice ligado e o aplica como superfície atual depois do percurso, em `0x0041D6E5–0x0041D6F0`. Portanto, algumas arestas são transições entre áreas, não paredes.
- Ao final, `0x0041D9DC` reconstrói componentes a partir de ângulos/magnitude e a rotina restaura vz. A preservação exata de direção nas singularidades e em múltiplas arestas no mesmo passo precisa de conferência adicional.

O tratamento observado é discreto: integra, testa limite e volta à posição anterior quando rebate. Não foi visto cálculo do instante exato de impacto nem varredura de uma esfera com raio nesse trecho. Isso não exclui tratamentos específicos em outras funções.

Há um detalhe legado comprovado: o incremento do contador `+0xCDBD0` fica **dentro da condição de som habilitado**, em `0x0041D60D–0x0041D62A`. Se o contador passa de 50, a rotina zera velocidades e reposiciona a bola em coordenadas de uma tabela do campo. Para a reconstrução, não há motivo funcional para atrelar proteção de colisões ao áudio; essa é uma decisão de implementação sugerida, não comportamento novo atribuído ao original.

## Parada e captura do buraco

Em `0x0041ECB8`, após física e desenho:

- Se os três valores z dos vértices selecionados são iguais (superfície plana), e **abs(vx) < 0,05 e abs(vy) < 0,05**, segue para `0x0041F0A0`, candidato à preparação de turno. Constante double em `0x0041F090`; testes em `0x0041EE42–0x0041EEE8`. O limiar é por componente e o trecho não exige vz pequeno. **Confiança alta nas condições; média no significado completo da função chamada.**
- Se **abs(x-alvo.x) < 8 e abs(y-alvo.y) < 8**, entra no teste de velocidade. Se **abs(vx) < 2 e abs(vy) < 2**, grava estado 3. Constantes float em `0x0041F098/0x0041F09C`; testes em `0x0041EF8B–0x0041F041`. É uma região quadrada em x/y, não distância radial. Não há teste explícito de z nesse bloco. **Confiança alta nas condições, média/alta na interpretação de captura**, a confirmar pela animação do estado 3.

`0x00486A58` é raiz quadrada: seus bytes incluem FSQRT (`D9 FA`). Isso confirma que sqrt(vx²)/sqrt(vy²) nos testes acima representam valores absolutos e corrige os artefatos `in_ST*` do decompilador.

## Diretriz para a futura implementação

Uma reprodução próxima deve preservar inicialmente: passo físico fixo de 20 ms, unidades internas de geometria, fator 0,98, aceleração em rampas, relação entre arraste e impulso, classificação das arestas e limiares de parada/captura. Renderização pode rodar em frequência independente. Recuperar passos atrasados por acumulador seria uma modernização intencional do agendamento, mantendo os mesmos passos físicos.

Não é necessário transplantar o runtime, o layout enorme do formulário, os artefatos x87 ou a dependência de proteção de colisões em som. Nenhum código de jogo moderno foi iniciado nesta etapa.

Próximo incremento recomendado: reconstruir um campo simples a partir de `0x00404EDC`/`0x00420074`, confirmar orientação dos eixos e sinais da transformação de mira, e descrever um trajeto de referência em plano/rampa/parede. Isso permitirá especificar uma física implementável e comparar alcance e rebotes antes de escolher a engine. A confirmação dinâmica do original ainda está pendente.
