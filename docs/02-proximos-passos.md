# Proposta de investigação incremental

O reconhecimento inicial termina aqui, antes da análise extensa. Todas as etapas propostas mantêm o Ghidra e o executável em somente leitura; nomes e estruturas sugeridos ficam na documentação.

1. **Recuperar os eventos do formulário principal.** Ler recursos de formulário e tabelas de métodos/VMT para ligar nomes de eventos a endereços. Começar por Form1, pelo chamador ainda indefinido em `0x0041D7E8` e pelas referências ao estado objeto+`0xC8674`. Entrega: mapa de eventos, timer e mouse com evidências de ligação.
2. **Seguir uma tacada.** Dos handlers de mouse, rastrear escritas em posição `+0xC821C/+0xC8220/+0xC8224`, componentes `+0xC8234/+0xC8238/+0xC823C` e campos angulares usados por `0x0041D9DC`. Esses papéis são preliminares. Entrega: fluxo entrada → impulso → atualização, com convenções e unidades ainda explicitamente desconhecidas quando necessário.
3. **Isolar física e colisões.** Aprofundar `0x0041D2A4`, `0x0041D1E0`, `0x0041D80C` e `0x0041D9DC`; ler constantes e confirmar operações x87 no assembly. Verificar reflexão, passagem entre superfícies, amortecimento e reposicionamento após excesso de colisões. Entrega: equações sustentadas por instruções e offsets, sem copiar artefatos do decompilador.
4. **Mapear desenho.** Seguir `0x0041B67C`, `0x0041C074`, `0x0041ECB8` e wrappers de canvas. Identificar projeção, ordem de desenho, sprites/máscaras e apresentação do bitmap. Entrega: pipeline gráfico e inventário dos recursos usados.
5. **Documentar regras.** Percorrer estados de `0x0041E654`, preparação em `0x0041F0A0` e progressão em `0x0041FB40`. Confirmar tacadas, jogadores, bola no buraco, limites, sequência de campos e recordes por referências cruzadas. Entrega: máquina de estados com condições e confiança por transição.
6. **Extrair o modelo dos campos.** Investigar a rotina grande `0x00404EDC` por trechos de disassembly e a cópia em `0x00420074`, relacionando tabelas à física e renderização. Entrega: esquema de dados e um campo descrito como exemplo verificável.

Prioridade sugerida: eventos e uma tacada primeiro. Isso conecta a interação visível à física e evita interpretar animações auxiliares como movimento da bola. Validação dinâmica fica para uma etapa separada; não foi realizada nem se propõe modificar o binário.
