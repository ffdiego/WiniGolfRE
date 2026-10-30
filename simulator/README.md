# Simulador físico local

Protótipo C#/.NET 10, com núcleo independente e console sem pacotes externos. Carrega os 36 JSON exportados e executa a física `modern-reference-1` baseada em [especificação estática](../docs/13-especificacao-fisica.md). Não abre o executável original nem o banco Ghidra.

Também há um **laboratório visual para Windows**: executar `dotnet run --project simulator/src/Winigolf.Viewer` na raiz deste repositório. Permite selecionar campo, clicar/puxar a bola, pausar, avançar um passo, percorrer a trajetória e comparar os dois perfis. Ver [controles, decisões e validação](../docs/16-laboratorio-visual.md). Usa Windows Forms apenas como interface de depuração; não fixa a engine do jogo futuro.

O **cliente jogável C# + raylib** pode ser aberto com `dotnet run --project simulator/src/Winigolf.Game`. Inclui os 36 campos, projeção ortográfica inclinada/planta, mira por dois botões, interpolação e progressão. A câmera vira aleatoriamente entre tacadas; não há mais perspectiva nem atalho P. [Transição e testes atuais](../docs/21-camera-ortografica-e-virada.md). Ele inicia com o perfil corrigido `surface-traversal-2`. [Arquitetura inicial](../docs/18-primeiro-cliente-raylib.md). O projeto restaura Raylib-cs do NuGet.org; as licenças estão em [licenses/](licenses/).

O cliente já usa os seis efeitos sonoros originais extraídos do PE. M alterna o som; o painel ajusta o esquerdo em 1×/2×/3×. O padrão atual é **1× da curva corrigida** (`drag-x87-corrected-2`): multiplicação por sqrt(L) e 0,1, resposta L^1,5. A curva anterior e o reforço temporário de 2× foram substituídos após auditoria independente dos bytes. [Correção e verificação](../docs/20-correcao-da-curva-de-tacada.md); [sons](../docs/19-sons-originais-e-ajuste-da-mira.md).

Também há o perfil experimental `surface-traversal-2`, selecionado por `--profile traversal` em `run`/`sweep`. O padrão continua `--profile reference`. A versão nova corrige travessias e cantos; há diferenças reais de trajetória, documentadas em [validação de cantos](../docs/15-validacao-de-cantos.md).

O programa calcula os passos sem espera de relógio: 107 passos correspondem a 2,14 segundos **simulados**, mas são calculados tão rápido quanto o computador permitir. Isso também permite futuramente executar o núcleo no servidor e reproduzir a animação no cliente.

## Executar

Na raiz deste repositório, com SDK .NET 10 instalado:

```powershell
dotnet run --project simulator/src/Winigolf.Simulator -- verify

# Basic: puxada para a esquerda, modo 2; gera impulso (10,0).
dotnet run --project simulator/src/Winigolf.Simulator -- run --course 1 --drag -25 0 --mode 2 --max-steps 6000 --output artifacts/simulator/basic-drag.json

# Dip: mesmo impulso em coordenadas do campo, sem conversão de mouse.
dotnet run --project simulator/src/Winigolf.Simulator -- run --course 3 --velocity 10 0 --max-steps 6000 --output artifacts/simulator/dip.json

# Splitt: teste local junto à parede interna, na superfície 4.
dotnet run --project simulator/src/Winigolf.Simulator -- run --course 25 --position 0 201 10 --surface 4 --velocity 0 -2 --max-steps 6000 --output artifacts/simulator/splitt.json

# 864 tacadas: 36 campos × 8 direções × 3 módulos de impulso.
dotnet run --project simulator/src/Winigolf.Simulator -- sweep --max-steps 6000 --output artifacts/simulator/sweep.json

# Mesma bateria com a política experimental; não sobrescreve a referência.
dotnet run --project simulator/src/Winigolf.Simulator -- sweep --profile traversal --max-steps 6000 --output artifacts/simulator/sweep-traversal-v2.json

# Testes de um passo junto às bordas de todas as superfícies.
dotnet run --project simulator/src/Winigolf.Simulator -- audit --output artifacts/simulator/geometry-probes-v2.json
```

Usar ponto decimal nos argumentos. Em `run`, fornecer exatamente uma opção entre `--drag DX DY` e `--velocity VX VY`. Posição personalizada exige `--position X Y Z` e `--surface ID` juntos. `--mode` vale 1 ou 2; câmera padrão a=0,c=-60. `--yaw` e `--tilt` ajustam a conversão da mira. `--prior-strokes` aceita 0 a 6 para testar contagem/penalidade. `--data DIRETORIO` substitui os JSON copiados para o diretório de saída pelo build.

`max-steps` é obrigatório e varia de 1 a 1.000.000; é um limite operacional configurado pelo chamador, não um valor recuperado do jogo. 6000 corresponde a até 120 segundos simulados. As trajetórias completas ficam em memória neste protótipo; para lotes grandes ou servidor, será útil permitir amostragem/streaming.

O núcleo físico, o console e o laboratório não têm pacotes NuGet externos. O cliente raylib usa `Raylib-cs` 8.1.0 e a ferramenta de auditoria usa `Iced` 1.21.0, restaurados do NuGet.org. O primeiro build desses dois projetos exige acesso à rede; depois, o cache local do NuGet permite builds offline.

## Saídas

`run --output arquivo.json` grava:

- **JSON:** versão física, hash do arquivo de campo, estado final, motivo de término, tacadas/penalidade e estado/eventos por passo, incluindo origem no passo 0.
- **CSV:** posição, velocidade, superfície e eventos por passo para comparação/planilha.
- **SVG:** planta do campo e linha da trajetória, abrível em um navegador. É um diagrama estático, não uma animação nem gravação do original.

`verify --output arquivo.json` grava o relatório dos casos de aceitação. `sweep` grava um resumo de cada tacada, limites atingidos e diagnósticos de posição fora da superfície. O hash é do JSON consumido, não do executável original.

Códigos de saída: 0 = execução concluída (ou verificações aprovadas); 1 = falha de verificação/auditoria, erro na bateria ou resolução geométrica abortada no sweep; 2 = argumento/dado inválido; 3 = `run` atingiu limite de passos ou falhou na resolução geométrica. **Diagnóstico de contenção no sweep é reportado no JSON e não altera sozinho o código 0.** Ver o relatório antes de interpretar sucesso como validade física.

## Estrutura e contrato

- `src/Winigolf.Physics`: tipos imutáveis de estado, leitura/validação de campos, conversão de mira, passo e simulação de tacada. Sem áudio, câmera visual ou rede.
- `src/Winigolf.Simulator`: console, verificações, bateria e exportadores. Sem framework de teste externo; `verify` retorna código não zero em qualquer falha.
- `src/Winigolf.Viewer`: interface Windows Forms e reprodução de trajetórias do mesmo núcleo. `--self-test artifacts/viewer` verifica interação, reprodução e renderização sem janela visível.
- `src/Winigolf.Game`: cliente jogável raylib com geometria 3D e apresentação interpolada; `--self-test artifacts/game` abre contexto gráfico oculto e verifica integração, câmera e framebuffer real.

`Simulator.Step` executa somente um passo e aceita velocidade zero para testar reversão em rampa. `Simulator.Run` exige impulso não zero, z da velocidade igual a zero, origem dentro da superfície informada e limite de passos. Cada nova tacada começa com contador de rebotes zerado. A bola é tratada como ponto sobre suporte planar; este protótipo não simula voo, raio de esfera, atrito rotacional ou colisão entre bolas.

`Evaluate` testa captura antes de parada. `Run` zera velocidade ao encerrar em repouso; em captura mantém a velocidade física do instante, com motivo terminal `Captured`. O visual pode animar o desaparecimento da bola sem continuar a simulação dessa tacada. No limite de rebotes, retorna à partida com superfície inicial correta, motivo `BounceLimitReset`; na sétima tentativa fracassada acrescenta a penalidade +2. Nenhum desses eventos escolhe outro jogador ou altera uma partida de rede.

As verificações incluem resultados analíticos, rampas, rebote oblíquo, paredes de Splitt, captura estrita, penalidade e repetição de uma tacada no mesmo runtime. Isso não comprova equivalência com o original nem determinismo bit a bit entre máquinas.

## Limite já encontrado: cantos e passagens

Preservamos a ordem documentada das arestas e a passagem pendente mesmo após um rebote posterior. Isso pode devolver posição temporariamente fora do polígono da superfície selecionada. Há um caso explícito de regressão para esse comportamento, em vez de escondê-lo com uma correção automática.

A primeira bateria de 864 tacadas encontrou 111 trajetórias com esse diagnóstico, 110 começando por `PassageSelected` seguido de `WallBounce`, e uma por passagem para um vizinho cujos limites a posição já ultrapassou. Não houve erro de execução ou limite de passos nessa bateria. Esse resultado confirma a necessidade de revisar cantos/transições antes de adotar o núcleo em um jogo final; **não comprova que o original produz as mesmas trajetórias**.

Próximo passo: comparar os casos representativos com o original e definir uma política moderna para canto/passagem. Essa alteração deverá receber outra versão física e novos resultados de referência.

Atualização: a política experimental versionada foi implementada e comparada sem substituir o perfil anterior. Ela teve zero diagnósticos de contenção na bateria de 864 tacadas e passou em 214.320 testes locais de geometria. Das mesmas 864 tacadas, 214 posições finais mudam acima de 1e-6, algumas bastante. Ver [relatório comparativo](../docs/15-validacao-de-cantos.md) antes de escolher uma política como definitiva.
