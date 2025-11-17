import os, json, threading, time, math, webbrowser
import pygame
import customtkinter as ctk
from tkinter import filedialog, colorchooser, messagebox, simpledialog
from PIL import Image, ImageTk

def mostrar_sobre():
    sobre_janela = ctk.CTkToplevel()
    sobre_janela.title("Sobre o Desenvolvedor")
    sobre_janela.geometry("500x400")
    sobre_janela.resizable(False, False)
    sobre_janela.grab_set()
    
    main_frame = ctk.CTkFrame(sobre_janela, fg_color="transparent")
    main_frame.pack(expand=True, fill="both", padx=20, pady=20)
    
    titulo = ctk.CTkLabel(main_frame, text="Sobre o Desenvolvedor", font=("Arial", 24, "bold"))
    titulo.pack(pady=(0, 20))
    
    avatar = ctk.CTkLabel(main_frame, text="👨‍💻", font=("Arial", 70))
    avatar.pack(pady=10)
    
    def pulsar_avatar():
        current_size = 70
        def aumentar():
            nonlocal current_size
            if current_size < 80:
                current_size += 2
                avatar.configure(font=("Arial", current_size))
                sobre_janela.after(20, aumentar)
            else:
                sobre_janela.after(20, diminuir)
        
        def diminuir():
            nonlocal current_size
            if current_size > 70:
                current_size -= 2
                avatar.configure(font=("Arial", current_size))
                sobre_janela.after(20, diminuir)
            else:
                sobre_janela.after(1500, pulsar_avatar)
        
        aumentar()
    
    pulsar_avatar()
    
    texto_sobre = ctk.CTkLabel(
        main_frame,
        text="Desenvolvido com carinho por Alan ❤️\n\n"
             "Siga-me nas redes sociais:",
        justify="center"
    )
    texto_sobre.pack(pady=10)
    
    links_frame = ctk.CTkFrame(main_frame, fg_color="transparent")
    links_frame.pack(pady=10)
    
    def criar_botao_link(text, url):
        btn = ctk.CTkButton(
            links_frame,
            text=text,
            command=lambda: webbrowser.open(url),
            fg_color="transparent",
            text_color=("#1a73e8", "#8ab4f8"),
            hover_color=("#e8f0fe", "#1a56db"),
            anchor="w"
        )
        btn.pack(fill="x", pady=2)
    
    criar_botao_link("📷 Instagram: @allan.psxd1", "https://instagram.com/allan.psxd1")
    criar_botao_link("▶️ YouTube: @alanPs", "https://www.youtube.com/@alanPs")
    criar_botao_link("▶️ YouTube: @alantecmoz", "https://www.youtube.com/@alantecmoz")
    criar_botao_link("💾 GitHub: alanxdpro/Som_de_fundo", "https://github.com/alanxdpro/Som_de_fundo")
    
    versao = ctk.CTkLabel(
        main_frame,
        text="Versão 1.0",
        text_color=("gray50", "gray70"),
        font=("Arial", 10)
    )
    versao.pack(side="bottom", pady=10)
    
    sobre_janela.update_idletasks()
    width = sobre_janela.winfo_width()
    height = sobre_janela.winfo_height()
    x = (sobre_janela.winfo_screenwidth() // 2) - (width // 2)
    y = (sobre_janela.winfo_screenheight() // 2) - (height // 2)
    sobre_janela.geometry(f'{width}x{height}+{x}+{y}')

CONFIG_FILE = "config.json"
SONS_DIR = "sons"
ICONS_DIR = "icons"
PLAYLISTS_DIR = "playlists"
FADE_MS = 800

os.makedirs(SONS_DIR, exist_ok=True)
os.makedirs(ICONS_DIR, exist_ok=True)
os.makedirs(PLAYLISTS_DIR, exist_ok=True)
pygame.mixer.init()

def carregar_icone(nome_arquivo, tamanho=(20, 20)):
    """Carrega um ícone da pasta icons e redimensiona se necessário"""
    try:
        caminho = os.path.join(ICONS_DIR, nome_arquivo)
        if os.path.exists(caminho):
            img = Image.open(caminho)
            img = img.resize(tamanho, Image.Resampling.LANCZOS)
            return ImageTk.PhotoImage(img)
    except:
        pass
    return None

config = {}
current_index = None
is_switching = threading.Lock()
music_start_time = None
timer_label = None
is_paused = False
pause_time = 0
master_volume = 1.0
current_playlist = "default"

def default_config():
    paleta_cores = [
        '#3b82f6', '#8b5cf6', '#06b6d4', '#10b981', '#ef4444',
        '#f59e0b', '#ec4899', '#14b8a6', '#f97316', '#6366f1'
    ]
    
    return {
        "botoes": [
            {"nome": f"Botão {i+1}", "cor": paleta_cores[i], "arquivo": "", "icone": "", "emoji": "", "volume": 1.0}
            for i in range(10)
        ],
        "atalhos_habilitados": True,
        "fade_in_ms": 800,
        "fade_out_ms": 800,
        "repeticao_habilitada": False,
        "master_volume": 1.0
    }

def carregar_config():
    global config, master_volume
    playlist_file = os.path.join(PLAYLISTS_DIR, f"{current_playlist}.json")
    
    if not os.path.exists(playlist_file):
        config = default_config()
        salvar_config()
    else:
        with open(playlist_file, "r", encoding="utf-8") as f:
            config = json.load(f)
        
        changed = False
        if "atalhos_habilitados" not in config:
            config["atalhos_habilitados"] = True
            changed = True
        if "fade_in_ms" not in config:
            config["fade_in_ms"] = 800
            changed = True
        if "fade_out_ms" not in config:
            config["fade_out_ms"] = 800
            changed = True
        if "repeticao_habilitada" not in config:
            config["repeticao_habilitada"] = False
            changed = True
        if "master_volume" not in config:
            config["master_volume"] = 1.0
            changed = True
        
        for b in config.get("botoes", []):
            if "emoji" not in b:
                b["emoji"] = ""
                changed = True
            if "volume" not in b:
                b["volume"] = 1.0
                changed = True
        
        if changed:
            salvar_config()
    
    master_volume = config.get("master_volume", 1.0)
    pygame.mixer.music.set_volume(master_volume)

def salvar_config():
    playlist_file = os.path.join(PLAYLISTS_DIR, f"{current_playlist}.json")
    config["master_volume"] = master_volume
    with open(playlist_file, "w", encoding="utf-8") as f:
        json.dump(config, f, indent=4, ensure_ascii=False)

def listar_playlists():
    playlists = []
    for file in os.listdir(PLAYLISTS_DIR):
        if file.endswith(".json"):
            playlists.append(file.replace(".json", ""))
    return playlists if playlists else ["default"]

def trocar_playlist(nova_playlist):
    global current_playlist, config
    parar_tudo()
    current_playlist = nova_playlist
    carregar_config()
    atualizar_estilos()
    atualizar_combo_playlists()
    messagebox.showinfo("Playlist", f"Playlist '{nova_playlist}' carregada com sucesso!")

def criar_nova_playlist():
    nome = simpledialog.askstring("Nova Playlist", "Digite o nome da nova playlist:")
    if nome:
        nome = nome.strip().replace(" ", "_")
        if nome:
            global current_playlist
            parar_tudo()
            current_playlist = nome
            global config
            config = default_config()
            salvar_config()
            atualizar_estilos()
            atualizar_combo_playlists()
            messagebox.showinfo("Playlist", f"Playlist '{nome}' criada com sucesso!")

def duplicar_playlist():
    nome = simpledialog.askstring("Duplicar Playlist", f"Digite o nome para a cópia de '{current_playlist}':")
    if nome:
        nome = nome.strip().replace(" ", "_")
        if nome:
            nova_playlist_file = os.path.join(PLAYLISTS_DIR, f"{nome}.json")
            playlist_atual_file = os.path.join(PLAYLISTS_DIR, f"{current_playlist}.json")
            
            with open(playlist_atual_file, "r", encoding="utf-8") as f:
                config_copia = json.load(f)
            
            with open(nova_playlist_file, "w", encoding="utf-8") as f:
                json.dump(config_copia, f, indent=4, ensure_ascii=False)
            
            atualizar_combo_playlists()
            messagebox.showinfo("Playlist", f"Playlist duplicada como '{nome}'!")

def excluir_playlist():
    if current_playlist == "default":
        messagebox.showwarning("Aviso", "Não é possível excluir a playlist 'default'!")
        return
    
    resposta = messagebox.askyesno("Excluir Playlist", f"Tem certeza que deseja excluir a playlist '{current_playlist}'?")
    if resposta:
        playlist_file = os.path.join(PLAYLISTS_DIR, f"{current_playlist}.json")
        if os.path.exists(playlist_file):
            os.remove(playlist_file)
        trocar_playlist("default")
        messagebox.showinfo("Playlist", f"Playlist excluída com sucesso!")

def _play_file_loop(path, volume):
    global is_paused
    try:
        pygame.mixer.music.load(path)
        volume_final = volume * master_volume
        pygame.mixer.music.set_volume(volume_final)
        # Decidir se deve repetir baseado na configuração
        repetir = config.get("repeticao_habilitada", False)
        loops = -1 if repetir else 0
        pygame.mixer.music.play(loops=loops, fade_ms=config.get("fade_in_ms", FADE_MS))
        is_paused = False
    except Exception as e:
        messagebox.showerror("Erro de áudio", f"Falha ao tocar: {e}")

def abrir_arquivo(index):
    """Abre a janela de seleção de arquivo para o botão especificado"""
    f = filedialog.askopenfilename(
        title=f"Selecionar som para {config['botoes'][index]['nome']}", 
        filetypes=[("Áudio", "*.mp3 *.wav *.ogg")]
    )
    if f:
        config["botoes"][index]["arquivo"] = f
        salvar_config()
        messagebox.showinfo("Sucesso", f"Arquivo adicionado ao botão {config['botoes'][index]['nome']}")

def tocar_som(index):
    global current_index
    botao = config["botoes"][index]
    caminho = botao["arquivo"]
    if not caminho or not os.path.exists(caminho):
        resposta = messagebox.askyesno("Arquivo não encontrado", 
                                     f"Nenhum arquivo de som definido para este botão.\n\nDeseja adicionar um arquivo agora?")
        if resposta:
            # Abre a janela de seleção de arquivo quando o usuário confirmar
            abrir_arquivo(index)
        return
    if current_index == index and pygame.mixer.music.get_busy():
        return
    volume = botao.get("volume", 1.0)
    threading.Thread(target=_switch_music_thread, args=(index, caminho, volume), daemon=True).start()

def _switch_music_thread(index, caminho, volume):
    global current_index, music_start_time
    if not is_switching.acquire(blocking=False):
        return
    try:
        if pygame.mixer.music.get_busy():
            fade_out = config.get("fade_out_ms", FADE_MS)
            pygame.mixer.music.fadeout(fade_out)
            time.sleep(fade_out / 1000)
        _play_file_loop(caminho, volume)
        current_index = index
        music_start_time = time.time()
        atualizar_estilos()
        atualizar_timer()
    finally:
        is_switching.release()

def obter_duracao_musica(caminho):
    try:
        sound = pygame.mixer.Sound(caminho)
        return int(sound.get_length())
    except:
        return 0

def formatar_tempo(segundos):
    minutos = int(segundos) // 60
    segundos = int(segundos) % 60
    return f"{minutos:02d}:{segundos:02d}"

def atualizar_timer():
    global music_start_time, timer_label
    if music_start_time and pygame.mixer.music.get_busy():
        elapsed = int(time.time() - music_start_time)
        
        if current_index is not None and "arquivo" in config["botoes"][current_index]:
            caminho = config["botoes"][current_index]["arquivo"]
            nome_arquivo = os.path.basename(caminho)
            nome_musica = os.path.splitext(nome_arquivo)[0]
            
            if "duracao" not in config["botoes"][current_index]:
                duracao_total = obter_duracao_musica(caminho)
                config["botoes"][current_index]["duracao"] = duracao_total
            else:
                duracao_total = config["botoes"][current_index]["duracao"]
            
            tempo_atual = formatar_tempo(elapsed)
            tempo_total = formatar_tempo(duracao_total) if duracao_total > 0 else "--:--"
            
            timer_label.configure(text=f" {nome_musica} | {tempo_atual} / {tempo_total}")
        
        app.after(1000, atualizar_timer)
    elif timer_label:
        timer_label.configure(text="")

def fade_in(step=0.0):
    if not is_paused and current_index is not None and pygame.mixer.music.get_busy():
        volume_botao = config["botoes"][current_index].get("volume", 1.0)
        target_volume = volume_botao * master_volume
        current_vol = min(step, target_volume)
        pygame.mixer.music.set_volume(current_vol)
        
        if current_vol < target_volume:
            app.after(30, lambda: fade_in(step + 0.05))

def pausar_retomar():
    global music_start_time, is_paused, current_index, pause_time
    
    if not is_paused and pygame.mixer.music.get_busy():
        fade_out_time = 500
        pygame.mixer.music.fadeout(fade_out_time)
        is_paused = True
        if music_start_time is not None:
            pause_time = time.time() - music_start_time
    elif is_paused and current_index is not None:
        botao = config["botoes"][current_index]
        caminho = botao["arquivo"]
        volume = botao.get("volume", 1.0)
        
        pygame.mixer.music.stop()
        
        try:
            pygame.mixer.music.load(caminho)
            pygame.mixer.music.set_volume(0)
            
            posicao_segundos = pause_time if pause_time is not None else 0
            # Respeitar configuração de repetição ao retomar
            repetir = config.get("repeticao_habilitada", False)
            loops = -1 if repetir else 0
            pygame.mixer.music.play(loops=loops, start=posicao_segundos)
            
            is_paused = False
            music_start_time = time.time() - posicao_segundos
            
            fade_in(0.0)
            
            atualizar_timer()
            
        except Exception as e:
            messagebox.showerror("Erro", f"Não foi possível retomar a música: {e}")
            is_paused = True
    
    atualizar_estilos()

def parar_tudo():
    global current_index, music_start_time, is_paused
    if pygame.mixer.music.get_busy() or is_paused:
        fade_out_time = 500
        pygame.mixer.music.fadeout(fade_out_time)
        is_paused = False
    music_start_time = None
    if timer_label:
        timer_label.configure(text="")
    current_index = None
    atualizar_estilos()

def atualizar_volume_individual(index, volume):
    config["botoes"][index]["volume"] = volume
    if current_index == index and pygame.mixer.music.get_busy():
        volume_final = volume * master_volume
        pygame.mixer.music.set_volume(volume_final)
    salvar_config()

def atualizar_volume_master(volume):
    global master_volume
    master_volume = volume
    config["master_volume"] = volume
    if current_index is not None and pygame.mixer.music.get_busy():
        volume_botao = config["botoes"][current_index].get("volume", 1.0)
        volume_final = volume_botao * master_volume
        pygame.mixer.music.set_volume(volume_final)
    salvar_config()

animation_frames = {}

def criar_animacao_pulsacao(canvas_widget, index):
    def animar():
        if current_index == index:
            t = time.time() * 3
            escala = 1 + 0.05 * math.sin(t)
            opacity = 0.3 + 0.2 * math.sin(t)
            app.after(50, animar)
    animar()

ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")

app = ctk.CTk()
app.title("Som de Fundo — Console Profissional")
app.geometry("1100x650")

# Definir ícone da janela
try:
    icon_path = os.path.join(ICONS_DIR, "app_icon.png")
    if os.path.exists(icon_path):
        app.iconbitmap(icon_path)
except:
    pass  # Se não conseguir carregar o ícone, continua sem ele

carregar_config()
button_refs = []
volume_sliders = []

header_frame = ctk.CTkFrame(app, fg_color="transparent")
header_frame.pack(pady=20, fill="x", padx=20)

header = ctk.CTkLabel(header_frame, text="🎚️ SOM DE FUNDO PRO", font=("Arial Rounded MT Bold", 26))
header.pack(side="left", expand=True)

timer_label = ctk.CTkLabel(header_frame, text="", font=("Arial", 12), text_color="#9ca3af", anchor="e")
timer_label.pack(side="right", padx=20, pady=5)

playlist_frame = ctk.CTkFrame(app, fg_color="#1e293b", height=50)
playlist_frame.pack(fill="x", padx=20, pady=(0, 10))

ctk.CTkLabel(playlist_frame, text="📁 Playlist:", font=("Arial", 13, "bold")).pack(side="left", padx=(10, 5))

playlist_combo = ctk.CTkComboBox(playlist_frame, values=listar_playlists(), width=200,
                                  command=lambda choice: trocar_playlist(choice))
playlist_combo.set(current_playlist)
playlist_combo.pack(side="left", padx=5)

ctk.CTkButton(playlist_frame, text="➕ Nova", width=80, height=28,
              command=criar_nova_playlist).pack(side="left", padx=2)
ctk.CTkButton(playlist_frame, text="📋 Duplicar", width=90, height=28,
              command=duplicar_playlist).pack(side="left", padx=2)
ctk.CTkButton(playlist_frame, text="🗑️ Excluir", width=80, height=28, fg_color="#dc2626",
              command=excluir_playlist).pack(side="left", padx=2)

def atualizar_combo_playlists():
    playlists = listar_playlists()
    playlist_combo.configure(values=playlists)
    playlist_combo.set(current_playlist)

volume_master_frame = ctk.CTkFrame(playlist_frame, fg_color="transparent")
volume_master_frame.pack(side="right", padx=10)

ctk.CTkLabel(volume_master_frame, text="🔊 Volume Geral:", font=("Arial", 12, "bold")).pack(side="left", padx=5)

volume_master_label = ctk.CTkLabel(volume_master_frame, text="100%", width=45, font=("Arial", 11))
volume_master_label.pack(side="left", padx=5)

def on_master_volume_change(valor):
    volume_master_label.configure(text=f"{int(valor*100)}%")
    atualizar_volume_master(valor)

volume_master_slider = ctk.CTkSlider(volume_master_frame, from_=0, to=1, width=150,
                                     command=on_master_volume_change)
volume_master_slider.set(master_volume)
volume_master_slider.pack(side="left", padx=5)

panel = ctk.CTkFrame(app)
panel.pack(expand=True, fill="both", padx=20, pady=10)

def atualizar_estilos():
    for i, ref in enumerate(button_refs):
        cor = config["botoes"][i]["cor"]
        nome = config["botoes"][i]["nome"]
        emoji = config["botoes"][i].get("emoji", "")
        display = f"{emoji} {nome}".strip() if emoji else nome
        ref.configure(fg_color=cor, text=quebrar_texto(display))
        
        if current_index == i:
            ref.configure(border_color="#ffffff", border_width=3)
            cor_rgb = tuple(int(cor.lstrip('#')[i:i+2], 16) for i in (0, 2, 4))
            cor_clara = f"#{min(cor_rgb[0]+30, 255):02x}{min(cor_rgb[1]+30, 255):02x}{min(cor_rgb[2]+30, 255):02x}"
            ref.configure(hover_color=cor_clara)
        else:
            ref.configure(border_color=cor, border_width=0)
            ref.configure(hover_color=cor)
    
    atualizar_texto_atalhos()

def quebrar_texto(texto, max_chars=12):
    palavras = texto.split()
    linhas = []
    linha_atual = []
    
    for palavra in palavras:
        teste = ' '.join(linha_atual + [palavra])
        if len(teste) <= max_chars:
            linha_atual.append(palavra)
        else:
            if linha_atual:
                linhas.append(' '.join(linha_atual))
            linha_atual = [palavra]
    
    if linha_atual:
        linhas.append(' '.join(linha_atual))
    
    return '\n'.join(linhas) if linhas else texto

def criar_botoes():
    panel.grid_propagate(True)
    for i in range(2):
        panel.grid_rowconfigure(i, weight=1, uniform='row')
    for i in range(5):
        panel.grid_columnconfigure(i, weight=1, uniform='col')
    
    for i in range(10):
        frame = ctk.CTkFrame(panel, fg_color="transparent")
        frame.grid(row=i//5, column=i%5, padx=5, pady=5, sticky="nsew")
        frame.grid_propagate(True)
        frame.grid_rowconfigure(0, weight=1)
        frame.grid_rowconfigure(1, weight=0)
        frame.grid_columnconfigure(0, weight=1)
        
        emoji = config["botoes"][i].get("emoji", "")
        base_texto = config["botoes"][i]["nome"]
        texto_botao = quebrar_texto((f"{emoji} {base_texto}".strip() if emoji else base_texto))
        
        b = ctk.CTkButton(frame, 
                         text=texto_botao,
                         fg_color=config["botoes"][i]["cor"],
                         text_color="white",
                         width=140, 
                         height=70, 
                         font=("Arial", 13, "bold"),
                         anchor="center",
                         corner_radius=8,
                         hover_color=config["botoes"][i]["cor"],
                         command=lambda i=i: tocar_som(i))
        b.grid(row=0, column=0, sticky="nsew", padx=2, pady=2)
        button_refs.append(b)
        
        volume_frame = ctk.CTkFrame(frame, fg_color="#1e1e1e", height=30)
        volume_frame.grid(row=1, column=0, sticky="ew", padx=2, pady=(0, 2))
        
        volume_label = ctk.CTkLabel(volume_frame, text=f"{int(config['botoes'][i].get('volume', 1.0)*100)}%", 
                                     width=35, font=("Arial", 10))
        volume_label.pack(side="left", padx=2)
        
        def criar_callback(idx, lbl):
            def callback(valor):
                lbl.configure(text=f"{int(valor*100)}%")
                atualizar_volume_individual(idx, valor)
            return callback
        
        slider = ctk.CTkSlider(volume_frame, from_=0, to=1, width=100, height=12,
                               command=criar_callback(i, volume_label))
        slider.set(config["botoes"][i].get("volume", 1.0))
        slider.pack(side="left", padx=2, expand=True, fill="x")
        volume_sliders.append((slider, volume_label))

criar_botoes()

def abrir_config_janela():
    win = ctk.CTkToplevel(app)
    win.title("Configurações dos Botões")
    win.geometry("650x550")
    win.resizable(False, False)
    
    win.transient(app)
    win.grab_set()
    win.lift()
    win.focus_force()
    
    win.update_idletasks()
    x = (win.winfo_screenwidth() // 2) - (650 // 2)
    y = (win.winfo_screenheight() // 2) - (550 // 2)
    win.geometry(f"650x550+{x}+{y}")
    
    def resetar_config():
        resposta = messagebox.askyesno(
            "Resetar Configurações",
            "Tem certeza que deseja resetar todas as configurações para o padrão?\n\nIsso irá remover todos os nomes, cores e arquivos de áudio configurados."
        )
        if resposta:
            global config
            config = default_config()
            salvar_config()
            atualizar_estilos()
            for i, (slider, label) in enumerate(volume_sliders):
                slider.set(1.0)
                label.configure(text="100%")
            messagebox.showinfo("✅ Resetado", "Configurações resetadas com sucesso!\nFeche e abra a janela de configurações novamente para ver as mudanças.")
            win.destroy()
    
    header_config = ctk.CTkFrame(win, fg_color="transparent", height=40)
    header_config.pack(fill="x", padx=10, pady=(10, 5))
    
    ctk.CTkLabel(header_config, text="⚙️ Configurações", font=("Arial", 18, "bold")).pack(side="left")
    
    ctk.CTkButton(header_config, text="🔄 Resetar Tudo", fg_color="#dc2626", hover_color="#b91c1c",
                  width=120, height=30, font=("Arial", 11, "bold"),
                  command=resetar_config).pack(side="right")

    canvas = ctk.CTkScrollableFrame(win, width=620, height=380)
    canvas.pack(padx=10, pady=(5, 5), fill="both", expand=True)

    entries = []
    
    atalhos_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color="#1e293b")
    atalhos_frame.pack(pady=8, padx=10, fill="x")
    
    ctk.CTkLabel(atalhos_frame, text="⌨️ Atalhos de Teclado", font=("Arial", 16, "bold")).pack(anchor="w", pady=4, padx=8)
    
    atalhos_var = ctk.BooleanVar(value=config.get("atalhos_habilitados", True))
    atalhos_checkbox = ctk.CTkCheckBox(atalhos_frame, 
                                       text="Habilitar atalhos de teclado (Teclas 0-9)",
                                       variable=atalhos_var,
                                       font=("Arial", 12))
    atalhos_checkbox.pack(anchor="w", padx=10, pady=8)
    
    fade_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color="#1e293b")
    fade_frame.pack(pady=8, padx=10, fill="x")
    ctk.CTkLabel(fade_frame, text="🎵 Áudio (Fade)", font=("Arial", 16, "bold")).pack(anchor="w", pady=4, padx=8)
    fades_row = ctk.CTkFrame(fade_frame, fg_color="transparent")
    fades_row.pack(fill="x", padx=10, pady=6)
    ctk.CTkLabel(fades_row, text="Fade In (ms):", width=120).pack(side="left")
    fade_in_entry = ctk.CTkEntry(fades_row, width=100)
    fade_in_entry.insert(0, str(config.get("fade_in_ms", 800)))
    fade_in_entry.pack(side="left", padx=(0, 20))
    ctk.CTkLabel(fades_row, text="Fade Out (ms):", width=120).pack(side="left")
    fade_out_entry = ctk.CTkEntry(fades_row, width=100)
    fade_out_entry.insert(0, str(config.get("fade_out_ms", 800)))
    fade_out_entry.pack(side="left")

    # Opção de repetição do áudio (logo após Fade)
    repeticao_frame = ctk.CTkFrame(fade_frame, fg_color="transparent")
    repeticao_frame.pack(fill="x", padx=10, pady=(6, 6))
    ctk.CTkLabel(repeticao_frame, text="🔁 Repetição:", width=120).pack(side="left")
    repeticao_var = ctk.BooleanVar(value=config.get("repeticao_habilitada", False))
    repeticao_checkbox = ctk.CTkCheckBox(repeticao_frame, 
                                         text="Habilitar repetição do áudio (loop)",
                                         variable=repeticao_var,
                                         font=("Arial", 12))
    repeticao_checkbox.pack(side="left", padx=8)

    ctk.CTkLabel(canvas, text="🎚️ Configuração dos Botões", font=("Arial", 16, "bold")).pack(anchor="w", pady=(15, 5), padx=10)

    for i, b in enumerate(config["botoes"]):
        frame = ctk.CTkFrame(canvas, corner_radius=12)
        frame.pack(pady=8, padx=10, fill="x")

        ctk.CTkLabel(frame, text=f"🎵 {b['nome']}", font=("Arial", 16, "bold")).pack(anchor="w", pady=4, padx=8)

        nome = ctk.CTkEntry(frame, placeholder_text="Nome do botão (máx. 30 caracteres)")
        nome.insert(0, b["nome"])
        nome.pack(padx=10, pady=5, fill="x")
        
        aviso_label = ctk.CTkLabel(frame, text="", font=("Arial", 10), text_color="#e74c3c")
        aviso_label.pack(padx=10, pady=2, anchor="w")
        
        def validar_caracteres(event, nome_entry=nome, aviso=aviso_label):
            texto = nome_entry.get()
            num_chars = len(texto)
            if num_chars > 30:
                aviso.configure(text=f"⚠️ Limite excedido: {num_chars}/30 caracteres")
            else:
                aviso.configure(text=f"{num_chars}/30 caracteres")
        
        nome.bind("<KeyRelease>", validar_caracteres)
        validar_caracteres(None, nome, aviso_label)

        emojis_opcoes = [
            "", "🎵", "🎶", "🙏", "🙌", "🔥", "✨", "💖", "🌟", "🕊️",
            "🎹", "🎸", "🥁", "🎤", "🎷", "🎺", "🪗", "🪘", "🪕", "📯",
            "📖", "✝️", "⛪", "🕯️", "👐", "💫", "🎼", "🎧", "📿", "🌈"
        ]
        emoji_row = ctk.CTkFrame(frame, fg_color="transparent")
        emoji_row.pack(padx=10, pady=(0, 8), fill="x")
        ctk.CTkLabel(emoji_row, text="Emoji:", width=60).pack(side="left")
        emoji_var = ctk.StringVar(value=b.get("emoji", ""))
        emoji_menu = ctk.CTkOptionMenu(emoji_row, values=emojis_opcoes, variable=emoji_var, width=120)
        emoji_menu.pack(side="left")

        cor_frame = ctk.CTkFrame(frame, fg_color=b["cor"], width=30, height=30, corner_radius=6)
        cor_frame.pack(padx=10, pady=5, side="left")
        cor_label = ctk.CTkLabel(frame, text=b["cor"])
        cor_label.pack(padx=5, side="left")

        def escolher_cor_local(cor_frame=cor_frame, cor_label=cor_label, i=i):
            c = colorchooser.askcolor()[1]
            if c:
                config["botoes"][i]["cor"] = c
                cor_frame.configure(fg_color=c)
                cor_label.configure(text=c)
        ctk.CTkButton(frame, text="🎨 Escolher Cor", width=130, command=escolher_cor_local).pack(side="left", padx=8)

        def escolher_som_local(i=i):
            f = filedialog.askopenfilename(title="Selecionar som", filetypes=[("Áudio", "*.mp3 *.wav *.ogg")])
            if f:
                config["botoes"][i]["arquivo"] = f
                messagebox.showinfo("Som", f"Som selecionado para {config['botoes'][i]['nome']}")
        ctk.CTkButton(frame, text="🎵 Escolher Som", width=150, command=escolher_som_local).pack(side="right", padx=10)

        entries.append((i, nome, emoji_var))

    def salvar_tudo():
        for i, entry, emoji_var in entries:
            texto = entry.get()
            num_chars = len(texto)
            if num_chars > 30:
                messagebox.showerror("Erro", f"O botão '{config['botoes'][i]['nome']}' excede o limite de 30 caracteres ({num_chars} caracteres).\nPor favor, reduza o texto.")
                return
            config["botoes"][i]["nome"] = texto
            config["botoes"][i]["emoji"] = emoji_var.get()
        
        try:
            fi = int(fade_in_entry.get())
            fo = int(fade_out_entry.get())
            if fi < 0 or fo < 0:
                raise ValueError
            config["fade_in_ms"] = fi
            config["fade_out_ms"] = fo
        except Exception:
            messagebox.showerror("Erro", "Os valores de Fade In/Out devem ser números inteiros não negativos (em milissegundos).")
            return
        
        config["atalhos_habilitados"] = atalhos_var.get()
        # Salvar opção de repetição
        config["repeticao_habilitada"] = repeticao_var.get()
        
        salvar_config()
        atualizar_estilos()
        messagebox.showinfo("Configurações", "✅ Alterações salvas com sucesso!")
        win.destroy()

    rodape = ctk.CTkFrame(win, fg_color="#2b2b2b", height=60)
    rodape.pack(fill="x", side="bottom", pady=0)
    rodape.pack_propagate(False)
    
    btn_frame = ctk.CTkFrame(rodape, fg_color="transparent")
    btn_frame.pack(expand=True)
    
    ctk.CTkButton(btn_frame, text="💾 Salvar", fg_color="#16a34a", hover_color="#15803d",
                  width=120, height=35, font=("Arial", 13, "bold"),
                  command=salvar_tudo).pack(side="left", padx=5)
    ctk.CTkButton(btn_frame, text="❌ Cancelar", fg_color="#6b7280", hover_color="#4b5563",
                  width=120, height=35, font=("Arial", 13, "bold"),
                  command=win.destroy).pack(side="left", padx=5)

footer = ctk.CTkFrame(app, fg_color="transparent")
footer.pack(pady=15, fill="x", padx=10)

buttons_frame = ctk.CTkFrame(footer, fg_color="transparent")
buttons_frame.pack(side="left")

# Carregar ícones para os botões
icon_stop = carregar_icone("stop.png", (16, 16))
icon_pause = carregar_icone("pause.png", (16, 16))
icon_config = carregar_icone("config.png", (16, 16))

# Botão Parar com ícone
if icon_stop:
    btn_stop = ctk.CTkButton(buttons_frame, text=" Parar", image=icon_stop, 
                           fg_color="#e74c3c", hover_color="#c0392b", 
                           command=parar_tudo)
else:
    btn_stop = ctk.CTkButton(buttons_frame, text="⏹️ Parar", fg_color="#e74c3c", hover_color="#c0392b", 
                           command=parar_tudo)
btn_stop.pack(side="left", padx=5)

# Botão Pausar/Retomar com ícone
if icon_pause:
    btn_pause = ctk.CTkButton(buttons_frame, text=" Pausar/Retomar", image=icon_pause,
                            fg_color="#f39c12", hover_color="#d35400", 
                            command=pausar_retomar)
else:
    btn_pause = ctk.CTkButton(buttons_frame, text="⏯️ Pausar/Retomar", fg_color="#f39c12", hover_color="#d35400", 
                            command=pausar_retomar)
btn_pause.pack(side="left", padx=5)

# Botão Configurar com ícone
if icon_config:
    btn_config = ctk.CTkButton(buttons_frame, text=" Configurar", image=icon_config,
                              fg_color="#2563eb", hover_color="#1d4ed8", 
                              command=abrir_config_janela)
else:
    btn_config = ctk.CTkButton(buttons_frame, text="⚙️ Configurar", fg_color="#2563eb", hover_color="#1d4ed8", 
                              command=abrir_config_janela)
btn_config.pack(side="left", padx=5)

sobre_frame = ctk.CTkFrame(footer, fg_color="transparent")
sobre_frame.pack(side="right")

ctk.CTkButton(sobre_frame, text="ℹ️ Sobre", 
              fg_color="transparent", 
              text_color=("#4b5563", "#9ca3af"),
              hover_color=("#f3f4f6", "#1f2937"),
              border_width=1,
              border_color=("#e5e7eb", "#374151"),
              command=mostrar_sobre).pack(side="right", padx=5)

shortcuts_label = ctk.CTkLabel(footer, text="", font=("Arial", 11), text_color="#9ca3af")
shortcuts_label.pack(side="right", padx=20)

def atualizar_texto_atalhos():
    if config.get("atalhos_habilitados", True):
        shortcuts_label.configure(text="⌨️ Atalhos: Teclas 1-0 para reproduzir os botões")
    else:
        shortcuts_label.configure(text="⌨️ Atalhos: Desabilitados", text_color="#6b7280")

def on_key(event):
    if not config.get("atalhos_habilitados", True):
        return
    if event.char.isdigit():
        tecla = int(event.char)
        # Mapear teclas 1-0 para botões 0-9
        if tecla == 0:
            index = 9  # Tecla 0 = Botão 10
        else:
            index = tecla - 1  # Tecla 1 = Botão 1, Tecla 2 = Botão 2, etc.
        
        if index < len(button_refs):
            tocar_som(index)
    elif event.keysym == 'space':
        parar_tudo()
app.bind("<Key>", on_key)

app.protocol("WM_DELETE_WINDOW", lambda: (pygame.mixer.music.stop(), app.destroy()))
atualizar_estilos()
atualizar_texto_atalhos()
app.mainloop()
