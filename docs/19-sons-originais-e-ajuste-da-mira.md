# Sons originais e ajuste de força do esquerdo

**Nota posterior:** a [etapa 20](20-correcao-da-curva-de-tacada.md) encontrou um erro na curva de conversão que este ganho apenas compensava parcialmente. O padrão atual é 1× da fórmula corrigida, proporcional a L^1,5. A descrição abaixo registra a etapa intermediária; a extração e o agendamento sonoro continuam válidos.

Revisão de 28/10/2026, após experimentação do primeiro cliente raylib. Objetivo: incorporar os efeitos do jogo e tornar o esquerdo mais útil sem alterar as fórmulas do núcleo de referência.

## Recuperação dos sons

[extract-sounds.cjs](scripts/extract-sounds.cjs) lê o PE como arquivo, valida o SHA-256 conhecido e percorre o diretório de recursos. Não carrega nem executa WiniGolf. Da raiz do repositório, espera o executável original em `../WiniGolf/WiniGolf.exe`. Foram encontrados **seis recursos WAVE**, copiados integralmente para `simulator/assets/audio/`, sem conversão. [Manifesto com RVA, offset no arquivo, idioma, SHA-256 e formato de cada recurso](dados/sons-embutidos.json).

| Recurso original | Arquivo | Uso no cliente | Evidência e confiança |
|---|---|---|---|
| HIT | `hit.wav` | Ao aceitar a tacada | Alta: MouseUp `0x0041DF0C`, já analisado no documento 03 |
| BOUNCE | `bounce.wav` | Um som por quadro com rebote em parede | Alta no vínculo: `0x0041D2A4`, chamada `PlaySoundA("Bounce")` no ramo de parede |
| APPLAUSE | `applause.wav` | Captura com tacadas abaixo do par | Alta: estado 3 em `0x0041E654`, compara contagem `< par` |
| CLAP | `clap.wav` | Captura no par ou acima | Alta: outro ramo do mesmo teste; string em `0x0048E72D` |
| LAUGH | `laugh.wav` | Penalidade por limite de tentativas | Alta: `0x0041ECB8`, ramo 7→9 e +2 |
| UH | `uh.wav` | Passagem rápida na região do buraco, uma vez por tacada | Alta no vínculo ao teste rápido em `0x0041ECB8`, string em `0x0048E738`; frequência de uma vez por tacada é política moderna explícita |

Novas leituras somente de consulta em [efeitos-sonoros-ghidra.json](evidencias/efeitos-sonoros-ghidra.json). No original, flags de reprodução do Windows e contadores de apresentação também afetam quando um efeito se ouve. O cliente conserva os arquivos e os eventos de jogo, sem tentar reproduzir exatamente a interrupção/prioridade do `PlaySoundA` legado.

Os formatos são PCM mono: HIT/APPLAUSE/CLAP a 11.025 Hz e 8 bits; BOUNCE a 22.050 Hz e 16 bits; LAUGH a 8.000 Hz e 8 bits; UH a 11.000 Hz e 8 bits. Raylib decodifica todos diretamente. Não se adicionou música MIDI nesta etapa.

## Apresentação sonora

`SoundTimeline` monta uma fila a partir dos quadros físicos imutáveis. `GameAudio` consome os efeitos conforme o tempo de reprodução, não durante o cálculo antecipado de `Simulator.Run`. Isso impede ouvir todos os rebotes imediatamente ao soltar o mouse.

- Tacada soa no quadro zero; rebotes seguem os respectivos quadros; reação/captura/penalidade seguem suas condições.
- Quadro redesenhado não repete efeito. Vários rebotes no mesmo quadro resultam em um único efeito de parede. Portais não produzem esse som.
- Pausar a jogada pausa sons ativos. Trocar/reiniciar o campo interrompe os sons e descarta a fila anterior.
- M ou o botão no topo liga/desliga os efeitos. Eventos já passados são consumidos mesmo em silêncio; reativar não dispara uma fila antiga.
- Após um atraso de renderização, efeitos com mais de 150 ms de atraso são descartados, evitando uma rajada de sons antigos. É uma política de apresentação, sem alterar a física.
- Falta de dispositivo de áudio não impede jogar; a interface informa indisponibilidade. Não há exigência de áudio no núcleo físico.

## Força ajustável

O usuário considerou o modo esquerdo ainda fraco após a correção do botão. Em vez de alterar `Aim.FromDrag`, `SceneCamera.Impulse` ou as constantes recuperadas, o cliente aplica um ganho **somente ao vetor inicial gerado pelo esquerdo**:

```text
impulsoEsquerdo = ganhoSelecionado × impulsoModo1
ganhoSelecionado ∈ {1, 2, 3}; padrão = 2
impulsoDireito = impulsoModo2, sem alteração
```

O painel oferece 1×/2×/3×. 1× restaura o comportamento da versão jogável anterior; 2× é o novo padrão; 3× oferece mais alcance. O ganho permanece ao trocar/reiniciar campos, mas não é salvo entre execuções. Para uma puxada horizontal equivalente a 25 unidades na planta, as velocidades iniciais do esquerdo são aproximadamente 1,25 / 2,5 / 3,75 unidades por passo; o direito continua aproximadamente 10. O ganho não transforma a curva em linear: a resposta de raiz do comprimento permanece.

**Esta é uma escolha de jogabilidade moderna, não uma constante descoberta no original.** O laboratório e o console continuam com a conversão de referência. O vetor resultante entra no mesmo núcleo de física; atrito, rampas, colisões e captura não receberam ajustes. No futuro online, o perfil de entrada e seus limites terão de fazer parte do contrato validado pelo servidor.

## Verificação e limites

**235 verificações aprovadas** no cliente: [relatório](../artifacts/game/checks.json). Além das verificações anteriores, cobrem fila por quadro, ausência de repetição, aplausos em relação ao par, penalidade, reação rápida, rebotes, batching de quadros, descarte ao trocar campo, hashes dos seis recursos, decodificação WAV pelo raylib e ganho real do esquerdo pelas mesmas entradas usadas pela interface. Confirmado que mudar o ganho do esquerdo não muda o direito.

O dispositivo real de áudio inicializou e os seis objetos de som carregaram neste ambiente (`audioDeviceReady: true`, `loadedSoundCount: 6`). A bateria roda **silenciada**; isso valida inicialização, decodificação e agendamento, não uma avaliação auditiva humana nem o volume percebido nos alto-falantes do usuário.

Confiança alta na extração sem alterações e nos vínculos de eventos indicados. A semelhança auditiva percebida e a sensação do novo ganho ainda dependem de experimentação. Nenhum arquivo do núcleo físico, executável original ou banco Ghidra foi alterado.
