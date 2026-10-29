# Winigolf — reconhecimento inicial

Data: 2026-10-28. Escopo: análise estática inicial pelo GhidraMCP, somente leitura. Nenhuma renomeação, comentário, mudança de tipos, patch ou análise adicional foi solicitada ao banco do Ghidra. Os nomes FUN_* abaixo são os existentes; descrições funcionais são interpretações documentais.

## Identificação

- Acesso confirmado por `get_program_info`: `WiniGolf.exe`, projeto `/WiniGolf.exe`, origem `Z:/WiniGolfRE/WiniGolf/WiniGolf.exe`.
- PE, x86 little endian, ponteiros de 4 bytes, base `0x00400000`, compiler spec `borlandcpp`.
- SHA-256 informado pelo Ghidra: `3b50e7311d733a1afbd3a33b31e14f95564226e1310a230ffc375e6727a6fb53`.
- Banco atual: 3052 funções, incluindo 360 externas. Isso não mede quantas funções pertencem ao jogo.
- `.text`: `0x00401000–0x0048DFFF`; `.data`: `0x0048E000–0x0049BFFF`; `.rsrc`: `0x004A2000–0x00543FFF` (663552 bytes). Há também TLS, imports, exports e relocations.

## Entrada e inicialização

**Confiança alta:** o ponto de entrada PE é `0x00401000` (`entry`). A leitura do cabeçalho em `0x00400200` confirma PE32, AddressOfEntryPoint RVA `0x1000` e ImageBase `0x00400000`. A lista `entry_points` de `get_program_info` inclui vários exports e não deve ser interpretada como uma lista de entradas de execução do processo.

`entry` zera uma região global, consulta `GetModuleHandleA` e chama rotinas de startup, terminando em `FUN_004881B4`. Isso caracteriza infraestrutura de inicialização, não o loop de minigolfe.

**Confiança alta quanto ao comportamento; média quanto ao papel exato na cadeia de startup:** `FUN_004012C0` configura o título `WiniGolf`, cria Form7 e passa referências de Form1–Form6 e Form8 para `FUN_0045396C`, depois chama `FUN_004539EC`. É o candidato principal ao corpo de inicialização da aplicação. Há referência de dados a seu endereço em `0x0048E36C`; a cadeia indireta entre startup e essa tabela ainda precisa ser percorrida.

## Borland/VCL versus aplicação

**Confiança alta:** exports com decoração C++, `__GetExceptDLLinfo` (`0x00401060`), `___CPPdebugHook` (`0x0048E398`), `System@AnsiString` e classes VCL sustentam C++ Builder/Borland C++ com VCL. A presença da VCL não prova que o código próprio foi escrito em Delphi. A versão exata do compilador não foi determinada.

Há exports `@@Unit1@Initialize` até `@@Unit8@Initialize`, finalizadores e `_Form1` (`0x00497D88`) até `_Form8` (`0x00497DC0`). Isso oferece pontos de partida para separar formulários e eventos.

A região inicial de código contém carregamento de recursos do jogo, tabelas de campos e física; a região posterior contém infraestrutura VCL e runtime. Essa é uma orientação de busca, não uma divisão rígida por endereço. Construtores com nomes repetidos de TForm e métodos exportados não devem ser classificados automaticamente como lógica de jogo nem renomeados apenas pelo símbolo.

`FUN_004424EC` é um exemplo confirmado de infraestrutura gráfica: prepara dois objetos e chama `StretchBlt`, usando retângulos e o raster operation em `param_1[8]`.

## Mapa preliminar de funções

| Endereço | Evidência observada e interpretação | Confiança |
| --- | --- | --- |
| `0x004018C0` | Inicializa muitos campos do objeto, consulta GetTickCount, cria bitmaps, usa dimensões 640×480 e carrega `balls`/`ballmask`; provável inicialização do formulário principal. Ligação ao evento FormCreate ainda não verificada. | Alta no comportamento; média no evento |
| `0x00404EDC` | Grande sequência de constantes e tabelas, com string `Dogleg`; candidato a dados de geometria/configuração dos campos. Decompilação retornada truncada. | Alta para tabelas de campos; sem esquema completo |
| `0x0041B67C` | Transformações de coordenadas, chamadas matemáticas e ordenação por valores derivados de profundidade; provável preparação geométrica para desenho. | Média |
| `0x0041C074` | Converte coordenadas em posições com deslocamentos e opera sobre canvas/bitmap; chamada por `0x0041E654` em `0x0041EB1D`. Candidato ao desenho da geometria. | Média |
| `0x0041D2A4` | Salva posição anterior, calcula altura por geometria, soma componentes de velocidade à posição, aplica multiplicador, percorre arestas; ramo com `Bounce` restaura posição e reflete um ângulo, outro troca índice de superfície. Atualização física e tratamento de limites/colisões. | Alta no papel geral; média na semântica dos campos |
| `0x0041D9DC` | Recalcula três componentes a partir de ângulos/magnitude; chamada pela física em `0x0041D6F8`. | Média; convenções angulares pendentes |
| `0x0041E654` | Switch sobre byte em objeto+`0xC8674`, chama atualização/desenho, usa `Applause`, compara valores indexados por jogador/campo. Principal candidato à atualização por estado do jogo. | Alta no despacho; média nas regras |
| `0x0041ECB8` | Chama `0x0041D2A4`, rotinas gráficas e `0x004424EC`; inclui sons `Laugh` e alterações de estado. Integra física, desenho e resultados. | Alta na integração; regras pendentes |
| `0x0041F0A0` | Manipula seleção de jogadores e diversas tabelas, configura estado. Candidato a preparação de turno/partida. | Média |
| `0x0041FB40` | Marca índice atual como concluído, avança sequencialmente ou sorteia índice ainda não marcado, possui ramo para seis itens e muda estado para 5. Provável progressão de campos. | Média; não inferir que o jogo inteiro tem seis campos |
| `0x00420074` | Copia tabelas indexadas pelo índice em objeto+`0xC932C` para geometria de trabalho. Provável carregamento do campo selecionado. | Média/alta |
| `0x0040434C` | Soma ponderada sobre tabelas; resultado comparado durante mensagens de erro de leitura do Hole of Fame. Provável verificação de integridade dos recordes. | Alta na soma; média na finalidade |

## Evidências cruzadas

- String `balls` em `0x0048E445`: referência em `0x00401BC8`, dentro de `FUN_004018C0`. A decompilação também mostra `ballmask` em `0x0048E44B` e criação de bitmaps.
- `FUN_0041E654` seleciona `FUN_0041ECB8` quando o estado vale 2; esta chama `FUN_0041D2A4`. Ainda não foi confirmado o significado humano de cada estado.
- Referência de chamada a `FUN_0041E654` em `0x0041D7E8`: o MCP retornou `caller: null`. É necessário inspecionar o trecho antes de atribuir um timer ou evento.
- Imports enumerados por completo: GDI32 inclui BitBlt, StretchBlt, MaskBlt, GetPixel; USER32 inclui GetCursorPos, SetTimer/KillTimer; KERNEL32 inclui GetTickCount; WINMM inclui PlaySoundA e MCI. Imports demonstram disponibilidade, não uso pela lógica própria de cada subsistema. O uso de StretchBlt já foi confirmado por decompilação.
- Strings OnMouseMove/OnMouseUp aparecem em metadados de classes VCL. Nenhum handler específico do jogo foi confirmado nesta etapa. Não atribuir comportamento de mira, arraste ou tacada a essas strings isoladas.

## Limites

Não foram determinados o timer principal, handlers de mouse, unidades de velocidade, passo temporal, coeficientes físicos, formato completo dos campos, critérios de bola no buraco, pontuação ou condições finais. Os offsets são relativos ao objeto recebido, não endereços globais. A decompilação x87 exibe `in_ST*`/`longdouble`; fórmulas trigonométricas exigem conferência no assembly antes de transcrição.

As listas iniciais de funções/strings foram amostras paginadas, sem pretensão de inventário completo. Imports e exports foram enumerados integralmente. Evidências retornadas pelo MCP e pseudocódigo consultado estão em [evidencias/reconhecimento-ghidra.json](evidencias/reconhecimento-ghidra.json). A evidência de `0x00404EDC` está truncada na própria resposta do decompilador. Nenhum comportamento foi validado por execução dinâmica.
