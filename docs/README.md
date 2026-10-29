# Guia da investigação

Para implementar a física, começar por [13-especificacao-fisica.md](13-especificacao-fisica.md). É a consolidação atual, com algoritmo observado, decisões propostas e casos de aceitação. Os documentos numerados anteriores preservam a investigação e os graus de confiança da época; não tratá-los como requisitos independentes conflitantes.

O primeiro núcleo C# e console já podem ser executados: [instruções do simulador](../simulator/README.md). Resultados e limitações encontrados estão em [14-primeiro-simulador.md](14-primeiro-simulador.md).

A validação mais recente compara o perfil original do simulador com uma política experimental corrigida: [15-validacao-de-cantos.md](15-validacao-de-cantos.md). O segundo perfil é uma modernização explícita e permanece opcional.

Já há uma interface para experimentar os campos, puxar a bola e comparar os perfis: [16-laboratorio-visual.md](16-laboratorio-visual.md). Abrir pelo [iniciador do laboratório](../simulator/abrir-laboratorio.cmd).

A [revisão de mira e buraco](17-revisao-mira-e-buraco.md) corrige a força do botão esquerdo, acrescenta o direito e torna a captura visível; também registra as evidências de rotação aleatória da câmera.

A primeira versão jogável com raylib está em [18-primeiro-cliente-raylib.md](18-primeiro-cliente-raylib.md): 36 campos, câmeras inclinada/planta, animação interpolada e progressão. Abrir com [abrir-jogo.cmd](../simulator/abrir-jogo.cmd); o laboratório continua separado.

A atualização [19-sons-originais-e-ajuste-da-mira.md](19-sons-originais-e-ajuste-da-mira.md) adiciona seis efeitos recuperados do executável e força do esquerdo ajustável em 1×/2×/3×, com padrão 2×.

**Correção importante:** [20-correcao-da-curva-de-tacada.md](20-correcao-da-curva-de-tacada.md) substitui a fórmula anterior de mira após auditoria dos bytes x87: a curva é L^1,5, com multiplicação por sqrt(L) e 0,1. O padrão atual do cliente é **1× da fórmula corrigida**; a seção 2 da especificação 13 já incorpora a errata.

O cliente agora usa [projeção ortográfica e virada aleatória entre tacadas](21-camera-ortografica-e-virada.md). A perspectiva foi removida; a câmera gira antes de liberar a próxima puxada.

- [Dados dos 36 campos](dados/campos/) e [resumo](dados/campos-resumo.json).
- [Extração e catálogo](08-extracao-dos-36-campos.md), [atlas](diagramas/campos-atlas.svg) e [auditoria geométrica](dados/auditoria-geometria-fisica.json).
- [Paredes internas e Splitt](10-splitt-e-paredes-internas.md).
- [Turnos e regras](11-turnos-e-regras.md).
- [Direção para online e determinismo](12-proposta-online-e-determinismo.md).

`evidencias/` contém retornos de leitura do Ghidra; `scripts/` contém extratores/auditorias locais e geração de diagramas. Eles não executam o jogo nem modificam o banco. `dados/` contém produtos derivados, incluindo proveniência e lacunas explícitas. As validações atuais são estáticas, não medições dinâmicas do original.
