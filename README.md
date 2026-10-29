# Reconstrução do Winigolf

Estudo incremental de um minigolfe freeware antigo para Windows 32 bits e protótipo moderno em C#/.NET 10.

## Conteúdo

- [`simulator/`](simulator/README.md): núcleo físico, simulador de linha de comando, laboratório Windows Forms, cliente raylib e ferramenta de auditoria da mira.
- [`docs/`](docs/README.md): notas da engenharia reversa, especificação física, evidências estáticas e dados dos 36 campos.
- [`artifacts/`](artifacts/): relatórios e capturas de versões específicas dos testes citados pela documentação. São evidências de execução, não saídas necessárias para compilar.

## Executar no Windows

Instale o SDK .NET 10. Na raiz deste repositório:

```powershell
dotnet build Winigolf.sln
dotnet run --project simulator/src/Winigolf.Simulator -- verify
dotnet run --project simulator/src/Winigolf.Game
```

`Winigolf.sln` reúne o núcleo físico, o console, o laboratório visual, o cliente raylib e a ferramenta `AimAudit`; também pode ser aberto no Visual Studio.

Ou abra `simulator/abrir-jogo.cmd`. Para o laboratório de análise, use `simulator/abrir-laboratorio.cmd`. O restore baixa `Raylib-cs` e `Iced` do NuGet.org nas versões fixadas pelos projetos, portanto o primeiro build desses projetos precisa de rede. Os textos das licenças estão em `simulator/licenses/`.

As capturas e relatórios em `artifacts/` documentam resultados históricos. Para gerar novos resultados, passe um caminho de saída sob `artifacts/`; os diretórios `bin/`, `obj/` e `.cache/` são locais e ignorados pelo Git.

## Limites e origem dos dados

O executável original, o arquivo de ajuda, a música MIDI, o projeto/banco do Ghidra e os caches da análise permanecem fora deste repositório. Os dados JSON dos campos e as conclusões documentadas foram extraídos/analisados a partir do executável; veja as notas de método e confiança em `docs/`.

Os seis WAVs em `simulator/assets/audio/` foram extraídos sem conversão dos recursos do jogo. Não foi identificada uma licença de redistribuição para esses sons. Este repositório está privado por enquanto; reavalie ou remova esses recursos antes de torná-lo público.

O código deste protótipo ainda não tem uma licença de distribuição declarada. A presença de um jogo freeware como objeto de estudo não implica uma licença para o executável ou seus recursos originais.
