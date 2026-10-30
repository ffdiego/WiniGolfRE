# Escala fixa durante o giro e zoom suave

A câmera ortográfica usa os limites esféricos da pista para definir uma escala que comporta qualquer ângulo. O ajuste iterativo aos vértices projetados foi removido: girar não altera mais a ampliação. Pistas compridas podem deixar mais espaço livre em algumas orientações.

A roda do mouse, sobre a área da pista, altera o zoom desejado em 15% por passo, limitado a 0,5×–3×. A aproximação ao destino usa amortecimento exponencial independente da frequência dos frames. Ao iniciar a mira, a escala é fixada para evitar deslocamento da bola sob o cursor durante a puxada. A troca de pista restaura 1×.

Validação: build Release sem avisos nem erros e 1433 verificações aprovadas. Os novos casos cobrem escala constante nos 36 campos em 24 ângulos e convergência do zoom com diferentes intervalos de frame. Nenhuma alteração na física.
