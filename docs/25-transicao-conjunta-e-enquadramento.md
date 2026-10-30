# Transição conjunta e enquadramento durante a tacada

O alvo da câmera agora interpola da posição atual até o centro da bola usando o mesmo tempo e a mesma curva suave do giro automático. A mira é liberada ao concluir ambos. Sem giro automático, a recentralização usa uma transição de 0,5 s.

Durante o movimento da bola, a câmera conserva seu alvo enquanto o centro visual da bola permanece na área central de 70% da largura e altura. Ao entrar nas margens de 15%, o alvo acompanha apenas o excesso, com amortecimento exponencial. Um limite externo de 3% preserva visibilidade quando a velocidade ou o zoom superam o acompanhamento suave. A câmera não muda o zoom nem a simulação física.

Configurações em `CameraOptions.cs`: `KeepBallInFrame`, `EdgeMargin` (padrão 0,15) e `FollowSpeed` (padrão 8). A pausa congela o acompanhamento e a transição. O foco é atualizado apenas na apresentação.

Validação: build Release sem avisos ou erros; bateria gráfica e de integração com 1443 verificações. Novos casos verificam transição sem salto inicial, sincronismo no meio e fim do giro, câmera parada na área central e redução do deslocamento fora da área segura.
