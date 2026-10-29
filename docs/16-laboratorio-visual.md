# Laboratório visual de física

Implementado em 28/10/2026, em `simulator/src/Winigolf.Viewer`. É uma ferramenta local de investigação e comparação, não a versão final do jogo. Usa C#/.NET 10 e Windows Forms, sem pacotes externos. O núcleo `Winigolf.Physics` continua independente da interface e reutilizável em outro cliente ou servidor.

## Abrir e experimentar

Executar [abrir-laboratorio.cmd](../simulator/abrir-laboratorio.cmd), com o SDK .NET 10 instalado no Windows, ou, na raiz deste repositório:

```powershell
dotnet run --project simulator/src/Winigolf.Viewer
```

1. Escolher um dos 36 campos. Clicar perto da bola, puxar e soltar para bater. Esquerdo usa precisão (modo 1); direito usa força (modo 2). Esc cancela a puxada.
2. Usar pausa, avanço de 20 ms ou a barra de tempo para inspecionar uma tacada. Com foco no campo: Espaço pausa, seta direita avança, R reinicia.
3. Trocar o perfil físico para recalcular a **mesma origem, impulso e número da tacada** no outro perfil. Trocar de campo limpa a tacada e a contagem.
4. Ativar a sobreposição: ciano é o perfil selecionado; âmbar é a trajetória completa do outro perfil. A trajetória selecionada normalmente mostra só o trecho já reproduzido; a opção “Trajetória completa” libera sua previsão inteira.
5. Usar “Caso de depuração do campo” para uma origem controlada. Em Splitt, começa em `(0,201,10)`, superfície 4, impulso `(0,-2)`, junto à parede interna. Em Plateau, usa a origem do campo e impulso `(-15,1.83697019872103e-15)`, que evidencia a divergência dos perfis. Nos demais campos usa impulso `(10,0)` na origem extraída.
6. Exportar a tacada em JSON para guardar versão física, hash do campo, origem, estados/eventos por passo, término e contagem. A exportação contém a tacada inteira calculada, mesmo com a reprodução pausada no início.

O painel mostra posição, altura, velocidade, módulo da velocidade, superfície, rebotes, passo, tempo simulado e tacadas. Linhas contínuas claras são paredes; linhas tracejadas entre superfícies são portais. O contorno verde destaca a superfície física atual. Há vista de cima e projeção inclinada de −60°. A vista de cima facilita ler paredes internas.

## Contratos da interface

- Cada tacada chama `Simulator.Run` uma vez, com limite operacional de 6.000 passos (120 segundos simulados). O relógio da janela apenas reproduz os quadros retornados. Pausa, velocidade 0,25×/1×/2× e redesenho não alteram os cálculos físicos.
- “Repetir” reproduz a última tacada desde a origem, sem somar outra tacada. Uma nova puxada só é aceita ao fim da reprodução e com resultado que permita continuar. Captura, limite de tacadas e falhas exigem reinício ou comparação, conforme o caso.
- Percorrer quadros antigos não cria uma nova ramificação da jogada. É necessário voltar ao último quadro para bater novamente.
- O deslocamento do mouse é dividido pela escala visual antes de chamar `Aim.FromDrag`. Assim, a mesma puxada em unidades do campo produz o mesmo impulso ao redimensionar. Modos 1/2 conservam as fórmulas e limites do núcleo. A câmera da mira acompanha a vista: inclinação 0° na planta e −60° na vista inclinada; rotação horizontal 0°.
- Esta normalização de escala é uma decisão moderna de interface. Não é reprodução pixel a pixel do mouse/VCL do original. Redimensionar, mudar a vista ou perder captura durante uma puxada cancela a mira.
- A física continua com suas coordenadas e atraso de altura documentados. O raio visual da bola, a bandeira, as cores, o enquadramento e o desenho das paredes são auxiliares de depuração, não parâmetros de colisão nem arte recuperada.
- O perfil padrão permanece `modern-reference-1`. O perfil experimental `surface-traversal-2` continua separado. Ver [comparação dos perfis](15-validacao-de-cantos.md). Se o perfil de referência terminar fora da superfície atribuída, o laboratório mostra o diagnóstico e bloqueia uma nova puxada inválida; não reposiciona a bola silenciosamente.
- Não há rede, áudio, progressão automática de campos, vários jogadores ou editor de geometria. Este laboratório não executa nem altera `WiniGolf.exe` ou o banco Ghidra.

## Validação e confiança

Executar a verificação da interface, sem uma janela visível:

```powershell
dotnet run --project simulator/src/Winigolf.Viewer -- --self-test artifacts/viewer
```

Resultado: **90 verificações aprovadas**, incluindo reprodução independente da divisão dos intervalos de relógio; pausa/passo; contagem e penalidade; repetição sem dupla contagem; comparação com origem idêntica; puxadas nulas/canceladas/fora da bola; conversão de mira, redimensionamento e vista inclinada; renderização dos 36 campos nas duas vistas (72 combinações, com checagem de pixels de campo). Os eventos de mouse são exercitados pelas entradas do controle; não é automação de um usuário real do Windows.

Atualização: [revisão de mira e buraco](17-revisao-mira-e-buraco.md) corrige o modo padrão do esquerdo, implementa o direito e mostra a captura visual. A bateria atual contém **101 verificações aprovadas**; 90 era o resultado inicial desta etapa.

Relatórios: [interface](../artifacts/viewer/checks.json) e [núcleo](../artifacts/viewer/physics-verification.json), este com **92/92** casos aprovados. Capturas renderizadas pela própria aplicação: [Splitt](../artifacts/viewer/splitt.png), [Plateau referência](../artifacts/viewer/plateau-reference.png), [Plateau experimental](../artifacts/viewer/plateau-traversal.png). As capturas são do protótipo; não são capturas do jogo original.

| Conclusão | Confiança / alcance |
|---|---|
| A interface usa o núcleo já especificado, sem uma segunda implementação da física | Alta; referência de projeto e chamadas diretas |
| Pausa, passo, repetição, troca de perfil e casos de mira testados funcionam conforme os contratos acima | Alta no runtime e cenários exercitados; testes automatizados e inspeção das capturas |
| Os dados carregados são os mesmos 36 JSON extraídos | Alta; mesmos arquivos copiados no build e leitura pelo mesmo `Course.Load` |
| O comportamento se aproxima suficientemente da experiência do jogo antigo | Ainda não determinado; falta comparação dinâmica com o original |
| O cliente e um servidor em máquinas distintas darão resultado idêntico bit a bit | Não demonstrado; este laboratório não resolve esse contrato |

Não foi necessária nova inferência de endereços do executável nesta etapa. As funções originais e seus graus de confiança permanecem na [especificação física](13-especificacao-fisica.md) e nos documentos de evidências associados.

O próximo uso recomendado é jogar Basic, Dip, Splitt e Plateau nos dois programas, anotando origem, puxada, direção, tempo até parar e posição final. Isso fornece uma comparação dinâmica antes de escolher definitivamente o perfil físico e investir na apresentação final.
