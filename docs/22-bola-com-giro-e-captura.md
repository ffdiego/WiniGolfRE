# Bola com giro e queda no buraco

A apresentação do cliente raylib usa uma esfera de raio constante 4, com manchas escuras distribuídas sobre a superfície. A orientação é acumulada conforme o deslocamento visual, com ângulo distância/raio e eixo perpendicular à direção de movimento. O sombreamento acompanha a orientação no mundo. O giro é apenas visual, sem alterar a simulação física.

A captura dura 0,65 s: nos primeiros 35% a bola se alinha ao centro; depois, desce com deslocamento quadrático. Os triângulos abaixo da boca são recortados durante a queda, escondendo progressivamente a esfera. O raio não diminui. A sombra de contato desaparece quando começa a descida.

Implementação em `BallVisual.cs`, integração em `CourseScene.cs` e temporização em `GameSession.cs`. Ao reconstruir a pista, a orientação visual é reiniciada.

Validação: build Release sem avisos ou erros; 567 verificações existentes do cliente aprovadas. Captura estática inspecionada para conferir a esfera com marcas. A bateria existente verifica integração e captura terminal; não mede a qualidade perceptiva do giro nem a animação intermediária da queda.
