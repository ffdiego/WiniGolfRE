# Interface minimalista e tipografia

A interface usa Segoe UI Bold do Windows, com atlas de 64 px e textos ampliados. Preserva o fallback padrão do raylib se o arquivo não existir. Os botões do seletor reduzem o tamanho somente quando necessário para acomodar nomes longos.

Foram removidos os controles visíveis de ganho, perfil físico e câmera, atalhos para campos específicos, indicação de protótipo, botão de áudio e botão de reinício. A interface mantém o seletor de todas as pistas, tacadas, par e instruções essenciais. Após cada captura, a partida espera 2 segundos (3 quando abaixo do par); terminada a última bola, o avanço de campo é automático, sem espera adicional. M, R e Espaço continuam disponíveis pelo teclado.

Partículas foram adiadas: raylib oferece um exemplo oficial de partículas, mas a lista, emissão e atualização são implementadas pelo aplicativo. Não há um efeito pronto que satisfaça a condição de apenas ativar algo existente. Referência: https://www.raylib.com/examples/textures/loader.html?name=textures_particles_blending

Validação: build Release sem avisos/erros, 1443 verificações de integração aprovadas e inspeção de captura real da interface.
