# Primeiro cliente jogável C# + raylib

**Atualização da etapa 21:** o cliente atual usa exclusivamente [projeção ortográfica e virada entre tacadas](21-camera-ortografica-e-virada.md). Perspectiva e atalho P descritos no histórico inicial foram removidos.

Implementado em 28/10/2026. Projeto `simulator/src/Winigolf.Game`, separado do laboratório Windows Forms e do console. Reutiliza diretamente `Winigolf.Physics`; não altera o algoritmo da física, os JSON extraídos, o executável original ou o banco Ghidra.

## Executar

No Windows, com SDK .NET 10:

- Abrir [abrir-jogo.cmd](../simulator/abrir-jogo.cmd).
- Ou, na raiz deste repositório: `dotnet run --project simulator/src/Winigolf.Game`.

Raylib-cs 8.1.0 / raylib 6.0 são restaurados do NuGet.org; os textos de licença estão em [simulator/licenses](../simulator/licenses/). O primeiro build precisa baixar o pacote. Testado no Windows x64 deste workspace; não foram validadas outras plataformas, nem se criou instalador ou distribuição autossuficiente.

## O que já é jogável

- Os **36 campos** usam os mesmos dados extraídos. Basic, Dip e Splitt têm atalhos no topo por cobrirem plano, rampas e paredes internas; os demais estão no seletor de pistas.
- A geometria é triangulada a partir dos polígonos convexos. Toda aresta com vizinho zero produz parede visual, incluindo as internas de Splitt. IDs de vértices sem uso não recebem coordenadas inventadas.
- Esfera, bandeira, cores e espessuras são arte procedural de protótipo, não assets recuperados. Paredes aparentam espessura/altura, mas a colisão continua sendo o modelo 2D por arestas do núcleo, sem raio de esfera.
- Esquerdo inicia precisão, direito inicia força; soltar o mesmo botão dispara. A puxada é no sentido oposto ao lançamento. Esc cancela. O modo forte tem linha mais grossa.
- Câmera inclinada ortográfica e planta, com virada automática entre tacadas conforme etapa 21. Enquadramento automático considera os vértices projetados, sem cortar o campo no zoom padrão. Zoom manual pode cortar intencionalmente partes da pista.
- Cada tacada conta uma vez. Ao capturar, a esfera converge visualmente ao centro e desaparece em 0,35 s. A posição final física continua a de `ShotResult`, sem snapping no estado autoritativo.
- Conclusão libera “Próximo campo” / Enter. O limite de tentativas e a penalidade vêm do núcleo. Após a pista 36, a seleção volta à primeira. Resultados concluídos aparecem no seletor e ficam apenas em memória nesta sessão; uma nova conclusão no mesmo campo substitui seu resultado anterior.

| Controle | Ação |
|---|---|
| Esquerdo na bola, puxar e soltar | Modo 1, precisão |
| Direito na bola, puxar e soltar | Modo 2, força |
| Esc | Cancelar mira / fechar seleção |
| V ou botões de câmera | Alternar inclinada/planta |
| Q / E ou arraste com botão do meio | Girar vista inclinada |
| Roda sobre a cena | Zoom |
| Espaço | Pausar/continuar animação |
| R | Recomeçar campo |
| Tab | Abrir/fechar seleção dos 36 campos |
| Enter após conclusão | Avançar campo |
| F2, diagnóstico | Trocar perfil de colisões; reinicia campo e limpa resultados da sessão |

Atualização: foram adicionados os seis efeitos sonoros originais e ganho ajustável do esquerdo. Após a [correção da curva de tacada](20-correcao-da-curva-de-tacada.md), o padrão voltou a **1× da fórmula corrigida**. O reforço temporário de 2× pertence à [etapa 19](19-sons-originais-e-ajuste-da-mira.md). M alterna o som; o painel oferece 1×/2×/3× para o esquerdo. Espaço continua pausando por teclado. A [etapa 21](21-camera-ortografica-e-virada.md) acrescenta câmera aleatória entre tacadas e remove a perspectiva. Não há rede, partidas com vários jogadores ou salvamento de progresso.

## Separação entre simulação e apresentação

`GameSession.Shoot` chama `Simulator.Run` para obter a tacada inteira, com limite operacional de 6.000 passos. `Advance` move apenas o tempo de reprodução. O passo físico continua **20 ms**. A apresentação interpola posições entre quadros e tenta desenhar a esfera apoiada na rampa, evitando exibir o atraso legado da altura. Nem a interpolação nem a animação de captura modificam posições, velocidades, eventos ou superfícies de `ShotResult`.

O desenho solicita até **120 quadros/s**, com sincronização vertical; a taxa efetiva depende do monitor/GPU e não foi certificada. Polígonos são desenhados pelo raylib em um viewport com profundidade e supersampling 2× em cada dimensão. `CourseScene` prepara triângulos de geometria estática ao trocar campo; `SceneCamera` cuida de projeção e mira, fora da física.

Coordenadas de renderização são `(x,z,-y)`, mantendo os números originais. A ordem dos triângulos é invertida para que a face de cima tenha normal positiva no novo eixo vertical. As distâncias de recorte do renderizador são ajustadas às unidades dos campos.

### Mira em uma câmera moderna

Na planta e na câmera ortográfica, a conversão usa a inversa da projeção local no plano horizontal da bola e equivale à compensação angular recuperada, conservando ganhos 0,1/0,8, limites 250/50 e, após a errata da etapa 20, resposta proporcional a **L^1,5**. O deslocamento de tela é normalizado pela escala local antes dos limites.

Na perspectiva, essa inversa é uma **aproximação local por Jacobiana**, calculada pela projeção de deslocamentos unitários nos eixos x/y da bola. É uma decisão moderna, porque a projeção recuperada do original não tem divisão por profundidade. O comando resultante é um vetor canônico em coordenadas físicas. Não se afirma equivalência pixel a pixel com o mouse original, nem força idêntica sob qualquer mudança de perspectiva. A câmera pode mudar a interpretação da entrada, mas nunca os estados de uma tacada já calculada.

### Perfil físico

O cliente jogável inicia em **`surface-traversal-2`**, escolha explícita para evitar as inconsistências de cantos diagnosticadas no perfil de referência. O laboratório mantém comparação e exportação dos dois. F2 permite conferir a referência no próprio cliente, reiniciando o campo e descartando resultados para não misturar pontuações entre versões físicas. Essa escolha inicial não encerra a validação de sensação com o original.

## Verificação e confiança

Na raiz:

```powershell
dotnet run --project simulator/src/Winigolf.Game -- --self-test artifacts/game
```

**204 verificações aprovadas**, usando uma janela raylib/OpenGL oculta real, não um renderizador simulado. [Relatório completo](../artifacts/game/checks.json).

Esse foi o resultado inicial; após a revisão de som e mira, o relatório atual contém **235 verificações aprovadas**, incluindo as anteriores.

- Reprodução independente da divisão dos intervalos de relógio; interpolação sem modificar o quadro físico; pausa; captura e contagem sem duplicação; penalidade e progressão.
- Entrada pelas mesmas funções de mira da interface: clique fora, puxada zero/cancelada, soltura do outro botão, modo forte e captura em Basic.
- Correspondência com `Aim.FromDrag` em planta e na vista ortográfica inclinada de −60°; direção física consistente em cinco rotações da câmera perspectiva.
- Para cada um dos 36 campos: quantidade de paredes únicas conferida contra vizinhos zero; vértices dentro do viewport/recorte no zoom padrão; leitura de pixels verdes reais do framebuffer nas vistas perspectiva e planta. São 72 combinações de campo/vista.

Capturas do próprio cliente:

| Campo | Perspectiva | Planta |
|---|---|---|
| Basic | [imagem](../artifacts/game/01-perspective.png) | [imagem](../artifacts/game/01-top.png) |
| Dip | [imagem](../artifacts/game/03-perspective.png) | [imagem](../artifacts/game/03-top.png) |
| Splitt | [imagem](../artifacts/game/25-perspective.png) | [imagem](../artifacts/game/25-top.png) |

[Basic após captura](../artifacts/game/basic-captured.png).

**Confiança alta** na integração testada com o núcleo e na presença da geometria nas vistas exercitadas. **Ainda não determinado** o grau de semelhança da sensação de mira, dos tempos e das trajetórias com o jogo original em execução. Não são testes de um usuário real interagindo com o Windows; não certificam todos os ângulos/zooms possíveis, desempenho em outras GPUs ou determinismo bit a bit entre máquinas. Não houve nova inferência de endereços: a proveniência da física permanece nos documentos 13 e 15–17.

Próxima validação recomendada: experimentar primeiro Basic, Dip e Splitt, alternando planta e câmera inclinada. Comparar com o original puxadas curtas, passagem lenta pelo buraco, reversão em rampa e rebotes na parede interna. Usar o laboratório para inspecionar a física quando surgir uma diferença, antes de acrescentar rede ou efeitos gráficos.
