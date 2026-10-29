# Extração em lote: 36 campos

2026-10-28. **Os 36 blocos de dados dos campos foram extraídos**, com proveniência por atribuição. Isso substitui o estado anterior em que apenas Basic tinha dados recuperados. A extração é estática e não equivale a validar jogabilidade no original.

## Artefatos e reprodução

- [dados/campos/](dados/campos/): arquivos 01.json a 36.json, com nomes, coordenadas, superfícies, ligações, par, partida, alvo e parâmetro não classificado. Cada valor conhecido inclui endereço de instrução e offset do objeto.
- [dados/campos-resumo.json](dados/campos-resumo.json): contagens, faixas de altura e exceções.
- [scripts/extract-courses.cjs](scripts/extract-courses.cjs): extrator reproduzível. Executar `node docs/scripts/extract-courses.cjs` da raiz deste repositório. Ele espera o executável original em `../WiniGolf/WiniGolf.exe`.
- [evidencias/inicializacao-campos-completa.json](evidencias/inicializacao-campos-completa.json): todas as 19051 instruções definidas em `0x00404EDC–0x0041B67B`.
- [Atlas dos campos](diagramas/campos-atlas.svg): planta de cada campo, com alturas e passagens. Regerar por `node docs/scripts/draw-course-atlas.cjs`.

O script confere SHA-256 e os bytes de todas as instruções contra o PE local. Avalia o trecho `0x00404EF3–0x0041B3F3`; os 36 blocos terminam antes de `0x0041B3F8`, onde começa inicialização de estado de demonstração. A função contém MOV/LEA/ADD/SHL/XOR/INC/DEC e chamadas aos helpers de AnsiString, sem branches na sequência dos campos.

As chamadas `0x0048C804`, `0x0048C8EC` e `0x0048C8BC` são modeladas para extrair nomes, sem executar runtime. EAX/ECX/EDX são invalidados após chamadas, salvo retorno explicitamente modelado do construtor; uso posterior de registrador desconhecido interrompe a extração. Instruções não suportadas também interrompem. Tags e profundidade de cleanup na pilha não fazem parte dos dados do campo.

## Resultado das verificações

- 36 nomes e blocos de inicialização; índice final ESI=36.
- **718 posições declaradas de vértices**, sendo 717 com coordenadas explicitamente atribuídas.
- **265 superfícies**; 19 campos têm variação de altura.
- Todos os índices de vértices e superfícies vizinhas estão dentro das contagens declaradas.
- Todas as passagens declaradas têm aresta inversa e ligação recíproca.
- Nenhuma superfície possui denominador de altura zero no cálculo baseado nos três primeiros vértices.
- A extração em lote de Basic coincide com a extração independente anterior para vértices, superfícies, partida, alvo, superfície inicial e parâmetro não classificado.

Esses são testes estruturais e de consistência da extração. Não provam ausência de todos os problemas geométricos, nem equivalência da futura física.

## Exceção preservada: Splitt, campo 25

O campo declara 19 vértices, mas não há atribuição das três coordenadas do vértice 7 na sequência completa de inicialização. **Nenhuma superfície referencia esse ID.** A exportação preserva o slot com `value: null`, `instruction: null` e status explícito de ausência de atribuição. Não foi presumido zero ou interpolado um ponto.

A lacuna não impede recuperar os polígonos utilizados. O desenho do atlas ignora slots sem coordenadas e interromperia se um deles fosse necessário para uma superfície. Para um port moderno, o slot pode ser omitido com remapeamento explícito dos IDs; isso seria transformação dos dados, não recuperação de um valor original.

## Catálogo

| ID | Nome original | Vértices declarados | Superfícies | Par | z mínimo–máximo |
| ---: | --- | ---: | ---: | ---: | --- |
| 1 | Basic | 10 | 2 | 2 | 10–10 |
| 2 | Dogleg | 14 | 4 | 2 | 10–10 |
| 3 | Dip | 16 | 5 | 2 | 10–90 |
| 4 | Horseshoe | 18 | 6 | 2 | 10–10 |
| 5 | Plateau | 21 | 5 | 2 | 50–100 |
| 6 | Cactus | 20 | 9 | 2 | 10–10 |
| 7 | Double-bump | 20 | 7 | 2 | 10–60 |
| 8 | Octagon | 18 | 8 | 2 | 10–10 |
| 9 | No | 18 | 6 | 3 | 10–10 |
| 10 | Mega | 10 | 2 | 2 | 10–10 |
| 11 | Hammer | 16 | 7 | 3 | 10–50 |
| 12 | Bump | 14 | 4 | 2 | 10–90 |
| 13 | Pyramid | 12 | 9 | 4 | 10–60 |
| 14 | Branches | 28 | 11 | 3 | 10–60 |
| 15 | Cross | 24 | 9 | 3 | 10–60 |
| 16 | Stadium | 19 | 6 | 2 | 10–50 |
| 17 | Long | 10 | 2 | 2 | 10–10 |
| 18 | Bump-up | 30 | 11 | 3 | 10–90 |
| 19 | Shortcut | 24 | 10 | 2 | 10–10 |
| 20 | Hills | 20 | 7 | 2 | 10–160 |
| 21 | Pipes | 32 | 11 | 3 | 10–70 |
| 22 | Spring | 20 | 6 | 3 | 10–10 |
| 23 | Snake | 32 | 11 | 3 | 10–40 |
| 24 | Double-dip | 22 | 8 | 2 | 10–60 |
| 25 | Splitt | 19* | 7 | 3 | 10–10 |
| 26 | Green | 8 | 1 | 1 | 10–10 |
| 27 | Turn | 21 | 7 | 3 | 10–90 |
| 28 | Side | 20 | 8 | 2 | 10–60 |
| 29 | Roundtrip | 30 | 10 | 3 | 10–110 |
| 30 | Down | 22 | 7 | 2 | 10–90 |
| 31 | Goldfish | 11 | 3 | 2 | 10–10 |
| 32 | Totem | 32 | 15 | 2 | 10–10 |
| 33 | Why | 12 | 5 | 2 | 10–10 |
| 34 | Walls | 28 | 12 | 3 | 10–60 |
| 35 | Zigzag | 22 | 8 | 3 | 10–10 |
| 36 | Maze | 25 | 16 | 4 | 10–10 |

*Splitt: um slot não atribuído e não utilizado. Os nomes são mantidos literalmente, inclusive `No` e `Splitt`.

Confiança alta no conteúdo atribuído pela rotina e no relacionamento com o loader. Ainda é preciso verificar eventuais alterações de geometria em outras rotinas; a extração não afirma que toda a partida usa dados imutáveis.
