# Opções de câmera e foco no início da tacada

`simulator/src/Winigolf.Game/CameraOptions.cs` concentra as escolhas de apresentação. Padrões:

- `CenterOnBallAtTurnStart = true`: centraliza no centro visual da bola antes de cada tacada e conserva esse alvo durante seu movimento.
- `AllowTopDown = false`: oculta os botões de troca de vista e desabilita o atalho V.
- `AllowManualRotation = false`: desabilita Q/E e arraste com botão central.
- `AutoRotateBetweenShots = true`: preserva a virada automática entre tacadas.

Os padrões podem ser alterados na classe ou substituídos passando `CameraOptions` ao construtor de `GameApp`. Zoom suave pela roda permanece disponível. A escala continua independente da rotação; centralizar na bola pode deixar partes da pista fora da tela.

Build Release sem avisos nem erros. 1438 verificações aprovadas, incluindo foco projetado no centro, configurações alternativas e preservação do foco durante a tacada. A verificação de enquadramento de todos os vértices se aplica ao modo opcional de foco na pista inteira.
