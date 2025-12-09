# Som de Fundo — Console Profissional

Aplicativo leve e moderno para tocar fundos musicais em cultos e eventos. Desenvolvido em Python com CustomTkinter e integração de controle remoto via navegador.

## Recursos

- Até 20 botões por playlist, com cor, imagem e volume por card
- Grade responsiva (5–8 colunas), tamanhos pequeno/médio/grande + ajuste fino
- Loop individual por botão com LED discreto e indicador “Pausado” na UI
- Barra de tempo com seek suave (pré‑fade e fade‑in) e visualização do tempo
- Atalhos de teclado: 1–0 (1–10), Q–P (11–20), Espaço (pausar/retomar), V (reiniciar com fade)
- Preferências globais de layout aplicadas a todas as playlists
- Controle remoto com PIN, capas atualizadas e volume geral com feedback
- Backup/Importar playlists com ícones e sons, seleção de conteúdo e progresso

## Novidades

- Loop por botão e indicador visual sincronizado
- Linha de status com “Pausado” e “loop ativado” de forma sutil
- Limite de arquivo de áudio ao anexar aos botões: 800 MB (formatos `.mp3`, `.wav`, `.ogg`)
- Controle remoto serve capas diretamente do diretório de ícones e exibe volume geral
- Configurações globais: tamanho dos cards, colunas e escala persistem para todas as playlists

## Pré‑requisitos

- Python 3.10+
- `requirements.txt`: `customtkinter`, `pillow`, `pygame`, `flask`, `qrcode`

## Instalação

```bash
git clone https://github.com/alanxdpro/Som_de_fundo.git
cd Som_de_fundo
pip install -r requirements.txt
```

## Execução

```bash
python som_de_fundo.py
```

## Atalhos

- 1–0 (1–10), Q–P (11–20)
- Espaço: pausar/retomar
- V: reiniciar com fade out rápido
- ←/→: navegar playlists • Enter: aplicar • Esc: fechar
- Atalhos são bloqueados ao editar textos/campos

## Configurações

- Tema: claro/escuro
- Áudio: Fade In/Out, Crossfade, Seek Fade
- Botões: quantidade, cor, imagem, texto e som
- Layout global: tamanho dos cards (P/M/G), colunas (5–8) e ajuste fino

## Controle Remoto

- Acesse a URL e digite o PIN para controlar pelo navegador
- Visualiza lista de botões com capas, estado (Tocando/Pausado/Parado) e volume geral
- Endpoints: `/api/state`, `/api/play/<index>`, `/api/pause`, `/api/stop`, `/api/playlist`, `/api/volume`

## Backup e Importar

- Exporta playlists (JSON), ícones e sons para `.zip`
- Importa playlists de diferentes tamanhos com ajuste de caminhos
- Operações com barras de progresso e mensagens de status

## Limites de Arquivo

- Ao anexar áudio ao botão: limite máximo 800 MB
- Aviso de desempenho para arquivos acima de ~40 MB

## Criando Executável (Windows)

```bash
pip install pyinstaller
py -m PyInstaller --noconfirm --clean --onefile --windowed \
  --name Som_de_fundo \
  --icon "c:\\Users\\pc User\\Documents\\meu codigos\\App_fundo_De_Louvor\\Som_de_fundo\\icone.ico" \
  --add-data "icons;icons" \
  som_de_fundo.py
```

Saída: `dist/Som_de_fundo.exe` (único arquivo, sem pasta). Os dados de usuário ficam em `AppData\Roaming\Som_de_fundo`.

## Licença

Projeto sob Licença MIT — veja [LICENSE](LICENSE).

—

Desenvolvido com ❤️ por [@allan.psxd1]
