# Rampas reais: Dip e estado do estudo

2026-10-28. Os dados de [campo 03 — Dip](dados/campos/03.json) permitem relacionar a geometria recuperada às equações da física sem inventar uma pista de teste.

## Perfil do campo Dip

Partida `(-250,0,90)`, alvo `(250,0,90)`, superfície inicial 1 e par 2. As cinco superfícies se ligam em sequência, com passagens recíprocas 1↔2↔3↔4↔5. No corredor central, y está entre -50 e 50.

| Superfície | IDs dos vértices | Região no eixo x | Altura |
| ---: | --- | --- | --- |
| 1 | 1,2,3,4 | -300 a -200 | 90 |
| 2 | 2,5,6,3 | -200 a -50 | Desce de 90 para 10 |
| 3 | 5,7,8,6 | -50 a 0 | 10 |
| 4 | 7,9,10,8 | 0 a 150 | Sobe de 10 para 90 |
| 5 | 11,12,13,14,15,16,10,9 | Área final, a partir de x=150 | 90 |

Os pontos e endereços de origem estão no JSON; a região final se alarga além da largura do corredor. **Confiança alta no perfil e nas ligações**, decorrente das atribuições e dos polígonos recuperados.

## Aceleração prevista nas duas rampas

Aplicando literalmente as operações de `0x0041D2A4` documentadas em [04-fisica-base.md](04-fisica-base.md):

```text
Superfície 2:
dx=150, dzx=-80
deltaVx = -0.5*(-80)/sqrt(150²+80²)
        = +40/170 = +0.2352941176 por atualização

Superfície 4:
dx=150, dzx=+80
deltaVx = -40/170 = -0.2352941176 por atualização
```

Em ambas, os deltas de z ao longo de y são zero, então essa geometria não acrescenta aceleração em y. Em direção +x, a primeira rampa acelera e a segunda freia. O fator 0,98 é aplicado **depois** de atualizar a posição.

Exemplos locais de um passo, sem atingir aresta, com vy=vz=0 e vx inicial 2:

| Situação | Posição inicial x/y | z calculado no começo do passo | vx usada no deslocamento | x após deslocamento | vx depois de 0,98 |
| --- | --- | ---: | ---: | ---: | ---: |
| Descida, superfície 2 | -125 / 0 | 50 | 2,2352941 | -122,7647059 | 2,1905882 |
| Subida, superfície 4 | 75 / 0 | 50 | 1,7647059 | 76,7647059 | 1,7294118 |

**São previsões aritméticas, não observações dinâmicas.** A rotina original usa campos float32 e intermediários x87; a tabela acima usa cálculo matemático arredondado. A reconstrução de velocidade em `0x0041D9DC` pode introduzir diferenças numéricas e singularidades; seu assembly completo foi preservado em [reconstrucao-velocidade-0041d9dc.json](evidencias/reconstrucao-velocidade-0041d9dc.json).

Há um detalhe de ordem importante: z é calculado usando x/y **antes** do deslocamento, e depois recebe vz. Para esses exemplos com vz=0, z permanece 50 neste passo, embora x já tenha mudado. Recalcular a altura sob a nova posição imediatamente seria uma mudança em relação à ordem observada. Para o port, pode ser uma melhoria aceitável, mas deve ser uma escolha consciente ao comparar a aparência e a física.

Ainda falta confirmar como a troca de superfície no final do passo afeta a altura no frame seguinte e testar uma bola que desacelera, para e reverte em uma rampa. O limiar de fim de turno em `0x0041ECB8` exige superfície plana; aplicar o mesmo limiar indiscriminadamente em rampa interromperia o rolamento de retorno.

## Quanto falta

O estudo já tem material suficiente para começar a especificar um núcleo de simulação e um primeiro protótipo: campos exportados, controles, temporização nominal, impulso, amortecimento, cálculo de altura, aceleração em rampas, paredes/passagens e condições locais de parada/captura. Não é necessário compreender todo o runtime Borland/VCL para reescrever esses elementos.

O trabalho restante mais relevante para a semelhança física é:

1. Validar o movimento no original em plano, descida/subida, rebote reto/oblíquo e cruzamento de superfícies. Ainda não houve medição dinâmica.
2. Fechar as convenções e singularidades de direção/velocidade e testar casos de canto ou múltiplas arestas em um passo.
3. Verificar alterações de geometria e possíveis tratamentos específicos em outros caminhos de código. Nomes como Pipes ou Spring, isoladamente, não comprovam mecânicas especiais.
4. Consolidar preparação/fim de turno, limite de tacadas, captura e progressão para uma partida completa.

Para reconstruir o jogo completo também restam inventário/exportação de recursos gráficos e sons, apresentação, câmera, placar, configurações e persistência. Esses itens podem ser feitos depois do protótipo físico e não precisam reproduzir o layout interno da VCL.

A principal incerteza passou de disponibilidade dos dados para **validação do comportamento**. Os 36 campos foram recuperados com uma exceção não utilizada explicitamente preservada; isso reduz bastante a parte mecânica pendente, mas não justifica declarar a física finalizada nem atribuir uma porcentagem de conclusão sem comparar com o original.
