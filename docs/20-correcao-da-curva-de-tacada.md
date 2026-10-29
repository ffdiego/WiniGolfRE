# Correção da curva de velocidade da tacada

2026-10-28. O usuário continuou percebendo o esquerdo fraco mesmo com ganho 2×. A revisão identificou **um erro de análise e transcrição**, não uma limitação da linguagem: duas multiplicações finais foram descritas como divisões e a constante extended de 80 bits foi lida com valor incorreto. A especificação 13, seção 2, foi corrigida; documentos históricos recebem aviso de errata.

## Evidência do executável

Mesmo alvo confirmado pelo GhidraMCP, SHA-256 `3b50e7311d733a1afbd3a33b31e14f95564226e1310a230ffc375e6727a6fb53`. Foram feitas apenas consultas. Nenhum handler foi criado, renomeado ou modificado no banco.

A revisão usou os bytes de `FormMouseUp` em `0x0041DF0C`, já preservados no documento 03, e novas leituras em [correcao-mira-ghidra-bytes.json](evidencias/correcao-mira-ghidra-bytes.json). Um segundo decodificador, **Iced 1.21.0**, leu os mesmos bytes diretamente do PE, sem executar o programa e sem depender de funções definidas no Ghidra.

| Endereço | Bytes | Operação confirmada |
|---|---|---|
| `0x0041E1D8` | `E8 7B 88 06 00` | Chama `0x00486A58`, sqrt, sobre L |
| `0x0041E1DD` | `D8 8B 34 82 0C 00` | FMUL com a componente vx já transformada |
| `0x0041E1E6` | `DB 2D 48 E6 41 00` | FLD da constante extended em `0x0041E648` |
| `0x0041E1EC` | `DE C9` | FMULP, multiplica pela constante e desempilha |
| `0x0041E202` | `D8 8B 38 82 0C 00` | FMUL com a componente vy já transformada |
| `0x0041E211` | `DE C9` | FMULP da componente vy |

A constante em `0x0041E648` tem bytes `CD CC CC CC CC CC CC CC FB 3F`. Como extended de 80 bits, é **0,1**, não 0,4. A ferramenta decodifica mantissa inteira de 64 bits e expoente com bias 16383, em vez de ler esses dez bytes como double. As constantes de modo em `0x0041E624` e `0x0041E640` continuam 0,1 e 0,8.

[Disassembly independente completo do trecho aritmético](evidencias/mira-iced-disassembly.json). **Confiança alta nos opcodes, constantes e na curva corrigida.**

## Fórmula corrigida

Conservam-se limites, ganhos por modo, compensação da câmera e sinais:

```text
L = sqrt(dx² + dy²), depois do limite do MouseMove
k = modo 1 ? 0.1 : 0.8
u = k * dx
w = k * dy / cos(c)

vx = (-cos(a)*u + sin(a)*w) * sqrt(L) * 0.1
vy = ( sin(a)*u + cos(a)*w) * sqrt(L) * 0.1
```

Para direção e câmera fixas, o vetor bruto já tem módulo proporcional a L. Multiplicá-lo por sqrt(L) dá **L^1,5**, não sqrt(L).

Comparação horizontal do esquerdo, yaw=0, antes do amortecimento. Comprimentos em pixels de entrada original ou unidades equivalentes normalizadas do cliente:

| Puxada | Fórmula antiga | Antiga com reforço 2× | Aritmética original corrigida |
|---:|---:|---:|---:|
| 10 | 0,79057 | 1,58114 | 0,31623 |
| 25 | 1,25 | 2,50 | 1,25 |
| 50 | 1,76777 | 3,53553 | 3,53553 |
| 100 | 2,50 | 5,00 | 10,00 |
| 200 | 3,53553 | 7,07107 | 28,28427 |
| 250 | 3,95285 | 7,90569 | 39,52847 |

Assim, o reforço 2× apenas coincidia com o original em torno de L=50. Para L=250, mesmo reforçado, o cliente ainda produzia aproximadamente um quinto da velocidade esperada. Puxadas muito pequenas, ao contrário, estavam fortes demais. Isso explica por que um multiplicador fixo não recuperava a sensação.

O direito também é afetado pela correção da curva: no limite horizontal L=50, gera aproximadamente **28,28427**, em vez de 14,14214. Para L=25, produz 10 tanto na fórmula antiga quanto na corrigida. Essa coincidência também ocorre no esquerdo em L=25: vários testes anteriores de integração usavam justamente essa entrada e não distinguiam as curvas. Outros testes de mira derivavam os valores esperados da transcrição incorreta. A nova auditoria é sustentada pelas instruções, não apenas pela especificação anterior.

## Auditoria reproduzível

[simulator/tools/AimAudit](../simulator/tools/AimAudit/) usa Iced para decodificar o handler e interpreta a sequência aritmética real: movimentações de registradores/locais, desvios por modo, pilha x87, conversões e chamadas identificadas de seno, cosseno e sqrt. O processo nunca executa WiniGolf.

```powershell
dotnet run --project simulator/tools/AimAudit -- --verify
```

O programa procura `WiniGolf.exe` na raiz do repositório ou na pasta `WiniGolf` um nível acima. Para outro local, passe `--exe <caminho>` ou defina a variável de ambiente `WINIGOLF_EXE`. O executável original não é incluído no repositório.

Comparação em **234 entradas** com deslocamentos inteiros, dois modos, várias direções, três yaw e duas inclinações:

- [Antes da correção](evidencias/auditoria-mira-antes-correcao.json): **210 divergências** e 24 coincidências.
- [Após a correção](evidencias/auditoria-mira-x87.json): **zero divergências** dentro da tolerância declarada no código.

O interpretador usa double e reproduz armazenamentos float do trecho; não é emulação integral da precisão de 80 bits, exceções ou modo de arredondamento x87. O limite/arredondamento do MouseMove fica fora da sequência interpretada; só se comparam entradas já dentro dos limites. As diferenças residuais de arredondamento são toleradas; não se reivindica igualdade bit a bit com a execução original.

## Implementação e verificação

- `Aim.FromDrag`: curva corrigida, identificada como **`drag-x87-corrected-2`**.
- `SceneCamera.Impulse`: usa o mesmo fator corrigido, conservando a inversa local da projeção e a normalização de escala do cliente.
- Padrão do cliente voltou a **1× da fórmula corrigida**. Opções 2×/3× continuam como reforços modernos opcionais, não como interpretação do executável. Não é necessário usar 2× para obter a nova curva.
- Laboratório e console também usam a mira corrigida. Movimento, amortecimento, rampas, colisões, captura e regras por impulso canônico não mudaram; suas versões dinâmicas continuam `modern-reference-1` e `surface-traversal-2`.
- Resultados anteriores de `--velocity` continuam válidos. Resultados de `--drag` devem considerar a versão da mira; alguns, especialmente L=25, coincidem. As trajetórias armazenam o impulso no quadro zero, permitindo reproduzir a física sem recalcular a entrada.

Resultados atuais:

| Verificação | Resultado |
|---|---:|
| Auditoria independente da aritmética | 234 casos, zero divergências |
| [Núcleo/console](../artifacts/simulator/verification-aim-v2.json) | 96/96 |
| [Laboratório](../artifacts/viewer/checks.json) | 101/101 |
| [Cliente raylib](../artifacts/game/checks.json) | 481/481 |

O cliente inclui novos casos de puxadas longas, máximo do direito e **240 tacadas** em Basic/Dip/Splitt: ambos os modos, cinco comprimentos e oito direções. Nenhuma falha de resolução, limite de passos ou quadro fora da superfície no perfil corrigido nessa bateria. Sons e demais controles continuam verificados.

Ainda falta comparação dinâmica com o original. A câmera perspectiva e a normalização de escala são decisões modernas; uma distância física idêntica do mouse não é necessariamente o mesmo comando em duas janelas com zoom/projeções diferentes. Arredondamento do endpoint e apresentação também podem afetar a sensação. A correção atual resolve uma divergência matemática concreta e grande; não encerra essas diferenças de interface.
