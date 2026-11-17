# Cliente WebAssembly experimental

O projeto `simulator/src/Winigolf.Web` executa uma partida local do campo 01 (Basic). Ele referencia `Winigolf.Physics` sem alterações e compila diretamente os arquivos compartilhados de `Winigolf.Game`: `GameSession`, `CourseScene`, `SceneCamera`, `DynamicFramingCamera`, `TurnPresentation`, `BallVisual`, `CaptureMotion` e suas dependências. O servidor ASP.NET `OnlineHost`, áudio e interface desktop não são incluídos. Nenhum arquivo do cliente desktop foi alterado.

`Host.cs` inicializa raylib, carrega `courses/01.json` do sistema de arquivos virtual e expõe métodos com `JSExport`. `main.js` associa o canvas ao módulo, inicializa o runtime e dirige os quadros com `requestAnimationFrame`; o delta é limitado a 100 ms para evitar saltos após suspensão. A simulação continua usando `GameSession.Shoot`, `Simulator.Run`, os quadros de 20 ms e `VisualPosition` do desktop. A câmera, transições, geometria da pista, bola e animação de captura são as mesmas classes do desktop. A mira usa `SceneCamera.Impulse` no modo 1. Um arrasto pode começar em qualquer ponto do canvas, com linha e força visíveis; o botão reinicia o campo. O canvas usa Pointer Events, captura do ponteiro e `touch-action: none`.

Os arquivos novos são `Winigolf.Web.csproj`, `Host.cs`, `index.html` e `main.js`, além deste documento. O projeto empacota o JSON usando `WasmFilesToIncludeInFileSystem` e a página com `WasmExtraFilesToDeploy`. A fonte padrão do raylib não requer fontes do Windows.

## Dependências e compilação

Instale o SDK .NET 10 e o workload `wasm-tools` correspondente ao SDK. O workload fornece Emscripten; não é necessário instalar um EMSDK separado. Raylib-cs 8.1.0 inclui a biblioteca nativa web e os targets de link. O projeto segue o [exemplo oficial Web](https://github.com/raylib-cs/raylib-cs/tree/main/Examples/Web): `WasmBuildNative`, runtime autônomo e canvas associado antes de `runMain`.

Na raiz do repositório:

```powershell
dotnet workload install wasm-tools
dotnet restore simulator/src/Winigolf.Web/Winigolf.Web.csproj
dotnet build simulator/src/Winigolf.Web/Winigolf.Web.csproj -c Release
dotnet publish simulator/src/Winigolf.Web/Winigolf.Web.csproj -c Release -r browser-wasm
```

O bundle fica em `simulator/src/Winigolf.Web/bin/Release/net10.0/browser-wasm/AppBundle/`. Como no exemplo oficial, o link usa `-O0`, sem strip nem mapa de símbolos: uma combinação de SDK e workload observada neste ambiente rejeitou `--enable-bulk-memory-opt` ao gerar o mapa. Atualizar o workload pode corrigir a incompatibilidade; não foi comprovado aqui que uma atualização a corrija.

Sirva o diretório por HTTP, por exemplo com Python instalado:

```powershell
python -m http.server 8000 --bind 0.0.0.0 --directory simulator/src/Winigolf.Web/bin/Release/net10.0/browser-wasm/AppBundle
```

Abra `http://localhost:8000/` no computador. No celular conectado à mesma rede, abra `http://IP-DO-COMPUTADOR:8000/`; obtenha o IPv4 com `ipconfig` no Windows e permita a porta no firewall se necessário. O servidor entrega apenas arquivos estáticos; não há servidor de jogo. Não abra o HTML diretamente como `file://`.

## Renderização e limites

O buffer é fixo em 960 × 600, escalado por CSS preservando a proporção; o device pixel ratio não multiplica a memória. A cena desenha diretamente no canvas, sem a render texture desktop com supersampling 2× por dimensão, e aplica os mesmos planos de clipping (1 a 20000). O custo e a nitidez em dispositivos reais ainda precisam ser medidos. As chamadas 3D usadas pela pista e bola foram observadas no Chrome WebGL 1 com renderização por software; `DrawSphereEx` para outros jogadores e render textures não são usados neste protótipo e não foram testados aqui. O áudio está desativado. A simulação de cada tacada é calculada sincronicamente, podendo provocar pausas em celulares lentos. Há apenas um campo e um jogador, sem seleção de modo, progressão, multiplayer ou persistência.

O canvas é centralizado no espaço disponível, com áreas seguras do dispositivo e redimensionamento ao girar a tela. O cliente restaura o buffer para 960 × 600 quando o navegador o altera: em um teste móvel emulado, ele havia sido reduzido às dimensões CSS, cortando a cena que o raylib ainda desenhava na resolução original.

O indicador no cabeçalho mostra a frequência dos callbacks `requestAnimationFrame` e a duração média e máxima da chamada C# por quadro, atualizadas aproximadamente a cada segundo. A duração mede o tempo decorrido na chamada, não o tempo total de GPU nem o cálculo síncrono da tacada no evento de ponteiro. Um FPS próximo de 60 com tempo C# baixo pode indicar limitação externa ao código do quadro; por si só, o indicador não identifica a causa. Para medir a taxa do monitor, mantenha a aba visível e confira a frequência configurada no sistema.

No navegador, `WebBallMesh` gera uma vez a mesma geometria e manchas da bola e mantém a malha na GPU. A rotação continua acompanhando o deslocamento. A iluminação fica gravada na malha e gira com ela, portanto não acompanha uma luz fixa como no desktop; na captura, o desenho original por triângulos é usado para recortar a bola na borda do buraco. A malha dura enquanto a página estiver aberta. No Chrome headless com WebGL por software nesta máquina, a amostra do indicador passou de 49 FPS e 20,3 ms médios por quadro para 142 FPS e 0,6 ms após a mudança; os valores dependem do ambiente e não são uma garantia para outros dispositivos. Uma tacada também foi repetida sem erro exibido. A captura no buraco e o desempenho em aparelho físico não foram retestados após essa mudança.

## Validação nesta máquina

- SDK 10.0.404 no Windows; restore, build e publish Release `browser-wasm` concluídos. O AppBundle contém `index.html`, `main.js`, `_framework` e o runtime; o carregamento do campo foi confirmado na execução.
- A instalação de `wasm-tools` foi interrompida por reinício do ambiente e não apareceu em `dotnet workload list`; os packs já instalados permitiram build e publish. Em uma instalação limpa, execute o comando de instalação acima e confirme o workload.
- Servidor HTTP local e Chrome headless com WebGL por software: a página carregou, pista, bola, buraco e bandeira apareceram. Um arrasto de mouse produziu a primeira tacada e a bola mudou de posição; sem erro exibido. Capturas estão em `artifacts/web-test/` se os artefatos locais ainda estiverem presentes.
- `dotnet build simulator/src/Winigolf.Game/Winigolf.Game.csproj -c Release`: sucesso, zero avisos.
- `dotnet run --project simulator/src/Winigolf.Simulator/Winigolf.Simulator.csproj -c Release -- verify`: 96/96 verificações, 36 campos.
- Após corrigir o buffer, um arrasto de toque no Chrome com viewport móvel emulado registrou uma tacada, e o canvas permaneceu centralizado e com o buffer correto após girar para paisagem. Aparelho físico, desempenho móvel, outros navegadores e regressão em Linux não foram testados.

Próximos passos: testar toque e desempenho em aparelhos reais, revisar resolução e dimensionamento, medir pausas da simulação, validar retomada após suspensão, ampliar seleção de campos e UI, e testar os recursos gráficos e áudio que forem adicionados.
