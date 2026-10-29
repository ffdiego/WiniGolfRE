# Primeiro simulador executado

2026-10-28. Implementado em C# usando o SDK .NET 10.0.402 disponível no workspace; execução em runtime 10.0.13, Windows x64. Código em [simulator/](../simulator/) e instruções em [simulator/README.md](../simulator/README.md).

O núcleo e o console são projetos separados. Não houve acesso de escrita ao executável ou banco do Ghidra, execução do jogo original, implantação de servidor nem dependência de engine gráfica. Os dados são os JSON já extraídos. Compilação e execução ocorreram localmente; não se baixaram pacotes externos.

## Resultados desta execução

- **72/72 verificações aprovadas**, incluindo carregamento dos 36 campos e os casos analíticos da especificação. Relatório: [verification.json](../artifacts/simulator/verification.json).
- Basic, origem (-250,0,10), impulso (10,0): **Captured no passo 107**, x=192,434788659962, superfície 2. Tempo nominal 2,14 s. [Trajetória](../artifacts/simulator/basic.json), [CSV](../artifacts/simulator/basic.csv), [planta](../artifacts/simulator/basic.svg).
- Dip, mesmo impulso: **Stopped no passo 390**, x≈-18,1120068, superfície 3. Tempo nominal 7,80 s. [Trajetória](../artifacts/simulator/dip.json), [planta](../artifacts/simulator/dip.svg).
- Splitt, origem (0,201,10), superfície 4, impulso (0,-2): **Stopped no passo 183**, y≈203,8507693, dois rebotes, ainda na superfície 4. [Trajetória](../artifacts/simulator/splitt.json), [planta](../artifacts/simulator/splitt.svg).

São resultados **do novo simulador**, não medições do original. A velocidade/posição residual em eixos quase nulos pode conter valores muito pequenos por trigonometria em double.

## Bateria e diagnóstico de cantos

[sweep.json](../artifacts/simulator/sweep.json) cobre 864 tacadas: cada campo, oito direções em incrementos de 45° e velocidades iniciais de módulo 2,5, 10 e 15. Os limites são 6000 passos por tacada. A bateria usa impulsos físicos diretos; não representa uma distribuição de usuários nem está limitada aos movimentos exatos de mouse de um modo específico.

Resultado: 848 paradas, 16 capturas, zero erros de execução e zero limites de passos atingidos. **111 trajetórias têm pelo menos um estado fora da superfície selecionada**; não é apenas resíduo de precisão: o maior afastamento normal a uma aresta é aproximadamente 9,55 unidades.

O primeiro estado inconsistente vem de passagem seguida de parede em 110 trajetórias. Exemplo: em Dip, selecionar vizinho ao cruzar a aresta direita e depois restaurar posição por colisão com a parede inferior mantém a passagem pendente. O estado pode estar ainda no lado antigo, mas usar a rampa vizinha no passo seguinte. É consequência compatível com a ordem de código preservada, não evidência de que o executável original foi observado fazendo o mesmo.

Na outra trajetória, em Octagon, a posição atravessa uma passagem e já fica fora de outro limite do vizinho, que só será testado no passo seguinte. O protótipo mantém apenas a troca de superfície ao fim do passo, conforme a referência estática.

O relatório preserva velocidade de entrada, primeiro passo fora, estado, eventos e maior distância externa para cada tacada. Esses casos são a próxima prioridade de validação/correção. Verificações aprovadas significam conformidade com o contrato documentado, **não ausência de defeitos físicos**: um dos testes fixa expressamente o caso legado de passagem pendente para torná-lo visível.

## Diferenças deliberadas e limites

Implementado o perfil moderno proposto na especificação: double persistente, recomposição planar sem tangentes, som sem efeito na física, superfície inicial restaurada ao limite de rebotes, captura antes de parada e evento terminal separado de turno. O limite de rebotes encerra a tacada com `BounceLimitReset`; a penalidade de sétima tentativa também é aplicada nessa conclusão fracassada. No original essa conclusão passa pelo fluxo de parada; é uma definição explícita do protótipo.

O clamp da mira é contínuo, sem reproduzir arredondamento de endpoint da VCL; este aspecto exige comparação posterior. `vz` deve ser zero. A geometria é validada antes da simulação e o slot 7 de Splitt continua ausente, não preenchido com zero.

As saídas não alteram os JSON de evidência. Não há garantia de igualdade numérica entre plataformas nem modo online implementado. A separação da biblioteca permite futuramente usar o mesmo núcleo no cliente e servidor, com autoridade e reconciliação ainda a implementar.
