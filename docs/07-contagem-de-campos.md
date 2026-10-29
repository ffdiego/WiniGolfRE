# Contagem e viabilidade da extração

2026-10-28. **36 campos selecionáveis, confiança alta.**

Evidências independentes:

- `0x0041FB40`: progressão/sorteio usa limite `0x24` (36), módulo 36 e percurso de índices 1 até antes de `0x25` (37). O pseudocódigo foi preservado em `evidencias/reconhecimento-ghidra.json`.
- `0x00426A68`: seleção individual trata índices de 1 até `0x24`, associados a 36 componentes consecutivos entre objeto+`0x2D0` e `+0x35C`. A rotina usa o índice para obter nome/par e chamar `0x00420074`. Evidência em [selecao-campos-00426a68.json](evidencias/selecao-campos-00426a68.json).
- Na apresentação de `0x00426A68`, o valor em objeto principal+`0xC7F5C + indice*4` é concatenado com a string `par` em `0x004915BA`. Isso confirma como par o parâmetro antes descrito como candidato; em Basic ele vale 2.

O formato comum e o loader compartilhado tornam a extração em lote viável. Recuperar dados de geometria, ligações, partida, alvo, nome e par parece trabalho predominantemente mecânico. Ainda não foram extraídos/validados os 36 campos: apenas Basic tem extração reproduzível concluída. Não foi demonstrado que todos os blocos de inicialização usam apenas as mesmas instruções nem descartadas alterações posteriores ou comportamentos específicos.

É necessário distinguir dados completos de comportamento completo: rampas, transições, condições especiais e fidelidade física exigem análise/validação além de exportar vértices. Próxima etapa possível: estender o extrator por blocos, falhar em instruções desconhecidas e validar contagens, índices, ligações e alturas de cada campo, com relatório de exceções.
