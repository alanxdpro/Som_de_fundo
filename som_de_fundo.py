import os, sys, json, threading, time, math, webbrowser, zipfile, tempfile, shutil
import pygame
import qrcode
from remote_control import RemoteControlServer
import customtkinter as ctk
from tkinter import filedialog, colorchooser, messagebox, simpledialog
from PIL import Image, ImageTk, ImageDraw, ImageFont

def mostrar_sobre():
    sobre_janela = ctk.CTkToplevel()
    sobre_janela.title("Sobre o Desenvolvedor")
    sobre_janela.geometry("500x400")
    sobre_janela.resizable(False, False)
    sobre_janela.transient(app)
    sobre_janela.grab_set()
    sobre_janela.lift()
    try:
        sobre_janela.attributes("-topmost", True)
        sobre_janela.after(300, lambda: sobre_janela.attributes("-topmost", False))
    except:
        pass
    
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
    ctk.CTkButton(main_frame, text="📘 Manual de Instruções", width=220,
                  fg_color="#2563eb", hover_color="#1d4ed8",
                  command=lambda: mostrar_manual()).pack(pady=(12, 6))
    
    versao = ctk.CTkLabel(
        main_frame,
        text="Versão 1.1.1",
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

def mostrar_manual():
    win = ctk.CTkToplevel(app)
    win.title("Manual de Instruções")
    win.geometry("760x620")
    win.resizable(False, False)
    try:
        win.transient(app); win.grab_set(); win.lift()
    except:
        pass
    def _safe_close_manual():
        try:
            win.grab_release()
        except Exception:
            pass
        try:
            if win and win.winfo_exists():
                win.destroy()
        except Exception:
            pass
    try:
        win.protocol("WM_DELETE_WINDOW", _safe_close_manual)
    except Exception:
        pass
    header = ctk.CTkFrame(win, fg_color="transparent")
    header.pack(fill="x", padx=16, pady=(14, 6))
    ctk.CTkLabel(header, text="Manual de Instruções", font=("Arial", 20, "bold")).pack(side="left")
    body = ctk.CTkScrollableFrame(win, width=720, height=500)
    body.pack(fill="both", expand=True, padx=16, pady=(6, 12))
    txt = (
        "Guia rápido\n"
        "- Escolha a playlist (topo) e clique num card para tocar.\n"
        "- Barra de tempo: passe o mouse para ver o minuto, clique para ir.\n\n"
        "Funcionalidades\n"
        "- Loop por botão: toggle ⟲ com LED verde no rodapé de cada card.\n"
        "- Indicador de estado: mostra ‘Pausado’ ao lado direito da linha de status.\n"
        "- Grade responsiva: escolha 5–8 colunas; tamanhos pequeno/médio/grande com ajuste fino.\n"
        "- Configurações globais: tamanho/colunas/escala aplicam-se a todas as playlists.\n\n"
        "Atalhos\n"
        "- 1–0 (1–10), Q–P (11–20), Espaço (Pausar/Retomar), V (Reinicia com fade).\n"
        "- ←/→ navegam pelas playlists; Enter aplica a playlist mostrada; Esc fecha.\n"
        "- Atalhos são bloqueados ao editar textos/campos de configuração.\n\n"
        "Som e transições\n"
        "- Volume geral no topo; volume por card no rodapé do card.\n"
        "- Seek suave: breve pré‑fade e fade‑in na transição ao clicar na barra.\n"
        "- Fade In/Out, Crossfade e Seek Fade em Configurar > Áudio (Fade).\n\n"
        "Capas e desempenho\n"
        "- Capas otimizadas e cacheadas; sem capa usa imagem padrão.\n"
        "- Preferir imagens leves e arquivos .mp3 para melhor desempenho.\n\n"
        "Backup/Importar\n"
        "- Exportar: selecione playlists; exibe tamanho estimado antes de compactar.\n"
        "- Importar: selecione playlists do zip; caminhos e imagens externas são ajustados.\n\n"
        "Configuração inicial\n"
        "1) Abra ‘Configurar’ (botão azul no rodapé).\n"
        "2) Defina tema (claro/escuro), Fade In/Out, Crossfade e Seek Fade.\n"
        "3) Ajuste grade (colunas) e tamanho dos cards com o ajuste fino.\n"
        "4) Cadastre nomes, cores, imagens e sons por card.\n\n"
        "Procedimentos de teste\n"
        "- Resetar Tudo: confirma a operação, reseta nomes/cores/sons e limpa cache.\n"
        "- Backup: gera .zip com playlists, ícones e sons; mostra progresso e status.\n"
        "- Importar Backup: exige confirmação, permite escolher playlists do zip e restaura dados.\n"
        "- Verifique feedback visual (barra de progresso, mensagens) em todas operações.\n\n"
        "Dicas\n"
        "- Mantenha arquivos organizados; use nomes curtos e consistentes.\n"
        "- Em pausa, ajuste o tempo na barra e retome com a tecla Espaço.\n"
    )
    ctk.CTkLabel(body, text=txt, justify="left", anchor="w").pack(fill="x", padx=8, pady=8)
    try:
        row_imgs = ctk.CTkFrame(body, fg_color="transparent")
        row_imgs.pack(fill="x", padx=8, pady=(0,8))
        ico_play = carregar_icone("pause.png", (20,20)) or carregar_icone("config.png", (20,20))
        ico_reset = carregar_icone("stop.png", (20,20))
        if ico_play:
            ctk.CTkLabel(row_imgs, text="", image=ico_play).pack(side="left", padx=4)
        if ico_reset:
            ctk.CTkLabel(row_imgs, text="", image=ico_reset).pack(side="left", padx=4)
        ctk.CTkLabel(row_imgs, text="Imagens ilustrativas dos controles", font=("Arial", 11)).pack(side="left", padx=8)
    except Exception:
        pass
    footer = ctk.CTkFrame(win, fg_color="transparent")
    footer.pack(fill="x", padx=16, pady=(0, 12))
    ctk.CTkButton(footer, text="Fechar", width=100, command=win.destroy).pack(side="right")

BASE_DIR = getattr(sys, "_MEIPASS", os.path.dirname(os.path.abspath(__file__)))
APP_NAME = "Som_de_fundo"
USER_DATA_DIR = os.path.join(os.path.expanduser("~"), "AppData", "Roaming", APP_NAME)
CONFIG_FILE = os.path.join(USER_DATA_DIR, "config.json")
APP_PREFS_FILE = os.path.join(USER_DATA_DIR, "app_prefs.json")
SONS_DIR = os.path.join(USER_DATA_DIR, "sons")
PLAYLISTS_DIR = os.path.join(USER_DATA_DIR, "playlists")
ICONS_DIR = os.path.join(BASE_DIR, "icons")
FADE_MS = 800
MAX_STORAGE_BYTES = 800 * 1024 * 1024
CONFIG_VERSION = 2

os.makedirs(USER_DATA_DIR, exist_ok=True)
os.makedirs(SONS_DIR, exist_ok=True)
os.makedirs(PLAYLISTS_DIR, exist_ok=True)
pygame.mixer.init()

ICON_CACHE = {}
DEFAULT_COVER_CACHE = {}
CUSTOM_COVER_CACHE = {}
ROUND_MASK_CACHE = {}

def carregar_icone(nome_arquivo, tamanho=(20, 20)):
    """Carrega um ícone da pasta icons e redimensiona se necessário"""
    try:
        caminho = os.path.join(ICONS_DIR, nome_arquivo)
        key = (caminho, tamanho[0], tamanho[1])
        cached = ICON_CACHE.get(key)
        if cached is not None:
            return cached
        if os.path.exists(caminho):
            img = Image.open(caminho)
            img = img.resize(tamanho, Image.Resampling.LANCZOS)
            icon = ctk.CTkImage(light_image=img, dark_image=img, size=tamanho)
            ICON_CACHE[key] = icon
            return icon
    except:
        pass
    return None

config = {}
current_index = None
is_switching = threading.Lock()
music_start_time = None
timer_label = None
loop_label = None
state_label = None
is_paused = False
pause_time = 0
master_volume = 1.0
current_playlist = "default"

remote_label = None
app_prefs = {"appearance_mode": "dark"}
playlist_preview_name = None
playlist_preview_win = None
playlist_preview_label = None
playlist_preview_pos_label = None
playlist_preview_after_id = None
playlist_preview_frame = None
playlist_preview_effect_label = None

def carregar_prefs():
    global app_prefs
    try:
        if os.path.exists(APP_PREFS_FILE):
            with open(APP_PREFS_FILE, "r", encoding="utf-8") as f:
                app_prefs = json.load(f)
        else:
            app_prefs = {"appearance_mode": "dark"}
    except:
        app_prefs = {"appearance_mode": "dark"}
    try:
        if "appearance_mode" not in app_prefs:
            app_prefs["appearance_mode"] = "dark"
        if "card_size" not in app_prefs:
            app_prefs["card_size"] = "medio"
        if "card_scale" not in app_prefs:
            app_prefs["card_scale"] = 1.0
        if "card_scale_min" not in app_prefs:
            app_prefs["card_scale_min"] = 0.8
        if "card_scale_max" not in app_prefs:
            app_prefs["card_scale_max"] = 1.4
        if "grid_columns" not in app_prefs:
            app_prefs["grid_columns"] = 5
    except:
        pass

def salvar_prefs():
    try:
        with open(APP_PREFS_FILE, "w", encoding="utf-8") as f:
            json.dump(app_prefs, f, indent=4, ensure_ascii=False)
    except:
        pass

def aplicar_tema_prefs():
    try:
        mode = app_prefs.get("appearance_mode", "dark")
        ctk.set_appearance_mode(mode)
    except:
        pass

# Modal do WhatsApp removido a pedido do usuário

def _post_to_main(func, *args, **kwargs):
    try:
        app.after(0, lambda: func(*args, **kwargs))
    except:
        pass

def _run_on_main_and_wait(func, timeout=2.0, *args, **kwargs):
    ev = threading.Event()
    def runner():
        try:
            func(*args, **kwargs)
        finally:
            ev.set()
    try:
        app.after(0, runner)
        ev.wait(timeout)
    except:
        pass
    return ev.is_set()


def _show_error(title, err):
    try:
        msg = str(err) if err is not None else ""
        typ = type(err).__name__ if err is not None else "Erro"
        messagebox.showerror(title, f"{msg}\n\n{typ}")
    except:
        pass

toast_overlay = None
toast_after_id = None

def _hide_toast():
    global toast_overlay, toast_after_id
    try:
        if toast_overlay and toast_overlay.winfo_exists():
            toast_overlay.destroy()
    except:
        pass
    toast_overlay = None
    toast_after_id = None

def _show_info_auto(title, text, timeout_ms=5000):
    global toast_overlay, toast_after_id
    try:
        try:
            if toast_overlay and toast_overlay.winfo_exists():
                toast_overlay.destroy()
        except:
            pass
        frame = ctk.CTkFrame(app, corner_radius=20, fg_color=("#f3f4f6", "#1e293b"))
        lbl = ctk.CTkLabel(frame, text=text, font=("Arial", 12))
        lbl.pack(padx=18, pady=10)
        app.update_idletasks()
        reqw = max(220, lbl.winfo_reqwidth() + 36)
        reqh = max(50, lbl.winfo_reqheight() + 28)
        aw = app.winfo_width() or 800
        ah = app.winfo_height() or 600
        x = max(0, (aw // 2) - (reqw // 2))
        y = max(0, (ah // 2) - (reqh // 2))
        frame.place(x=x, y=y, width=reqw, height=reqh)
        toast_overlay = frame
        try:
            if toast_after_id:
                app.after_cancel(toast_after_id)
        except:
            pass
        toast_after_id = app.after(timeout_ms, _hide_toast)
    except:
        pass


def trocar_playlist_remoto(nova_playlist):
    global current_playlist, config
    parar_tudo()
    current_playlist = nova_playlist
    carregar_config()
    atualizar_estilos()
    atualizar_combo_playlists()
    try:
        recriar_botoes()
    except Exception:
        pass

def default_config():
    paleta_cores = [
        '#3b82f6', '#8b5cf6', '#06b6d4', '#10b981', '#ef4444',
        '#f59e0b', '#ec4899', '#14b8a6', '#f97316', '#6366f1'
    ]
    
    return {
        "botoes": [
            {"nome": f"Botão {i+1}", "cor": paleta_cores[i], "arquivo": "", "icone": "", "volume": 1.0,
             "imagem": "", "imagem_cache": "", "texto_cor": "#ffffff", "loop": False}
            for i in range(10)
        ],
        "atalhos_habilitados": True,
        "fade_in_ms": 800,
        "fade_out_ms": 800,
        "crossfade_ms": 400,
        "seek_fade_ms": 50,
        "repeticao_habilitada": False,
        "card_size": "medio",
        "card_scale": 1.0,
        "card_scale_min": 0.8,
        "card_scale_max": 1.4,
        "grid_columns": 5,
        "master_volume": 1.0,
        "config_version": CONFIG_VERSION
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
        try:
            v = int(config.get("config_version", 0))
        except:
            v = 0
        if v < CONFIG_VERSION:
            config["config_version"] = CONFIG_VERSION
            changed = True
        if "atalhos_habilitados" not in config:
            config["atalhos_habilitados"] = True
            changed = True
        if "fade_in_ms" not in config:
            config["fade_in_ms"] = 800
            changed = True
        if "fade_out_ms" not in config:
            config["fade_out_ms"] = 800
            changed = True
        if "crossfade_ms" not in config:
            config["crossfade_ms"] = 400
            changed = True
        if "seek_fade_ms" not in config:
            config["seek_fade_ms"] = 200
            changed = True
        if "repeticao_habilitada" not in config:
            config["repeticao_habilitada"] = False
            changed = True
        if "card_size" not in config:
            config["card_size"] = "medio"
            changed = True
        if "card_scale" not in config:
            config["card_scale"] = 1.0
            changed = True
        if "card_scale_min" not in config:
            config["card_scale_min"] = 0.8
            changed = True
        if "card_scale_max" not in config:
            config["card_scale_max"] = 1.4
            changed = True
        if "grid_columns" not in config:
            config["grid_columns"] = 5
            changed = True
        try:
            config["card_size"] = app_prefs.get("card_size", config.get("card_size", "medio"))
            config["card_scale"] = float(app_prefs.get("card_scale", config.get("card_scale", 1.0)))
            config["card_scale_min"] = float(app_prefs.get("card_scale_min", config.get("card_scale_min", 0.8)))
            config["card_scale_max"] = float(app_prefs.get("card_scale_max", config.get("card_scale_max", 1.4)))
            config["grid_columns"] = int(app_prefs.get("grid_columns", config.get("grid_columns", 5)))
        except Exception:
            pass
        if "master_volume" not in config:
            config["master_volume"] = 1.0
            changed = True
        
        for idx, b in enumerate(config.get("botoes", [])):
            if "volume" not in b:
                b["volume"] = 1.0
                changed = True
            if "imagem" not in b:
                b["imagem"] = ""
                changed = True
            if "imagem_cache" not in b:
                b["imagem_cache"] = ""
                changed = True
            if "texto_cor" not in b:
                b["texto_cor"] = "#ffffff"
                changed = True
            if "loop" not in b:
                b["loop"] = False
                changed = True
            try:
                expected = os.path.join(ICONS_DIR, current_playlist, f"btn{idx+1}.jpg")
                cache_ok = b.get("imagem_cache") and os.path.normpath(b.get("imagem_cache")) == os.path.normpath(expected) and os.path.exists(expected)
                if b.get("imagem") and not cache_ok:
                    processar_imagem_botao(idx, b.get("imagem"))
                    changed = True
                elif not b.get("imagem"):
                    b["imagem_cache"] = ""
                    changed = True
            except:
                pass
        
        if changed:
            salvar_config()
    
    master_volume = config.get("master_volume", 1.0)
    try:
        if not pygame.mixer.music.get_busy():
            pygame.mixer.music.set_volume(master_volume)
    except Exception:
        pass

def salvar_config():
    playlist_file = os.path.join(PLAYLISTS_DIR, f"{current_playlist}.json")
    config["master_volume"] = master_volume
    try:
        config["config_version"] = CONFIG_VERSION
    except:
        pass
    persist = dict(config)
    for k in ("card_size", "card_scale", "card_scale_min", "card_scale_max", "grid_columns"):
        try:
            if k in persist:
                del persist[k]
        except Exception:
            pass
    with open(playlist_file, "w", encoding="utf-8") as f:
        json.dump(persist, f, indent=4, ensure_ascii=False)

def listar_playlists():
    playlists = []
    for file in os.listdir(PLAYLISTS_DIR):
        if file.endswith(".json"):
            playlists.append(file.replace(".json", ""))
    return playlists if playlists else ["default"]

def trocar_playlist(nova_playlist, silent=False):
    global current_playlist, config
    parar_tudo()
    current_playlist = nova_playlist
    carregar_config()
    atualizar_combo_playlists()
    if not silent:
        _show_info_auto("Playlist", f"Playlist '{nova_playlist}' carregada com sucesso!")
    try:
        recriar_botoes()
    except Exception:
        pass
    atualizar_estilos()

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
            atualizar_combo_playlists()
            _show_info_auto("Playlist", f"Playlist '{nome}' criada com sucesso!")
            try:
                recriar_botoes()
            except Exception:
                pass
            atualizar_estilos()

def duplicar_playlist():
    nome = simpledialog.askstring("Duplicar Playlist", f"Digite o nome para a cópia de '{current_playlist}':")
    if nome:
        nome = nome.strip().replace(" ", "_")
        if nome:
            nova_playlist_file = os.path.join(PLAYLISTS_DIR, f"{nome}.json")
            playlist_atual_file = os.path.join(PLAYLISTS_DIR, f"{current_playlist}.json")
            
            with open(playlist_atual_file, "r", encoding="utf-8") as f:
                config_copia = json.load(f)
            
            for k in ("card_size", "card_scale", "card_scale_min", "card_scale_max", "grid_columns"):
                try:
                    if k in config_copia:
                        del config_copia[k]
                except Exception:
                    pass
            with open(nova_playlist_file, "w", encoding="utf-8") as f:
                json.dump(config_copia, f, indent=4, ensure_ascii=False)
            
            atualizar_combo_playlists()
            _show_info_auto("Playlist", f"Playlist duplicada como '{nome}'!")

def excluir_playlist():
    if current_playlist == "default":
        resposta = messagebox.askyesno("Limpar Cache", "A playlist 'default' não pode ser excluída.\n\nDeseja limpar o cache de ícones desta playlist?")
        if resposta:
            try:
                base = os.path.join(ICONS_DIR, current_playlist)
                if os.path.isdir(base):
                    for nome in os.listdir(base):
                        p = os.path.join(base, nome)
                        try:
                            if os.path.isfile(p):
                                os.remove(p)
                        except Exception:
                            pass
                    try:
                        os.rmdir(base)
                    except Exception:
                        pass
            except Exception:
                pass
            _show_info_auto("Cache", "Cache de ícones da playlist 'default' limpo!")
        return
    
    resposta = messagebox.askyesno("Excluir Playlist", f"Tem certeza que deseja excluir a playlist '{current_playlist}'?")
    if resposta:
        playlist_file = os.path.join(PLAYLISTS_DIR, f"{current_playlist}.json")
        if os.path.exists(playlist_file):
            os.remove(playlist_file)
        try:
            base = os.path.join(ICONS_DIR, current_playlist)
            if os.path.isdir(base):
                for nome in os.listdir(base):
                    p = os.path.join(base, nome)
                    try:
                        if os.path.isfile(p):
                            os.remove(p)
                    except Exception:
                        pass
                try:
                    os.rmdir(base)
                except Exception:
                    pass
        except Exception:
            pass
        trocar_playlist("default")
        _show_info_auto("Playlist", f"Playlist excluída com sucesso!")

def _play_file_loop(index, path, volume):
    global is_paused
    try:
        pygame.mixer.music.load(path)
        volume_final = volume * master_volume
        pygame.mixer.music.set_volume(volume_final)
        repetir_global = config.get("repeticao_habilitada", False)
        try:
            btn_loop = bool(config["botoes"][index].get("loop", False))
        except Exception:
            btn_loop = False
        loops = -1 if (btn_loop or repetir_global) else 0
        pygame.mixer.music.play(loops=loops, fade_ms=config.get("fade_in_ms", FADE_MS))
        is_paused = False
    except Exception as e:
        _show_error("Erro de áudio", e)

def abrir_arquivo(index):
    """Abre a janela de seleção de arquivo para o botão especificado"""
    f = filedialog.askopenfilename(
        title=f"Selecionar som para {config['botoes'][index]['nome']}", 
        filetypes=[("Áudio", "*.mp3 *.wav *.ogg")]
    )
    if f:
        if validar_arquivo_audio(f):
            config["botoes"][index]["arquivo"] = f
            try:
                config["botoes"][index]["duracao"] = obter_duracao_musica(f)
            except:
                pass
            salvar_config()
            _show_info_auto("Sucesso", f"Arquivo adicionado ao botão {config['botoes'][index]['nome']}")

def resolve_audio_path(p):
    try:
        if p and os.path.exists(p):
            return p
        base = os.path.basename(p) if p else ""
        if base:
            candidate = os.path.join(SONS_DIR, base)
            if os.path.exists(candidate):
                return candidate
            for root, _, files in os.walk(SONS_DIR):
                for fn in files:
                    if fn.lower() == base.lower():
                        return os.path.join(root, fn)
    except Exception:
        pass
    return p

def tocar_som(index):
    global current_index
    botao = config["botoes"][index]
    caminho_orig = botao["arquivo"]
    caminho = resolve_audio_path(caminho_orig)
    if caminho != caminho_orig:
        config["botoes"][index]["arquivo"] = caminho
        salvar_config()
    if not caminho or not os.path.exists(caminho):
        resposta = messagebox.askyesno("Arquivo não encontrado", 
                                     f"Nenhum arquivo de som definido para este botão.\n\nDeseja adicionar um arquivo agora?")
        if resposta:
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
        prev_index = current_index
        if pygame.mixer.music.get_busy():
            fade_out = int(config.get("fade_out_ms", FADE_MS))
            overlap = int(config.get("crossfade_ms", 0))
            overlap = max(0, min(overlap, max(0, fade_out - 50)))
            wait_ms = max(0, fade_out - overlap)
            pygame.mixer.music.fadeout(fade_out)
            if wait_ms > 0:
                time.sleep(wait_ms / 1000)
        _play_file_loop(index, caminho, volume)
        current_index = index
        music_start_time = time.time()
        try:
            _atualizar_estilo_botao(prev_index)
            _atualizar_estilo_botao(index)
        except Exception:
            atualizar_estilos()
        atualizar_timer()
        try:
            if state_label:
                state_label.configure(text="")
        except Exception:
            pass
        try:
            if paused_song_label:
                paused_song_label.configure(text="")
        except Exception:
            pass
    finally:
        is_switching.release()

# Controle remoto: estado para API
def get_remote_state():
    botoes = []
    for i, b in enumerate(config.get("botoes", [])):
        img_cache = b.get("imagem_cache")
        base_dir = os.path.join(ICONS_DIR, current_playlist)
        use_img = bool(img_cache and os.path.exists(img_cache) and os.path.normpath(img_cache).startswith(os.path.normpath(base_dir)))
        icon = f"/icon/{current_playlist}/{i+1}" if use_img else "/icon/_default"
        botoes.append({
            "index": i,
            "nome": b.get("nome", f"Botão {i+1}"),
            "cor": b.get("cor", "#2563eb"),
            "ativo": current_index == i,
            "icon": icon,
        })
    return {
        "playlist": current_playlist,
        "playlists": listar_playlists(),
        "botoes": botoes,
        "tocando": bool(pygame.mixer.music.get_busy()),
        "paused": is_paused,
        "master_volume": master_volume,
        "volume_percent": int(master_volume * 100),
    }

def obter_duracao_musica(caminho):
    try:
        sound = pygame.mixer.Sound(caminho)
        return int(sound.get_length())
    except:
        return 0

def validar_arquivo_audio(path):
    try:
        ext = os.path.splitext(path)[1].lower()
        if ext not in (".mp3", ".wav", ".ogg"):
            messagebox.showerror("Áudio", "Formato não suportado. Use .mp3, .wav ou .ogg.")
            return False
        try:
            size = os.path.getsize(path)
            mb = max(1, int(size / (1024 * 1024)))
            if size > MAX_STORAGE_BYTES:
                messagebox.showerror("Áudio", "O arquivo excede 800 MB. Por favor, use um áudio menor.")
                return False
            if size > 40 * 1024 * 1024:
                messagebox.showwarning("Áudio", f"Arquivo grande (~{mb} MB). Se o seu computador não for muito potente, pode haver pequenas travadinhas durante a reprodução. Isso é normal — prefira arquivos menores quando possível.")
        except Exception as e:
            _show_error("Erro ao verificar tamanho", e)
            return False
        try:
            pygame.mixer.Sound(path)
        except Exception as e:
            _show_error("Áudio não suportado", e)
            return False
        return True
    except Exception as e:
        _show_error("Erro ao validar áudio", e)
        return False

def formatar_tempo(segundos):
    minutos = int(segundos) // 60
    segundos = int(segundos) % 60
    return f"{minutos:02d}:{segundos:02d}"

def atualizar_timer():
    global music_start_time, timer_label, progressBar_musica, current_index
    if music_start_time and (pygame.mixer.music.get_busy() or is_paused):
        elapsed = int(pause_time or 0) if is_paused else int(time.time() - music_start_time)
        if current_index is not None and "arquivo" in config["botoes"][current_index]:
            caminho = resolve_audio_path(config["botoes"][current_index]["arquivo"]) 
            nome_arquivo = os.path.basename(caminho) if caminho else ""
            nome_musica = os.path.splitext(nome_arquivo)[0] if nome_arquivo else ""
            if "duracao" not in config["botoes"][current_index]:
                duracao_total = obter_duracao_musica(caminho) if caminho and os.path.exists(caminho) else 0
                config["botoes"][current_index]["duracao"] = duracao_total
                try:
                    salvar_config()
                except:
                    pass
            else:
                duracao_total = config["botoes"][current_index]["duracao"]
            try:
                repetir = bool(config.get("repeticao_habilitada", False))
            except Exception:
                repetir = False
            try:
                per_loop = bool(config["botoes"][current_index].get("loop", False))
            except Exception:
                per_loop = False
            if (repetir or per_loop) and duracao_total and duracao_total > 0:
                try:
                    elapsed = int(elapsed % max(1, int(duracao_total)))
                except Exception:
                    pass
            tempo_atual = formatar_tempo(elapsed)
            tempo_total = formatar_tempo(duracao_total) if duracao_total > 0 else "--:--"
            label_loop = ""
            try:
                timer_label.configure(text=f" {nome_musica}{label_loop} | {tempo_atual} / {tempo_total}")
            except Exception:
                pass
            try:
                if loop_label:
                    loop_label.configure(text=("loop ativado" if (per_loop or repetir) else ""))
            except Exception:
                pass
            try:
                if paused_song_label:
                    if is_paused:
                        paused_song_label.configure(text="Pausado")
                    else:
                        paused_song_label.configure(text="")
            except Exception:
                pass
            progress = 0.0
            if duracao_total > 0:
                try:
                    progress = min(elapsed / duracao_total, 1.0)
                except Exception:
                    progress = 0.0
            try:
                progressBar_musica.set(progress)
            except Exception:
                pass
        if pygame.mixer.music.get_busy():
            app.after(1000, atualizar_timer)
    elif timer_label:
        timer_label.configure(text="00:00 / 00:00")
        try:
            progressBar_musica.set(0)
        except Exception:
            pass
        try:
            if loop_label:
                loop_label.configure(text="")
        except Exception:
            pass
        try:
            if state_label:
                state_label.configure(text="")
        except Exception:
            pass
        try:
            if paused_song_label:
                paused_song_label.configure(text="")
        except Exception:
            pass
        music_start_time = None
        current_index = None
        atualizar_estilos()

def fade_in(step=0.0):
    if not is_paused and current_index is not None and pygame.mixer.music.get_busy():
        volume_botao = config["botoes"][current_index].get("volume", 1.0)
        target_volume = volume_botao * master_volume
        total_ms = int(config.get("fade_in_ms", FADE_MS))
        interval = 30
        delta = target_volume if total_ms <= interval else max(target_volume / max(1, (total_ms // interval)), 0.01)
        current_vol = min(step, target_volume)
        pygame.mixer.music.set_volume(current_vol)
        if current_vol < target_volume:
            app.after(interval, lambda: fade_in(current_vol + delta))

def _get_target_volume_for_current():
    try:
        if current_index is None:
            return master_volume
        volume_botao = float(config["botoes"][current_index].get("volume", 1.0))
        return max(0.0, min(1.0, volume_botao * master_volume))
    except Exception:
        return master_volume

def _fade_volume_to(target, ms=120):
    try:
        target = max(0.0, min(1.0, float(target)))
        if ms <= 0:
            pygame.mixer.music.set_volume(target)
            return
        steps = max(1, int(ms // 20))
        try:
            cur = float(pygame.mixer.music.get_volume())
        except Exception:
            cur = target
        delta = (target - cur) / steps if steps > 0 else 0
        def _step(n=0, v=cur):
            try:
                nv = v + delta
                pygame.mixer.music.set_volume(max(0.0, min(1.0, nv)))
                if n + 1 < steps:
                    app.after(20, lambda: _step(n+1, nv))
            except Exception:
                pass
        _step()
    except Exception:
        pass

def pausar_retomar():
    global music_start_time, is_paused, current_index, pause_time
    
    if not is_paused and pygame.mixer.music.get_busy():
        fade_out_time = config.get("fade_out_ms", FADE_MS)
        pygame.mixer.music.fadeout(fade_out_time)
        is_paused = True
        if music_start_time is not None:
            pause_time = time.time() - music_start_time
        try:
            if state_label:
                state_label.configure(text="Pausado")
        except Exception:
            pass
        try:
            if paused_song_label:
                paused_song_label.configure(text="Pausado")
        except Exception:
            pass
    elif is_paused and current_index is not None:
        botao = config["botoes"][current_index]
        caminho = resolve_audio_path(botao["arquivo"]) 
        volume = botao.get("volume", 1.0)
        
        pygame.mixer.music.stop()
        
        try:
            pygame.mixer.music.load(caminho)
            pygame.mixer.music.set_volume(0)
            
            posicao_segundos = pause_time if pause_time is not None else 0
            repetir_global = config.get("repeticao_habilitada", False)
            try:
                btn_loop = bool(config["botoes"][current_index].get("loop", False))
            except Exception:
                btn_loop = False
            loops = -1 if (btn_loop or repetir_global) else 0
            pygame.mixer.music.play(loops=loops, start=posicao_segundos)
            
            is_paused = False
            music_start_time = time.time() - posicao_segundos
            
            fade_in(0.0)
            
            atualizar_timer()
            try:
                if state_label:
                    state_label.configure(text="")
            except Exception:
                pass
            
        except Exception as e:
            _show_error("Erro ao retomar", e)
            is_paused = True
    
    atualizar_estilos()

def reiniciar_musica():
    global music_start_time, is_paused, pause_time
    if current_index is None:
        return
    botao = config["botoes"][current_index]
    caminho = resolve_audio_path(botao["arquivo"]) 
    volume = botao.get("volume", 1.0)
    if not caminho or not os.path.exists(caminho):
        return
    try:
        fade_ms = 500
        busy = bool(pygame.mixer.music.get_busy())
        if busy:
            pygame.mixer.music.fadeout(fade_ms)
        else:
            fade_ms = 0
        try:
            d = int(botao.get("duracao") or 0)
            if d <= 0 and caminho and os.path.exists(caminho):
                d = obter_duracao_musica(caminho)
                botao["duracao"] = d
                try:
                    salvar_config()
                except Exception:
                    pass
            if timer_label:
                nome_arquivo = os.path.basename(caminho)
                nome_musica = os.path.splitext(nome_arquivo)[0]
                timer_label.configure(text=f" {nome_musica} | 00:00 / {formatar_tempo(d) if d>0 else '--:--'}")
            try:
                progressBar_musica.set(0)
            except Exception:
                pass
            music_start_time = time.time()
        except Exception:
            pass
        def _do_restart():
            try:
                pygame.mixer.music.stop()
                pygame.mixer.music.load(caminho)
                pygame.mixer.music.set_volume(0)
                repetir_global = config.get("repeticao_habilitada", False)
                try:
                    btn_loop = bool(config["botoes"][current_index].get("loop", False))
                except Exception:
                    btn_loop = False
                loops = -1 if (btn_loop or repetir_global) else 0
                pygame.mixer.music.play(loops=loops, fade_ms=0)
                is_paused = False
                pause_time = 0
                music_start_time = time.time()
                try:
                    d = int(botao.get("duracao") or 0)
                    if d <= 0:
                        d = obter_duracao_musica(caminho)
                        botao["duracao"] = d
                        salvar_config()
                    if timer_label:
                        nome_arquivo = os.path.basename(caminho)
                        nome_musica = os.path.splitext(nome_arquivo)[0]
                        timer_label.configure(text=f" {nome_musica} | 00:00 / {formatar_tempo(d) if d>0 else '--:--'}")
                    try:
                        progressBar_musica.set(0)
                    except Exception:
                        pass
                except Exception:
                    pass
                fade_in(0.0)
                atualizar_timer()
                atualizar_estilos()
            except Exception as e:
                _show_error("Erro ao reiniciar", e)
        if fade_ms > 0:
            app.after(fade_ms, _do_restart)
        else:
            _do_restart()
    except Exception as e:
        _show_error("Erro ao reiniciar", e)
    try:
        if state_label:
            state_label.configure(text="")
    except Exception:
        pass

def parar_tudo():
    global current_index, music_start_time, is_paused, progressBar_musica
    if pygame.mixer.music.get_busy() or is_paused:
        fade_out_time = config.get("fade_out_ms", FADE_MS)
        pygame.mixer.music.fadeout(fade_out_time)
        is_paused = False
    music_start_time = None
    if timer_label:
        timer_label.configure(text="00:00 / 00:00")
    try:
        progressBar_musica.set(0)
    except Exception:
        pass
    current_index = None
    atualizar_estilos()
    try:
        if state_label:
            state_label.configure(text="")
    except Exception:
        pass



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

def set_master_volume_smooth(target, duration_ms=500):
    try:
        target = max(0.0, min(1.0, float(target)))
    except:
        return
    current = float(master_volume)
    steps = max(1, int(duration_ms / 50))
    delta = (target - current) / steps
    def _step(i=0, v=current):
        nv = max(0.0, min(1.0, v + delta))
        atualizar_volume_master(nv)
        if i < steps - 1:
            app.after(50, lambda: _step(i+1, nv))
    _step()

def remote_set_master_volume(value):
    set_master_volume_smooth(value, 500)

def remote_delta_master_volume(delta):
    try:
        delta = float(delta)
    except:
        delta = 0.0
    set_master_volume_smooth(max(0.0, min(1.0, master_volume + delta)), 500)

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

try:
    ico_path = os.path.join(os.path.dirname(__file__), "icone.ico")
    if os.path.exists(ico_path):
        app.iconbitmap(ico_path)
    else:
        alt_ico = os.path.join(ICONS_DIR, "app_icon.ico")
        if os.path.exists(alt_ico):
            app.iconbitmap(alt_ico)
except:
    pass

carregar_prefs()
carregar_config()
aplicar_tema_prefs()
button_refs = []
volume_sliders = []
key_hint_labels = []
loop_led_labels = []

# Inicializar servidor de controle remoto
server = RemoteControlServer(
    port=5005,
    get_state=get_remote_state,
    play=tocar_som,
    stop=parar_tudo,
    pause=pausar_retomar,
    switch_playlist=trocar_playlist_remoto,
    post_to_main=_post_to_main,
    run_on_main_and_wait=_run_on_main_and_wait,
    set_volume=remote_set_master_volume,
    delta_volume=remote_delta_master_volume,
    icons_dir=ICONS_DIR,
)

header_frame = ctk.CTkFrame(app, fg_color="transparent")
header_frame.pack(pady=20, fill="x", padx=20)

header = ctk.CTkLabel(header_frame, text="🎚️ SOM DE FUNDO PRO", font=("Arial Rounded MT Bold", 26))
header.pack(side="left", expand=True)

musica_frame = ctk.CTkFrame(header_frame, fg_color="transparent")
# Botão Sobre no canto superior direito
try:
    sobre_top_btn = ctk.CTkButton(header_frame, text="ℹ️ Sobre",
                                  fg_color="transparent",
                                  text_color=("#4b5563", "#9ca3af"),
                                  hover_color=("#f3f4f6", "#1f2937"),
                                  border_width=1,
                                  border_color=("#e5e7eb", "#374151"),
                                  width=80,
                                  command=mostrar_sobre)
    sobre_top_btn.pack(side="right", padx=5)
except Exception:
    pass
musica_frame.pack(side="right", expand=True)


timer_label = ctk.CTkLabel(musica_frame, text="00:00 / 00:00", font=("Arial", 12), text_color="#9ca3af", anchor="e")
timer_label.pack(side="top", padx=20, pady=5)
status_row = ctk.CTkFrame(musica_frame, fg_color="transparent")
status_row.pack(side="top", fill="x", padx=10, pady=(0, 2))
loop_label = ctk.CTkLabel(status_row, text="", font=("Arial", 11), text_color="#16a34a")
loop_label.pack(side="left", padx=8)
paused_song_label = ctk.CTkLabel(status_row, text="", font=("Arial", 12, "bold"), text_color="#ef4444")
paused_song_label.pack(side="right", padx=8)
state_label = ctk.CTkLabel(musica_frame, text="", font=("Arial", 12, "bold"), text_color="#ef4444")
state_label.pack_forget()

progressBar_musica = ctk.CTkProgressBar(musica_frame, width=600)
progressBar_musica.pack(side="bottom", pady=10, padx=10)
progressBar_musica.set(0)

def _on_progress_click(event):
    try:
        global music_start_time, is_paused, current_index
        if current_index is None:
            return
        botao = config["botoes"][current_index]
        caminho = resolve_audio_path(botao.get("arquivo"))
        if not caminho or not os.path.exists(caminho):
            return
        dur = botao.get("duracao")
        if not dur or dur <= 0:
            try:
                dur = obter_duracao_musica(caminho)
                botao["duracao"] = dur
                salvar_config()
            except Exception:
                dur = 0
        w = max(1, progressBar_musica.winfo_width())
        ratio = max(0.0, min(1.0, event.x / w))
        pos = int(ratio * (dur or 0))
        nome_arquivo = os.path.basename(caminho)
        nome_musica = os.path.splitext(nome_arquivo)[0]
        tempo_pos = formatar_tempo(pos)
        tempo_total = formatar_tempo(dur) if dur > 0 else "--:--"
        try:
            timer_label.configure(text=f" {nome_musica} | {tempo_pos} / {tempo_total}")
        except Exception:
            pass
        try:
            if is_paused:
                globals()['pause_time'] = pos
                progressBar_musica.set((pos / dur) if dur > 0 else 0)
                atualizar_timer()
            else:
                prefade = int(config.get("seek_prefade_ms", 100))
                seek_fade = min(180, int(config.get("seek_fade_ms", 180)))
                repetir_global = config.get("repeticao_habilitada", False)
                try:
                    btn_loop = bool(botao.get("loop", False))
                except Exception:
                    btn_loop = False
                loops = -1 if (btn_loop or repetir_global) else 0
                vol_target = _get_target_volume_for_current()
                try:
                    _fade_volume_to(0.0, ms=prefade)
                except Exception:
                    pass
                def _do_seek():
                    try:
                        pygame.mixer.music.set_volume(vol_target)
                        pygame.mixer.music.play(loops=loops, start=pos, fade_ms=seek_fade)
                    except Exception:
                        try:
                            pygame.mixer.music.load(caminho)
                            pygame.mixer.music.set_volume(0)
                            pygame.mixer.music.play(loops=loops, start=pos, fade_ms=seek_fade)
                            pygame.mixer.music.set_volume(vol_target)
                        except Exception as e:
                            _show_error("Erro ao buscar posição", e)
                app.after(prefade, _do_seek)
                is_paused = False
                music_start_time = time.time() - pos
                atualizar_timer()
        except Exception as e:
            _show_error("Erro ao buscar posição", e)
    except Exception:
        pass

try:
    progressBar_musica.bind("<Button-1>", _on_progress_click)
except Exception:
    pass

hover_time_label = ctk.CTkLabel(musica_frame, text="", font=("Arial", 11), fg_color=("#f3f4f6", "#334155"), text_color=("#111827", "#e5e7eb"))
HOVER_LABEL_ALIGN = "center"

def _on_progress_motion(event):
    try:
        if progressBar_musica.winfo_ismapped() is False:
            return
        dur = 0
        if current_index is not None:
            try:
                d = int(config["botoes"][current_index].get("duracao") or 0)
                dur = max(0, d)
            except Exception:
                dur = 0
        w = max(1, progressBar_musica.winfo_width())
        ex = max(0, min(event.x, w))
        ratio = max(0.0, min(1.0, ex / w))
        pos = int(ratio * dur) if dur > 0 else 0
        txt = formatar_tempo(pos) if dur > 0 else "--:--"
        hover_time_label.configure(text=txt)
        try:
            label_w = max(30, hover_time_label.winfo_reqwidth())
            label_h = max(14, hover_time_label.winfo_reqheight())
            parent_w = musica_frame.winfo_width()
            parent_h = musica_frame.winfo_height()
            bar_x = progressBar_musica.winfo_x()
            bar_y = progressBar_musica.winfo_y()
            if HOVER_LABEL_ALIGN == "center":
                base_x = bar_x + (w // 2) - (label_w // 2)
            else:
                base_x = bar_x + ex - label_w - 12
            base_y = bar_y + event.y - label_h - 8
            min_x = 0
            max_x = max(min_x, parent_w - label_w - 6)
            min_y = 0
            max_y = max(min_y, parent_h - label_h - 6)
            x = min(max_x, max(min_x, base_x))
            y = min(max_y, max(min_y, base_y))
            hover_time_label.place(x=x, y=y)
            hover_time_label.lift()
        except Exception:
            pass
    except Exception:
        pass

def _on_progress_enter(event):
    try:
        _on_progress_motion(event)
    except Exception:
        pass

def _on_progress_leave(event):
    try:
        hover_time_label.place_forget()
    except Exception:
        pass

try:
    progressBar_musica.bind("<Motion>", _on_progress_motion)
    progressBar_musica.bind("<Enter>", _on_progress_enter)
    progressBar_musica.bind("<Leave>", _on_progress_leave)
except Exception:
    pass

playlist_frame = ctk.CTkFrame(app, fg_color=("#e5e7eb", "#1e293b"), height=50)
playlist_frame.pack(fill="x", padx=20, pady=(0, 10))

ctk.CTkLabel(playlist_frame, text="📁 Playlist:", font=("Arial", 13, "bold")).pack(side="left", padx=(10, 5))

playlist_combo = ctk.CTkOptionMenu(playlist_frame, values=listar_playlists(), width=200,
                                   command=lambda choice: trocar_playlist(choice))
playlist_combo.set(current_playlist)
playlist_combo.pack(side="left", padx=5)
# Bloquear setas verticais no menu para evitar troca imediata
try:
    playlist_combo.bind('<Up>', lambda e: 'break')
    playlist_combo.bind('<Down>', lambda e: 'break')
except Exception:
    pass
# CTkOptionMenu não entra em modo edição; não é necessário bloquear teclas

ctk.CTkButton(playlist_frame, text="➕ Nova", width=80, height=28,
              command=criar_nova_playlist).pack(side="left", padx=2)
ctk.CTkButton(playlist_frame, text="📋 Duplicar", width=90, height=28,
              command=duplicar_playlist).pack(side="left", padx=2)
ctk.CTkButton(playlist_frame, text="🗑️ Excluir", width=80, height=28, fg_color="#dc2626",
              command=excluir_playlist).pack(side="left", padx=2)
ctk.CTkButton(playlist_frame, text="✏️ Editar", width=80, height=28,
              command=lambda: renomear_playlist()).pack(side="left", padx=2)

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

panel = ctk.CTkScrollableFrame(app)
panel.pack(expand=True, fill="both", padx=20, pady=10)

def atualizar_estilos():
    try:
        limit = min(len(button_refs), len(config.get("botoes", [])))
        for i in range(limit):
            ref = button_refs[i]
            cor = config["botoes"][i].get("cor", "#2563eb")
            nome = config["botoes"][i].get("nome", f"Botão {i+1}")
            ref.configure(fg_color=cor, text=quebrar_texto(nome))
            if current_index == i:
                ref.configure(border_color="#ffffff", border_width=3)
                cor_rgb = tuple(int(cor.lstrip('#')[i:i+2], 16) for i in (0, 2, 4))
                cor_clara = f"#{min(cor_rgb[0]+30, 255):02x}{min(cor_rgb[1]+30, 255):02x}{min(cor_rgb[2]+30, 255):02x}"
                ref.configure(hover_color=cor_clara)
            else:
                ref.configure(border_color=cor, border_width=0)
                ref.configure(hover_color=cor)
            try:
                if i < len(loop_led_labels):
                    led = loop_led_labels[i]
                    if led:
                        val = bool(config["botoes"][i].get("loop", False))
                        led.configure(text_color=("#22c55e" if val else "#6b7280"))
            except Exception:
                pass
    except Exception:
        pass
    
    atualizar_texto_atalhos()

def _atualizar_estilo_botao(i):
    try:
        if i is None or i < 0 or i >= len(button_refs):
            return
        ref = button_refs[i]
        cor = config["botoes"][i].get("cor", "#2563eb")
        nome = config["botoes"][i].get("nome", f"Botão {i+1}")
        ref.configure(fg_color=cor, text=quebrar_texto(nome))
        if current_index == i:
            ref.configure(border_color="#ffffff", border_width=3)
            cor_rgb = tuple(int(cor.lstrip('#')[i:i+2], 16) for i in (0, 2, 4))
            cor_clara = f"#{min(cor_rgb[0]+30, 255):02x}{min(cor_rgb[1]+30, 255):02x}{min(cor_rgb[2]+30, 255):02x}"
            ref.configure(hover_color=cor_clara)
        else:
            ref.configure(border_color=cor, border_width=0)
            ref.configure(hover_color=cor)
        try:
            if i < len(loop_led_labels):
                led = loop_led_labels[i]
                if led:
                    val = bool(config["botoes"][i].get("loop", False))
                    led.configure(text_color=("#22c55e" if val else "#6b7280"))
        except Exception:
            pass
    except Exception:
        pass

def _hex_to_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))

def _lighten_hex(h, amt=30):
    r, g, b = _hex_to_rgb(h)
    return f"#{min(r+amt,255):02x}{min(g+amt,255):02x}{min(b+amt,255):02x}"

def _make_placeholder(size, cor):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    radius = max(10, size//12)
    fill = _lighten_hex(cor, 20)
    d.rounded_rectangle([0, 0, size-1, size-1], radius=radius, fill=fill, outline="#ffffff", width=max(2, size//40))
    return img

def _load_default_cover(size, cor):
    try:
        p = os.path.join(ICONS_DIR, "sem_capa.png")
        if os.path.exists(p):
            key = ("sem_capa", size)
            cached = DEFAULT_COVER_CACHE.get(key)
            if cached is not None:
                return cached
            img = Image.open(p).convert("RGBA")
            img = img.resize((size, size), Image.Resampling.LANCZOS)
            mask = Image.new("L", (size, size), 0)
            draw = ImageDraw.Draw(mask)
            draw.rounded_rectangle([0, 0, size-1, size-1], radius=max(10, size//12), fill=255)
            img.putalpha(mask)
            cimg = ctk.CTkImage(light_image=img, dark_image=img, size=(size, size))
            DEFAULT_COVER_CACHE[key] = cimg
            return cimg
    except:
        pass
    key2 = ("placeholder", size, cor)
    cached2 = DEFAULT_COVER_CACHE.get(key2)
    if cached2 is not None:
        return cached2
    imgp = _make_placeholder(size, cor)
    cimgp = ctk.CTkImage(light_image=imgp, dark_image=imgp, size=(size, size))
    DEFAULT_COVER_CACHE[key2] = cimgp
    return cimgp

def _get_round_mask(size):
    m = ROUND_MASK_CACHE.get(size)
    if m is not None:
        return m
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle([0, 0, size-1, size-1], radius=max(10, size//12), fill=255)
    ROUND_MASK_CACHE[size] = mask
    return mask

def _load_custom_cover(path_cache, size):
    key = (path_cache, size)
    cimg = CUSTOM_COVER_CACHE.get(key)
    if cimg is not None:
        return cimg
    try:
        img = Image.open(path_cache).convert("RGBA")
        img = img.resize((size, size), Image.Resampling.LANCZOS)
        mask = _get_round_mask(size)
        img.putalpha(mask)
        cimg = ctk.CTkImage(light_image=img, dark_image=img, size=(size, size))
        CUSTOM_COVER_CACHE[key] = cimg
        return cimg
    except:
        return None

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

def is_fullscreen_like():
    try:
        if app.attributes("-fullscreen"):
            return True
    except:
        pass
    try:
        sw, sh = app.winfo_screenwidth(), app.winfo_screenheight()
        ww, wh = app.winfo_width(), app.winfo_height()
        return ww >= sw - 50 and wh >= sh - 80
    except:
        return False

def _get_key_hint(idx):
    if idx < 9:
        return str(idx+1)
    if idx == 9:
        return "0"
    m = {10:"Q",11:"W",12:"E",13:"R",14:"T",15:"Y",16:"U",17:"I",18:"O",19:"P"}
    return m.get(idx, "")

def criar_botoes():
    # CTkScrollableFrame não aceita argumento em grid_propagate; dispensável aqui
    for i in range(20):
        try:
            panel.grid_rowconfigure(i, weight=1, uniform='row')
        except Exception:
            pass
    cols = max(5, min(8, int(config.get("grid_columns", 5))))
    for i in range(8):
        try:
            if i < cols:
                panel.grid_columnconfigure(i, weight=1, uniform='col')
            else:
                panel.grid_columnconfigure(i, weight=0, uniform='')
        except Exception:
            pass
    try:
        panel.update_idletasks()
    except Exception:
        pass
    try:
        aw = max(600, int(app.winfo_width() or 0) - 40)
        pw = int(panel.winfo_width() or 0)
        panel_w = max(aw, pw)
    except Exception:
        panel_w = 1000
    
    total = max(1, min(20, len(config.get("botoes", []))))
    rows = max(1, (total + cols - 1) // cols)
    for i in range(total):
        frame = ctk.CTkFrame(panel, fg_color="transparent")
        frame.grid(row=i//cols, column=i%cols, padx=5, pady=5, sticky="nsew")
        frame.grid_propagate(True)
        frame.grid_rowconfigure(0, weight=1)
        frame.grid_rowconfigure(1, weight=0)
        frame.grid_columnconfigure(0, weight=1)
        
        base_texto = config["botoes"][i]["nome"]
        texto_botao = quebrar_texto(base_texto)
        
        img_cache = config["botoes"][i].get("imagem_cache", "")
        base_dir = os.path.join(ICONS_DIR, current_playlist)
        use_img = bool(img_cache and os.path.exists(img_cache) and os.path.normpath(img_cache).startswith(os.path.normpath(base_dir)))
        size_opt = str(config.get("card_size", "medio")).lower()
        scale_min = float(config.get("card_scale_min", 0.8))
        scale_max = float(config.get("card_scale_max", 1.4))
        scale = float(config.get("card_scale", 1.0))
        if scale < scale_min: scale = scale_min
        if scale > scale_max: scale = scale_max
        try:
            gap = 10
            cell_w = int((panel_w - ((cols + 1) * gap)) / cols)
            cell_w = max(120, cell_w)
        except Exception:
            cell_w = 180
        base_fill = cell_w - 12
        if size_opt == "pequeno":
            target = base_fill * 0.60 * scale
        elif size_opt == "grande":
            target = base_fill * min(1.15, scale)
        else:
            target = base_fill * scale
        img_sz = int(max(90, min(base_fill, target)))
        cover_base = int(img_sz * 0.85)
        cover_sz = max(80 if size_opt == "pequeno" else 90, cover_base)
        font_base = 12 if size_opt == "pequeno" else (13 if size_opt == "medio" else 14)
        font_sz = max(10, min(18, int(font_base * scale)))
        extra_h = max(28, int(img_sz * 0.28))
        b = ctk.CTkButton(frame, 
                         text=texto_botao,
                         fg_color=config["botoes"][i]["cor"],
                         text_color=config["botoes"][i].get("texto_cor", "white"),
                         width=img_sz, 
                         height=cover_sz + extra_h, 
                         font=("Arial", font_sz, "bold"),
                         anchor="center",
                         corner_radius=8,
                         hover_color=config["botoes"][i]["cor"],
                         compound=("top" if use_img else None),
                         command=lambda i=i: tocar_som(i))
        b.grid(row=0, column=0, sticky="nsew", padx=2, pady=2)
        if use_img:
            try:
                cimg = _load_custom_cover(img_cache, cover_sz)
                if cimg is not None:
                    b.configure(image=cimg)
                    b.image = cimg
            except:
                pass
        else:
            try:
                cimgp = _load_default_cover(cover_sz, config["botoes"][i]["cor"]) 
                b.configure(image=cimgp, compound="top")
                b.image = cimgp
            except:
                pass
        button_refs.append(b)
        
        vf_h = max(22, int(img_sz * 0.11))
        volume_frame = ctk.CTkFrame(frame, fg_color=("#e5e7eb", "#1e1e1e"), height=vf_h)
        volume_frame.grid(row=1, column=0, sticky="ew", padx=2, pady=(0, 2))
        
        hint = _get_key_hint(i)
        if hint:
            key_label = ctk.CTkLabel(volume_frame, text=f"[{hint}]", width=24, font=("Arial", 10))
            key_label.pack(side="left", padx=(2,0))
            key_hint_labels.append(key_label)
        else:
            key_hint_labels.append(None)

        volume_label = ctk.CTkLabel(volume_frame, text=f"{int(config['botoes'][i].get('volume', 1.0)*100)}%", 
                                     width=35, font=("Arial", 10))
        volume_label.pack(side="left", padx=2)
        
        def criar_callback(idx, lbl):
            def callback(valor):
                lbl.configure(text=f"{int(valor*100)}%")
                atualizar_volume_individual(idx, valor)
            return callback
        
        slider_w = max(80, int(img_sz * 0.45))
        slider = ctk.CTkSlider(volume_frame, from_=0, to=1, width=slider_w, height=12,
                               command=criar_callback(i, volume_label))
        slider.set(config["botoes"][i].get("volume", 1.0))
        slider.pack(side="left", padx=2, expand=True, fill="x")
        volume_sliders.append((slider, volume_label))

        is_loop = bool(config["botoes"][i].get("loop", False))
        def _toggle_loop(idx=i):
            val = not bool(config["botoes"][idx].get("loop", False))
            config["botoes"][idx]["loop"] = val
            salvar_config()
            try:
                led = loop_led_labels[idx]
                if led:
                    led.configure(text_color=("#22c55e" if val else "#6b7280"))
            except Exception:
                pass
            if current_index == idx:
                atualizar_timer()
        loop_btn = ctk.CTkButton(volume_frame, text="⟲", width=26, height=18, fg_color="#374151", hover_color="#4b5563", command=_toggle_loop)
        loop_btn.pack(side="left", padx=2)
        led_label = ctk.CTkLabel(volume_frame, text="●", font=("Arial", 12), text_color=("#22c55e" if is_loop else "#6b7280"))
        led_label.pack(side="left", padx=2)
        loop_led_labels.append(led_label)

criar_botoes()
try:
    atualizar_estilos()
except Exception:
    pass

def recriar_botoes():
    try:
        for child in panel.winfo_children():
            try:
                child.destroy()
            except Exception:
                pass
    except Exception:
        pass
    button_refs.clear()
    volume_sliders.clear()
    key_hint_labels.clear()
    loop_led_labels.clear()
    criar_botoes()

def atualizar_dicas_atalhos():
    try:
        limit = min(len(key_hint_labels), len(button_refs))
        for i in range(limit):
            lbl = key_hint_labels[i]
            if lbl:
                lbl.configure(text=f"[{_get_key_hint(i)}]")
    except Exception:
        pass

def _ensure_icons_dir():
    try:
        base = os.path.join(ICONS_DIR, current_playlist)
        os.makedirs(base, exist_ok=True)
        return base
    except:
        return ICONS_DIR

def selecionar_imagem_botao(index, parent=None):
    _ensure_icons_dir()
    f = filedialog.askopenfilename(title=f"Selecionar imagem para {config['botoes'][index]['nome']}",
                                   filetypes=[("Imagens", "*.jpg *.jpeg *.png *.webp")])
    if not f:
        return
    try:
        if os.path.getsize(f) > 2 * 1024 * 1024:
            messagebox.showwarning("Imagem grande", "Limite de 2MB por imagem. Escolha outra.")
            return
    except:
        pass
    ok = processar_imagem_botao(index, f)
    if ok:
        salvar_config()
        recriar_botoes()
        # manter sem forçar foco

def remover_imagem_botao(index, parent=None):
    try:
        config["botoes"][index]["imagem"] = ""
        config["botoes"][index]["imagem_cache"] = ""
        salvar_config()
        recriar_botoes()
    except:
        pass
    # manter sem forçar foco

def processar_imagem_botao(index, path):
    try:
        img = Image.open(path).convert("RGB")
    except Exception as e:
        _show_error("Erro ao abrir imagem", e)
        return False
    w, h = img.size
    if min(w, h) < 140:
        messagebox.showwarning("Baixa resolução", "Imagem muito pequena, pode ficar borrada.")
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    img = img.crop((left, top, left + side, top + side))
    small = img.resize((126, 126), Image.Resampling.LANCZOS)
    bg = Image.new("RGB", (140, 140), "#ffffff")
    bg.paste(small, (7, 7))
    try:
        draw = ImageDraw.Draw(bg)
        nome = config["botoes"][index]["nome"]
        texto = nome
        cor = config["botoes"][index].get("texto_cor", "#ffffff")
        try:
            font = ImageFont.truetype("arial.ttf", 20)
        except:
            font = ImageFont.load_default()
        tw, th = draw.textsize(texto, font=font)
        x = (140 - tw) // 2
        y = 140 - th - 6
        shadow = (x+1, y+1)
        draw.text(shadow, texto, font=font, fill="#000000")
        draw.text((x, y), texto, font=font, fill=cor, stroke_width=2, stroke_fill="#000000")
    except:
        pass
    out_dir = _ensure_icons_dir()
    cache_name = f"btn{index+1}.jpg"
    out_path = os.path.join(out_dir, cache_name)
    try:
        bg.save(out_path, format="JPEG", quality=85, optimize=True)
    except Exception as e:
        _show_error("Erro ao salvar imagem", e)
        return False
    config["botoes"][index]["imagem"] = path
    config["botoes"][index]["imagem_cache"] = out_path
    return True

_last_fullscreen_like = None
_resize_rebuild_flag = False

def is_fullscreen_like():
    try:
        if app.attributes("-fullscreen"):
            return True
    except:
        pass
    try:
        sw, sh = app.winfo_screenwidth(), app.winfo_screenheight()
        ww, wh = app.winfo_width(), app.winfo_height()
        return ww >= sw - 50 and wh >= sh - 80
    except:
        return False

def _do_rebuild_buttons():
    global _resize_rebuild_flag
    _resize_rebuild_flag = False
    recriar_botoes()

def _on_app_resize(event=None):
    global _last_fullscreen_like, _resize_rebuild_flag
    cur = is_fullscreen_like()
    if cur != _last_fullscreen_like:
        _last_fullscreen_like = cur
        if not _resize_rebuild_flag:
            _resize_rebuild_flag = True
            try:
                app.after(300, _do_rebuild_buttons)
            except:
                pass

try:
    app.bind("<Configure>", _on_app_resize)
except:
    pass

 

def exportar_backup():
    try:
        parar_tudo()
    except Exception:
        pass
    backup_name = f"Som_de_fundo_backup_{time.strftime('%Y%m%d')}.zip"
    path = filedialog.asksaveasfilename(defaultextension=".zip", initialfile=backup_name,
                                        filetypes=[("Zip", "*.zip")], title="Salvar backup")
    if not path:
        return
    try:
        nomes = []
        try:
            for file in os.listdir(PLAYLISTS_DIR):
                if file.endswith(".json"):
                    nomes.append(file[:-5])
        except Exception:
            pass
        if not nomes:
            messagebox.showwarning("Backup", "Nenhuma playlist encontrada para exportar.")
            return
        size_map = {}
        try:
            for pl in nomes:
                total = 0
                pjson = os.path.join(PLAYLISTS_DIR, f"{pl}.json")
                if os.path.exists(pjson):
                    try:
                        total += os.path.getsize(pjson)
                    except Exception:
                        pass
                icdir = os.path.join(ICONS_DIR, pl)
                if os.path.isdir(icdir):
                    for root, _, files in os.walk(icdir):
                        for fn in files:
                            fp = os.path.join(root, fn)
                            try:
                                total += os.path.getsize(fp)
                            except Exception:
                                pass
                sdir = os.path.join(SONS_DIR, pl)
                if os.path.isdir(sdir):
                    for root, _, files in os.walk(sdir):
                        for fn in files:
                            fp = os.path.join(root, fn)
                            try:
                                total += os.path.getsize(fp)
                            except Exception:
                                pass
                try:
                    with open(pjson, "r", encoding="utf-8") as f:
                        pdata = json.load(f)
                    for b in pdata.get("botoes", []):
                        src = b.get("arquivo") or ""
                        if src:
                            try:
                                resolved = resolve_audio_path(src)
                            except Exception:
                                resolved = src
                            if resolved and os.path.exists(resolved):
                                try:
                                    norm_sons = os.path.normpath(SONS_DIR)
                                    norm_res = os.path.normpath(resolved)
                                    if not norm_res.startswith(norm_sons):
                                        total += os.path.getsize(resolved)
                                    
                                except Exception:
                                    pass
                        imgp = b.get("imagem") or ""
                        if imgp and os.path.exists(imgp):
                            try:
                                if not os.path.normpath(imgp).startswith(os.path.normpath(ICONS_DIR)):
                                    total += os.path.getsize(imgp)
                            except Exception:
                                pass
                except Exception:
                    pass
                size_map[pl] = total
        except Exception:
            pass
        def _fmt_bytes(n):
            try:
                units = ["B","KB","MB","GB","TB"]
                i = 0
                f = float(n)
                while f >= 1024 and i < len(units)-1:
                    f /= 1024
                    i += 1
                return f"{f:.1f} {units[i]}"
            except Exception:
                return str(n)
        sel_win = ctk.CTkToplevel(app)
        sel_win.title("Selecionar Playlists para Backup")
        sel_win.geometry("420x500")
        sel_win.resizable(False, False)
        try:
            sel_win.transient(app); sel_win.grab_set(); sel_win.lift()
        except Exception:
            pass
        ctk.CTkLabel(sel_win, text="Escolha as playlists:", font=("Arial", 14, "bold")).pack(pady=(10,6))
        sf = ctk.CTkScrollableFrame(sel_win, width=380, height=360)
        sf.pack(padx=10, pady=(4,8), fill="both", expand=True)
        vars_map = {}
        size_label = ctk.CTkLabel(sel_win, text="Tamanho estimado: 0 MB", font=("Arial", 12))
        for pl in nomes:
            v = ctk.BooleanVar(value=True)
            vars_map[pl] = v
            def _on_toggle(pl_name=pl):
                update_estimate()
            cb = ctk.CTkCheckBox(sf, text=pl, variable=v, command=_on_toggle)
            cb.pack(anchor="w", padx=8, pady=4)
        row = ctk.CTkFrame(sel_win, fg_color="transparent")
        row.pack(pady=6)
        size_label.pack(pady=(0,6))
        def _confirm():
            selected = [pl for pl,v in vars_map.items() if v.get()]
            if not selected:
                messagebox.showwarning("Backup", "Selecione ao menos uma playlist.")
                return
            try:
                sel_win.grab_release()
            except Exception:
                pass
            try:
                if sel_win and sel_win.winfo_exists():
                    sel_win.destroy()
            except Exception:
                pass
            _export_selected(selected)
        def _all():
            for v in vars_map.values():
                v.set(True)
            update_estimate()
        def update_estimate():
            try:
                total = 0
                for pl, v in vars_map.items():
                    if v.get():
                        total += int(size_map.get(pl, 0))
                size_label.configure(text=f"Tamanho estimado: {_fmt_bytes(total)}")
            except Exception:
                pass
        ctk.CTkButton(row, text="Selecionar tudo", command=_all).pack(side="left", padx=6)
        ctk.CTkButton(row, text="Exportar", fg_color="#2563eb", hover_color="#1d4ed8", command=_confirm).pack(side="left", padx=6)
        try:
            update_estimate()
        except Exception:
            pass

        def _export_selected(selected_playlists):
            files_to_copy = []
            for pl_name in selected_playlists:
                file = f"{pl_name}.json"
                try:
                    with open(os.path.join(PLAYLISTS_DIR, file), "r", encoding="utf-8") as f:
                        pdata = json.load(f)
                    for b in pdata.get("botoes", []):
                        src = b.get("arquivo") or ""
                        if src:
                            try:
                                resolved = resolve_audio_path(src)
                            except Exception:
                                resolved = src
                            try:
                                if resolved and os.path.exists(resolved):
                                    norm_sons = os.path.normpath(SONS_DIR)
                                    norm_res = os.path.normpath(resolved)
                                    if not norm_res.startswith(norm_sons):
                                        dest = os.path.join(SONS_DIR, pl_name, os.path.basename(resolved))
                                        files_to_copy.append((resolved, dest))
                            except Exception:
                                pass
                except Exception:
                    pass

            win = ctk.CTkToplevel(app)
            win.title("Exportar Backup")
            win.geometry("520x160")
            win.resizable(False, False)
            try:
                win.transient(app); win.grab_set(); win.lift()
            except Exception:
                pass
                ctk.CTkLabel(win, text="Copiando músicas para AppData", font=("Arial", 14, "bold")).pack(pady=(12, 6))
                status_label = ctk.CTkLabel(win, text="Preparando...", font=("Arial", 12)); status_label.pack(pady=4)
                pb = ctk.CTkProgressBar(win, width=460); pb.pack(pady=(6, 10)); pb.set(0.0)

            def _copy_and_zip():
                try:
                        # cálculo de bytes totais removido de avisos de limite
                    total = len(files_to_copy); done = 0
                    for src, dest in files_to_copy:
                        try:
                            os.makedirs(os.path.dirname(dest), exist_ok=True)
                            app.after(0, lambda s=os.path.basename(src), d=done, t=total: (
                                status_label.configure(text=f"Copiando {d+1}/{t}: {s}"),
                                pb.set((d / t) if t > 0 else 0.0)
                            ))
                            if not os.path.exists(dest):
                                shutil.copy2(src, dest)
                            else:
                                try:
                                    os.replace(src, dest)
                                except Exception:
                                    shutil.copy2(src, dest)
                        except Exception:
                            pass
                        finally:
                            done += 1
                    app.after(0, lambda: (status_label.configure(text="Compactando backup..."), pb.set(1.0)))

                    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as z:
                        for pl_name in selected_playlists:
                            pjson = os.path.join(PLAYLISTS_DIR, f"{pl_name}.json")
                            if os.path.exists(pjson):
                                z.write(pjson, os.path.join("playlists", f"{pl_name}.json"))
                            icdir = os.path.join(ICONS_DIR, pl_name)
                            if os.path.isdir(icdir):
                                for root, _, files in os.walk(icdir):
                                    for fn in files:
                                        fp = os.path.join(root, fn)
                                        arc = os.path.join("icons", pl_name, os.path.relpath(fp, icdir))
                                        z.write(fp, arc)
                            sdir = os.path.join(SONS_DIR, pl_name)
                            if os.path.isdir(sdir):
                                for root, _, files in os.walk(sdir):
                                    for fn in files:
                                        fp = os.path.join(root, fn)
                                        arc = os.path.join("sons", pl_name, os.path.relpath(fp, sdir))
                                        z.write(fp, arc)
                            try:
                                with open(os.path.join(PLAYLISTS_DIR, f"{pl_name}.json"), "r", encoding="utf-8") as f:
                                    pdata = json.load(f)
                                for b in pdata.get("botoes", []):
                                    imgp = b.get("imagem") or ""
                                    if imgp and os.path.exists(imgp) and not os.path.normpath(imgp).startswith(os.path.normpath(ICONS_DIR)):
                                        z.write(imgp, os.path.join("images", pl_name, os.path.basename(imgp)))
                            except Exception:
                                pass
                    app.after(0, win.destroy)
                    app.after(50, lambda: _show_info_auto("Backup", "Backup exportado com sucesso!"))
                except Exception as e:
                    app.after(0, win.destroy)
                    app.after(50, lambda: _show_error("Erro ao exportar backup", e))

            threading.Thread(target=_copy_and_zip, daemon=True).start()
        # fim _export_selected
    except Exception as e:
        _show_error("Erro ao exportar backup", e)

def importar_backup():
    try:
        parar_tudo()
        try:
            pygame.mixer.music.stop()
        except Exception:
            pass
    except Exception:
        pass
    resposta = messagebox.askyesno("Importar Backup", "Para evitar arquivos bloqueados, feche o app antes de importar.\n\nDeseja continuar?")
    if not resposta:
        return
    path = filedialog.askopenfilename(filetypes=[("Zip", "*.zip")], title="Selecionar backup")
    if not path:
        return
    tmpdir = None
    try:
        tmpdir = tempfile.mkdtemp(prefix="Som_de_fundo_import_")
        with zipfile.ZipFile(path, "r") as z:
            z.extractall(tmpdir)
        src_pl_dir = os.path.join(tmpdir, "playlists")
        pl_names = []
        try:
            if os.path.isdir(src_pl_dir):
                for fn in os.listdir(src_pl_dir):
                    if fn.endswith(".json"):
                        pl_names.append(fn[:-5])
        except Exception:
            pass
        if not pl_names:
            # fallback: copiar tudo
            pl_names = []
        else:
            win = ctk.CTkToplevel(app)
            win.title("Selecionar Playlists para Importar")
            win.geometry("420x500")
            win.resizable(False, False)
            try:
                win.transient(app); win.grab_set(); win.lift()
            except Exception:
                pass
            def _safe_close_import():
                try:
                    win.grab_release()
                except Exception:
                    pass
                try:
                    if win and win.winfo_exists():
                        win.destroy()
                except Exception:
                    pass
            try:
                win.protocol("WM_DELETE_WINDOW", _safe_close_import)
            except Exception:
                pass
            ctk.CTkLabel(win, text="Escolha as playlists:", font=("Arial", 14, "bold")).pack(pady=(10,6))
            sf = ctk.CTkScrollableFrame(win, width=380, height=360)
            sf.pack(padx=10, pady=(4,8), fill="both", expand=True)
            vars_map = {}
            for pl in pl_names:
                v = ctk.BooleanVar(value=True)
                vars_map[pl] = v
                cb = ctk.CTkCheckBox(sf, text=pl, variable=v)
                cb.pack(anchor="w", padx=8, pady=4)
            row = ctk.CTkFrame(win, fg_color="transparent"); row.pack(pady=6)
            selected_target = {"names": None, "confirmed": False, "canceled": False}
            def _confirm():
                selected = [pl for pl,v in vars_map.items() if v.get()]
                if not selected:
                    messagebox.showwarning("Importar", "Selecione ao menos uma playlist.")
                    return
                selected_target["names"] = selected
                selected_target["confirmed"] = True
                try:
                    win.grab_release()
                except Exception:
                    pass
                try:
                    if win and win.winfo_exists():
                        win.destroy()
                except Exception:
                    pass
            def _cancel():
                selected_target["canceled"] = True
                selected_target["confirmed"] = False
                try:
                    win.grab_release()
                except Exception:
                    pass
                try:
                    if win and win.winfo_exists():
                        win.destroy()
                except Exception:
                    pass
            def _all():
                for v in vars_map.values(): v.set(True)
            ctk.CTkButton(row, text="Selecionar tudo", command=_all).pack(side="left", padx=6)
            ctk.CTkButton(row, text="Importar", fg_color="#2563eb", hover_color="#1d4ed8", command=_confirm).pack(side="left", padx=6)
            ctk.CTkButton(row, text="Fechar", command=_cancel).pack(side="left", padx=6)
            try:
                win.protocol("WM_DELETE_WINDOW", _cancel)
            except Exception:
                pass
            win.wait_window()

        selected = selected_target.get("names") if 'selected_target' in locals() else None
        confirmed = selected_target.get("confirmed") if 'selected_target' in locals() else False
        if not confirmed:
            return
        if selected is None or len(selected) == 0:
            # copiar tudo
            for root, _, files in os.walk(tmpdir):
                for fn in files:
                    src = os.path.join(root, fn)
                    rel = os.path.relpath(src, tmpdir)
                    dest = os.path.join(USER_DATA_DIR, rel)
                    try:
                        os.makedirs(os.path.dirname(dest), exist_ok=True)
                        if not os.path.exists(dest):
                            shutil.copy2(src, dest)
                        else:
                            try:
                                os.replace(src, dest)
                            except Exception:
                                pass
                    except PermissionError:
                        pass
                    except Exception:
                        pass
        else:
            for pl in selected:
                try:
                    sp = os.path.join(tmpdir, "playlists", f"{pl}.json")
                    if os.path.exists(sp):
                        dp = os.path.join(PLAYLISTS_DIR, f"{pl}.json")
                        os.makedirs(os.path.dirname(dp), exist_ok=True)
                        shutil.copy2(sp, dp)
                    # icons
                    si = os.path.join(tmpdir, "icons", pl)
                    di = os.path.join(ICONS_DIR, pl)
                    if os.path.isdir(si):
                        os.makedirs(di, exist_ok=True)
                        for root, _, files in os.walk(si):
                            for fn in files:
                                s = os.path.join(root, fn)
                                rel = os.path.relpath(s, si)
                                d = os.path.join(di, rel)
                                os.makedirs(os.path.dirname(d), exist_ok=True)
                                shutil.copy2(s, d)
                    # sons
                    ss = os.path.join(tmpdir, "sons", pl)
                    ds = os.path.join(SONS_DIR, pl)
                    if os.path.isdir(ss):
                        os.makedirs(ds, exist_ok=True)
                        for root, _, files in os.walk(ss):
                            for fn in files:
                                s = os.path.join(root, fn)
                                rel = os.path.relpath(s, ss)
                                d = os.path.join(ds, rel)
                                os.makedirs(os.path.dirname(d), exist_ok=True)
                                shutil.copy2(s, d)
                    # images externas
                    simg = os.path.join(tmpdir, "images", pl)
                    dimg = os.path.join(USER_DATA_DIR, "images", pl)
                    if os.path.isdir(simg):
                        os.makedirs(dimg, exist_ok=True)
                        for root, _, files in os.walk(simg):
                            for fn in files:
                                s = os.path.join(root, fn)
                                rel = os.path.relpath(s, simg)
                                d = os.path.join(dimg, rel)
                                os.makedirs(os.path.dirname(d), exist_ok=True)
                                shutil.copy2(s, d)
                except Exception:
                    pass
        # atualizar caminhos de imagem nas playlists importadas
        for file in os.listdir(PLAYLISTS_DIR):
            if file.endswith(".json"):
                try:
                    pl_name = file.replace(".json", "")
                    ppath = os.path.join(PLAYLISTS_DIR, file)
                    with open(ppath, "r", encoding="utf-8") as f:
                        pdata = json.load(f)
                    changed = False
                    for b in pdata.get("botoes", []):
                        base = os.path.basename(b.get("imagem", "") or "")
                        if base:
                            new_img = os.path.join(USER_DATA_DIR, "images", pl_name, base)
                            if os.path.exists(new_img):
                                b["imagem"] = new_img
                                b["imagem_cache"] = ""
                                changed = True
                    if changed:
                        with open(ppath, "w", encoding="utf-8") as f:
                            json.dump(pdata, f, indent=4, ensure_ascii=False)
                except Exception:
                    pass
        carregar_config()
        try:
            recriar_botoes()
        except Exception:
            atualizar_estilos()
        _show_info_auto("Backup", "Backup importado com sucesso! Reinicie o aplicativo para aplicar totalmente.")
    except Exception as e:
        _show_error("Erro ao importar backup", e)
    finally:
        try:
            if tmpdir and os.path.isdir(tmpdir):
                shutil.rmtree(tmpdir, ignore_errors=True)
        except Exception:
            pass

def abrir_pasta_dados():
    try:
        if os.name == 'nt':
            os.startfile(USER_DATA_DIR)
        else:
            webbrowser.open(f"file://{os.path.abspath(USER_DATA_DIR)}")
    except Exception as e:
        _show_error("Erro ao abrir pasta de dados", e)

def abrir_config_janela():
    win = ctk.CTkToplevel(app)
    win.title("Configurações dos Botões")
    win.geometry("750x600")
    win.resizable(False, False)
    
    win.transient(app)
    win.grab_set()
    win.lift()
    try:
        win.attributes("-topmost", True)
        win.after(300, lambda: win.attributes("-topmost", False))
    except:
        pass
    
    win.update_idletasks()
    x = (win.winfo_screenwidth() // 2) - (750 // 2)
    y = (win.winfo_screenheight() // 2) - (600 // 2)
    win.geometry(f"750x600+{x}+{y}")
    
    def resetar_config():
        resposta = messagebox.askyesno(
            "Resetar Configurações",
            "Tem certeza que deseja resetar todas as configurações para o padrão?\n\nIsso irá remover todos os nomes, cores e arquivos de áudio configurados."
        )
        if resposta:
            global config, master_volume
            try:
                parar_tudo()
            except Exception:
                pass
            config = default_config()
            try:
                atualizar_volume_master(1.0)
            except Exception:
                master_volume = 1.0
                config["master_volume"] = 1.0
            # limpar cache de imagens da playlist atual
            try:
                base = os.path.join(ICONS_DIR, current_playlist)
                if os.path.isdir(base):
                    for nome in os.listdir(base):
                        p = os.path.join(base, nome)
                        try:
                            if os.path.isfile(p):
                                os.remove(p)
                        except Exception:
                            pass
            except Exception:
                pass
            salvar_config()
            try:
                recriar_botoes()
            except Exception:
                atualizar_estilos()
            for i, (slider, label) in enumerate(volume_sliders):
                try:
                    slider.set(1.0)
                    label.configure(text="100%")
                except Exception:
                    pass
            try:
                volume_master_slider.set(1.0)
                volume_master_label.configure(text="100%")
            except Exception:
                pass
            _show_info_auto("✅ Resetado", "Configurações resetadas com sucesso!")
            try:
                win.grab_release()
            except Exception:
                pass
            try:
                if win and win.winfo_exists():
                    win.destroy()
            except Exception:
                pass
    
    header_config = ctk.CTkFrame(win, fg_color="transparent", height=40)
    header_config.pack(fill="x", padx=10, pady=(10, 5))
    
    ctk.CTkLabel(header_config, text="⚙️ Configurações", font=("Arial", 18, "bold")).pack(side="left")
    
    ctk.CTkButton(header_config, text="🔄 Resetar Tudo", fg_color="#dc2626", hover_color="#b91c1c",
                  width=120, height=30, font=("Arial", 11, "bold"),
                  command=resetar_config).pack(side="right")


    canvas = ctk.CTkScrollableFrame(win, width=720, height=420)
    canvas.pack(padx=10, pady=(5, 5), fill="both", expand=True)

    entries = []
    
    atalhos_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color=("#f3f4f6", "#1e293b"))
    atalhos_frame.pack(pady=8, padx=10, fill="x")
    
    ctk.CTkLabel(atalhos_frame, text="⌨️ Atalhos de Teclado", font=("Arial", 16, "bold")).pack(anchor="w", pady=4, padx=8)
    
    atalhos_var = ctk.BooleanVar(value=config.get("atalhos_habilitados", True))
    atalhos_checkbox = ctk.CTkCheckBox(atalhos_frame, 
                                       text="Habilitar atalhos de teclado (Teclas 0-9)",
                                       variable=atalhos_var,
                                       font=("Arial", 12))
    atalhos_checkbox.pack(anchor="w", padx=10, pady=8)
    tema_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color=("#f3f4f6", "#1e293b"))
    tema_frame.pack(pady=8, padx=10, fill="x")
    ctk.CTkLabel(tema_frame, text="🎨 Tema", font=("Arial", 16, "bold")).pack(anchor="w", pady=4, padx=8)
    row_tema = ctk.CTkFrame(tema_frame, fg_color="transparent")
    row_tema.pack(fill="x", padx=10, pady=6)
    ctk.CTkLabel(row_tema, text="Aparência:", width=120).pack(side="left")
    appearance_combo = ctk.CTkComboBox(row_tema, values=["light", "dark"], width=140)
    appearance_combo.set(app_prefs.get("appearance_mode", "dark"))
    appearance_combo.pack(side="left", padx=(0, 20))
    
    fade_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color=("#f3f4f6", "#1e293b"))
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
    ctk.CTkLabel(fades_row, text="Crossfade (ms):", width=120).pack(side="left", padx=(20,0))
    crossfade_entry = ctk.CTkEntry(fades_row, width=100)
    crossfade_entry.insert(0, str(config.get("crossfade_ms", 400)))
    crossfade_entry.pack(side="left")
    ctk.CTkLabel(fades_row, text="Seek Fade (ms):", width=120).pack(side="left", padx=(20,0))
    seekfade_entry = ctk.CTkEntry(fades_row, width=100)
    seekfade_entry.insert(0, str(config.get("seek_fade_ms", 200)))
    seekfade_entry.pack(side="left")

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

    backup_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color=("#f3f4f6", "#1e293b"))
    backup_frame.pack(pady=8, padx=10, fill="x")
    ctk.CTkLabel(backup_frame, text="💾 Backup", font=("Arial", 16, "bold")).pack(anchor="w", pady=4, padx=8)
    row_backup = ctk.CTkFrame(backup_frame, fg_color="transparent")
    row_backup.pack(fill="x", padx=10, pady=6)
    ctk.CTkButton(row_backup, text="Exportar Backup", fg_color="#2563eb", hover_color="#1d4ed8",
                  command=exportar_backup).pack(side="left", padx=6)
    ctk.CTkButton(row_backup, text="Importar Backup", fg_color="#16a34a", hover_color="#15803d",
                  command=importar_backup).pack(side="left", padx=6)
    ctk.CTkButton(row_backup, text="Abrir Pasta de Dados", fg_color="#374151", hover_color="#1f2937",
                  command=abrir_pasta_dados).pack(side="left", padx=6)

    ctk.CTkLabel(canvas, text="🎚️ Configuração dos Botões", font=("Arial", 16, "bold")).pack(anchor="w", pady=(15, 5), padx=10)
    # Cores globais
    cores_globais_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color=("#f3f4f6", "#1e293b"))
    cores_globais_frame.pack(pady=8, padx=10, fill="x")
    ctk.CTkLabel(cores_globais_frame, text="🎨 Cores Globais", font=("Arial", 16, "bold")).pack(anchor="w", pady=4, padx=8)
    qtd_frame = ctk.CTkFrame(canvas, corner_radius=12, fg_color=("#f3f4f6", "#1e293b"))
    qtd_frame.pack(pady=8, padx=10, fill="x")
    ctk.CTkLabel(qtd_frame, text="🔢 Quantidade de botões (máx. 20)", font=("Arial", 14, "bold")).pack(anchor="w", pady=4, padx=8)
    row_qtd = ctk.CTkFrame(qtd_frame, fg_color="transparent")
    row_qtd.pack(fill="x", padx=10, pady=6)
    ctk.CTkLabel(row_qtd, text="Total:", width=120).pack(side="left")
    qtd_combo = ctk.CTkComboBox(row_qtd, values=[str(i) for i in range(1, 21)], width=100)
    qtd_combo.set(str(len(config.get("botoes", []))))
    
    def _alterar_qtd_botoes(value):
        try:
            alvo = int(value)
        except Exception:
            return
        alvo = max(1, min(20, alvo))
        atual = len(config.get("botoes", []))
        if alvo == atual:
            return
        paleta_cores = [
            '#3b82f6', '#8b5cf6', '#06b6d4', '#10b981', '#ef4444',
            '#f59e0b', '#ec4899', '#14b8a6', '#f97316', '#6366f1'
        ]
        if alvo > atual:
            for i in range(atual, alvo):
                cor = paleta_cores[i % len(paleta_cores)]
                config["botoes"].append({
                    "nome": f"Botão {i+1}",
                    "cor": cor,
                    "arquivo": "",
                    "icone": "",
                    "volume": 1.0,
                    "imagem": "",
                    "imagem_cache": "",
                    "texto_cor": "#ffffff",
                    "loop": False
                })
        else:
            # Remover do final até alcançar alvo
            for _ in range(atual - alvo):
                try:
                    config["botoes"].pop()
                except Exception:
                    break
        salvar_config()
        try:
            recriar_botoes()
        except Exception:
            atualizar_estilos()
        try:
            win.destroy()
        except Exception:
            pass
        abrir_config_janela()
    qtd_combo.configure(command=_alterar_qtd_botoes)
    qtd_combo.pack(side="left")
    size_row = ctk.CTkFrame(qtd_frame, fg_color="transparent")
    size_row.pack(fill="x", padx=10, pady=6)
    ctk.CTkLabel(size_row, text="Tamanho dos cards:", width=160).pack(side="left")
    def _on_size_change(value):
        try:
            config["card_size"] = value
            app_prefs["card_size"] = value
            salvar_prefs()
            recriar_botoes()
            _show_info_auto("Configurações", "✅ Tamanho dos cards atualizado!")
        except Exception:
            pass
    size_combo = ctk.CTkComboBox(size_row, values=["pequeno","medio","grande"], width=140, command=_on_size_change)
    try:
        size_combo.set(str(config.get("card_size", "medio")))
    except Exception:
        size_combo.set("medio")
    size_combo.pack(side="left", padx=6)
    cols_row = ctk.CTkFrame(qtd_frame, fg_color="transparent")
    cols_row.pack(fill="x", padx=10, pady=6)
    ctk.CTkLabel(cols_row, text="Colunas da grade:", width=160).pack(side="left")
    def _on_cols_change(value):
        try:
            config["grid_columns"] = int(value)
            app_prefs["grid_columns"] = int(value)
            salvar_prefs()
            recriar_botoes()
            _show_info_auto("Configurações", "✅ Colunas da grade atualizadas!")
        except Exception:
            pass
    cols_combo = ctk.CTkComboBox(cols_row, values=["5","6","7","8"], width=140, command=_on_cols_change)
    try:
        cols_combo.set(str(config.get("grid_columns", 5)))
    except Exception:
        cols_combo.set("5")
    cols_combo.pack(side="left", padx=6)
    slider_row = ctk.CTkFrame(qtd_frame, fg_color="transparent")
    slider_row.pack(fill="x", padx=10, pady=6)
    ctk.CTkLabel(slider_row, text="Ajuste fino:", width=160).pack(side="left")
    def _on_size_slider(val):
        try:
            config["card_scale"] = float(val)
            app_prefs["card_scale"] = float(val)
            salvar_prefs()
            recriar_botoes()
        except Exception:
            pass
    size_slider = ctk.CTkSlider(slider_row, from_=float(config.get("card_scale_min",0.8)), to=float(config.get("card_scale_max",1.4)), number_of_steps=20, width=180, command=_on_size_slider)
    try:
        size_slider.set(float(config.get("card_scale",1.0)))
    except Exception:
        size_slider.set(1.0)
    size_slider.pack(side="left", padx=6)
    def _sync_slider_by_combo(value):
        target = 1.0
        if value == "pequeno":
            target = 0.85
        elif value == "grande":
            target = 1.2
        try:
            mn = float(config.get("card_scale_min",0.8)); mx = float(config.get("card_scale_max",1.4))
            target = max(mn, min(mx, target))
            size_slider.set(target)
            config["card_scale"] = target
            app_prefs["card_scale"] = target
            salvar_prefs()
            recriar_botoes()
        except Exception:
            pass
    try:
        size_combo.configure(command=lambda v: (_on_size_change(v), _sync_slider_by_combo(v)))
    except Exception:
        pass
    def aplicar_cor_todos():
        c = colorchooser.askcolor(title="Escolher cor para todos os botões")[1]
        if c:
            for i in range(len(config.get("botoes", []))):
                config["botoes"][i]["cor"] = c
            salvar_config()
            try:
                recriar_botoes()
            except Exception:
                atualizar_estilos()
    ctk.CTkButton(cores_globais_frame, text="Aplicar cor em todos", width=180, command=aplicar_cor_todos).pack(anchor="w", padx=10, pady=6)

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

        # emojis removidos

        ctk.CTkLabel(frame, text="Imagem", font=("Arial", 12, "bold")).pack(anchor="w", padx=10)
        cor_frame = ctk.CTkFrame(frame, fg_color=b["cor"], width=30, height=30, corner_radius=6)
        cor_label = ctk.CTkLabel(frame, text=b["cor"]) 
        row_img = ctk.CTkFrame(frame, fg_color="transparent")
        row_img.pack(padx=10, pady=5, fill="x")
        cor_frame.pack(in_=row_img, side="left")
        cor_label.pack(in_=row_img, side="left", padx=5)

        def escolher_cor_local(cor_frame=cor_frame, cor_label=cor_label, i=i):
            c = colorchooser.askcolor()[1]
            if c:
                config["botoes"][i]["cor"] = c
                cor_frame.configure(fg_color=c)
                cor_label.configure(text=c)
        ctk.CTkButton(row_img, text="🎨 Cor do Fundo", width=130, command=escolher_cor_local).pack(side="left", padx=8)
        def inserir_img_local(i=i):
            selecionar_imagem_botao(i, win)
        def remover_img_local(i=i):
            remover_imagem_botao(i, win)
        ctk.CTkButton(row_img, text="🖼️ Inserir Imagem", width=150, command=inserir_img_local).pack(side="left", padx=8)
        ctk.CTkButton(row_img, text="🗑️ Remover Imagem", width=150, fg_color="#ef4444", hover_color="#dc2626", command=remover_img_local).pack(side="left", padx=8)
        prev = ctk.CTkLabel(row_img, text="(prévia)")
        prev.pack(side="left", padx=8)
        img_cache = b.get("imagem_cache", "")
        if img_cache and os.path.exists(img_cache):
            try:
                imgp = Image.open(img_cache).resize((80,80), Image.Resampling.LANCZOS)
                prev_img = ctk.CTkImage(light_image=imgp, dark_image=imgp, size=(80,80))
                prev.configure(image=prev_img, text="")
                prev.image = prev_img
            except:
                pass
        def escolher_cor_texto(i=i):
            c = colorchooser.askcolor(title="Cor do texto do botão")[1]
            if c:
                config["botoes"][i]["texto_cor"] = c
        row_text = ctk.CTkFrame(frame, fg_color="transparent")
        row_text.pack(padx=10, pady=(2, 6), fill="x")
        ctk.CTkLabel(row_text, text="Cor do texto", width=120).pack(side="left")
        ctk.CTkButton(row_text, text="🅣 Cor do Texto", width=140, command=escolher_cor_texto).pack(side="left", padx=8)
        def escolher_som_local(i=i):
            f = filedialog.askopenfilename(title="Selecionar som", filetypes=[("Áudio", "*.mp3 *.wav *.ogg")])
            if f:
                if validar_arquivo_audio(f):
                    config["botoes"][i]["arquivo"] = f
                    try:
                        config["botoes"][i]["duracao"] = obter_duracao_musica(f)
                    except:
                        pass
                    salvar_config()
                    _show_info_auto("Som", f"Som selecionado para {config['botoes'][i]['nome']}")
        ctk.CTkLabel(frame, text="Áudio", font=("Arial", 12, "bold")).pack(anchor="w", padx=10)
        row_audio = ctk.CTkFrame(frame, fg_color="transparent")
        row_audio.pack(padx=10, pady=6, fill="x")
        ctk.CTkButton(row_audio, text="🎵 Escolher Som", width=150, command=escolher_som_local).pack(side="left", padx=0)
        arq = b.get("arquivo", "")
        ctk.CTkLabel(row_audio, text=(os.path.basename(arq) if arq else ""), font=("Arial", 11)).pack(side="left", padx=8)

        entries.append((i, nome))

    def salvar_tudo():
        for i, entry in entries:
            texto = entry.get()
            num_chars = len(texto)
            if num_chars > 30:
                messagebox.showerror("Erro", f"O botão '{config['botoes'][i]['nome']}' excede o limite de 30 caracteres ({num_chars} caracteres).\nPor favor, reduza o texto.")
                return
            config["botoes"][i]["nome"] = texto
        
        try:
            fi = int(fade_in_entry.get())
            fo = int(fade_out_entry.get())
            cf = int(crossfade_entry.get())
            sf = int(seekfade_entry.get())
            if fi < 0 or fo < 0:
                raise ValueError
            config["fade_in_ms"] = fi
            config["fade_out_ms"] = fo
            config["crossfade_ms"] = max(0, cf)
            config["seek_fade_ms"] = max(0, sf)
        except Exception:
            messagebox.showerror("Erro", "Os valores de Fade In/Out/Crossfade/Seek Fade devem ser números inteiros não negativos (em milissegundos).")
            return
        
        config["atalhos_habilitados"] = atalhos_var.get()
        # Salvar opção de repetição
        config["repeticao_habilitada"] = repeticao_var.get()
        app_prefs["appearance_mode"] = appearance_combo.get()
        salvar_prefs()
        aplicar_tema_prefs()
        
        salvar_config()
        atualizar_estilos()
        recriar_botoes()
        _show_info_auto("Configurações", "✅ Alterações salvas com sucesso!")
        try:
            win.grab_release()
        except Exception:
            pass
        try:
            if win and win.winfo_exists():
                win.destroy()
        except Exception:
            pass

    rodape = ctk.CTkFrame(win, fg_color=("#e5e7eb", "#2b2b2b"), height=60)
    rodape.pack(fill="x", side="bottom", pady=0)
    rodape.pack_propagate(False)
    
    btn_frame = ctk.CTkFrame(rodape, fg_color="transparent")
    btn_frame.pack(expand=True)
    
    ctk.CTkButton(btn_frame, text="💾 Salvar", fg_color="#16a34a", hover_color="#15803d",
                  width=120, height=35, font=("Arial", 13, "bold"),
                  command=salvar_tudo).pack(side="left", padx=5)
    def _cancel_close():
        try:
            win.grab_release()
        except Exception:
            pass
        try:
            if win and win.winfo_exists():
                win.destroy()
        except Exception:
            pass
    ctk.CTkButton(btn_frame, text="❌ Cancelar", fg_color="#6b7280", hover_color="#4b5563",
                  width=120, height=35, font=("Arial", 13, "bold"),
                  command=_cancel_close).pack(side="left", padx=5)

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

def abrir_controle_remoto():
    abrir_controle_remoto_info()

btn_remote = ctk.CTkButton(buttons_frame, text="🌐 Controle Remoto", fg_color="#10b981", hover_color="#059669",
                           command=abrir_controle_remoto)
btn_remote.pack(side="left", padx=5)

def abrir_pasta_sons():
    try:
        if os.name == 'nt':
            os.startfile(SONS_DIR)
        else:
            webbrowser.open(f"file://{os.path.abspath(SONS_DIR)}")
    except Exception as e:
        _show_error("Erro ao abrir pasta", e)

ctk.CTkButton(buttons_frame, text="Abrir Pasta Sons", fg_color="#374151", hover_color="#1f2937", 
              command=abrir_pasta_sons).pack(side="left", padx=5)

# Botão Sobre movido para o topo (header)

shortcuts_label = ctk.CTkLabel(footer, text="", font=("Arial", 11), text_color="#9ca3af")
shortcuts_label.pack(side="right", padx=20)

remote_label = ctk.CTkLabel(footer, text="Controle Remoto: desligado", font=("Arial", 11), text_color="#9ca3af")
remote_label.pack(side="right", padx=10)
remote_led = ctk.CTkLabel(footer, text="●", font=("Arial", 12), text_color="#ef4444")
remote_led.pack(side="right")

def abrir_controle_remoto_info():
    win = ctk.CTkToplevel(app)
    win.title("Controle Remoto")
    win.geometry("520x560")
    win.resizable(False, False)
    win.transient(app)
    win.grab_set()
    win.lift()
    try:
        win.attributes("-topmost", True)
        def _unset_top():
            try:
                if win and win.winfo_exists():
                    win.attributes("-topmost", False)
            except:
                pass
        win.after(300, _unset_top)
    except:
        pass
    url = server.get_url()
    top = ctk.CTkFrame(win, fg_color="transparent")
    top.pack(expand=True, fill="both", padx=20, pady=20)
    ctk.CTkLabel(top, text="Acesse pelo celular", font=("Arial", 18, "bold")).pack(pady=(0,10))
    url_label = ctk.CTkLabel(top, text=url, font=("Arial", 14))
    url_label.pack(pady=6)
    pin_label = ctk.CTkLabel(top, text=f"PIN: {server.get_pin()}", font=("Arial", 14, "bold"))
    pin_label.pack(pady=6)

    status_row = ctk.CTkFrame(top, fg_color="transparent")
    status_row.pack(pady=6)
    status_text = ctk.CTkLabel(status_row, text="", font=("Arial", 12))
    status_text.pack(side="left", padx=6)
    status_led = ctk.CTkLabel(status_row, text="●", font=("Arial", 12))
    status_led.pack(side="left")
    conn_label = ctk.CTkLabel(status_row, text="Conectados: 0", font=("Arial", 12))
    conn_label.pack(side="left", padx=6)
    row = ctk.CTkFrame(top, fg_color="transparent")
    row.pack(pady=8)
    ctk.CTkButton(row, text="Abrir no Navegador", fg_color="#2563eb", hover_color="#1d4ed8",
                  command=lambda: webbrowser.open(url)).pack(side="left", padx=4)
    ctk.CTkButton(row, text="Copiar URL", fg_color="#374151", hover_color="#1f2937",
                  command=lambda: (app.clipboard_clear(), app.clipboard_append(url))).pack(side="left", padx=4)
    def regenerar():
        server.regenerate_pin()
        pin_label.configure(text=f"PIN: {server.get_pin()}")
        if remote_label:
            remote_label.configure(text=f"Controle Remoto: {server.get_url()}  PIN: {server.get_pin()}")
        render_qr()
    ctk.CTkButton(row, text="Trocar PIN", fg_color="#f59e0b", hover_color="#d97706",
                  command=regenerar).pack(side="left", padx=4)
    ctrl_row = ctk.CTkFrame(top, fg_color="transparent")
    ctrl_row.pack(pady=8)

    def ligar_servidor():
        server.start()
        pin_label.configure(text=f"PIN: {server.get_pin()}")
        url_label.configure(text=server.get_url())
        atualizar_status_servidor(True)
        atualizar_status_local(True)
        conn_label.configure(text=f"Conectados: {server.get_connections_count()} \u200b")

    def desligar_servidor():
        server.stop()
        atualizar_status_servidor(False)
        atualizar_status_local(False)
        conn_label.configure(text="Conectados: 0")

    ctk.CTkButton(ctrl_row, text="Ligar Servidor", fg_color="#10b981", hover_color="#059669",
                  command=ligar_servidor).pack(side="left", padx=4)
    ctk.CTkButton(ctrl_row, text="Desligar Servidor", fg_color="#ef4444", hover_color="#dc2626",
                  command=desligar_servidor).pack(side="left", padx=4)
    qr_frame = ctk.CTkFrame(top)
    qr_frame.pack(pady=8)
    qr_label = ctk.CTkLabel(qr_frame, text="")
    qr_label.pack()
    def render_qr():
        qr = qrcode.QRCode(box_size=8, border=2)
        qr.add_data(url)
        qr.make(fit=True)
        img = qr.make_image(fill_color="black", back_color="white").convert("RGB")
        img = img.resize((200, 200), Image.Resampling.LANCZOS)
        qr_img = ctk.CTkImage(light_image=img, dark_image=img, size=(200,200))
        qr_label.configure(image=qr_img)
        qr_label.image = qr_img
    render_qr()
    
    def atualizar_status_local(ligado):
        status_text.configure(text=("Servidor ligado" if ligado else "Servidor desligado"))
        try:
            status_led.configure(text_color=("#10b981" if ligado else "#ef4444"))
        except Exception:
            pass
    
    def monitor():
        try:
            atualizar_status_local(server.is_running())
            conn_label.configure(text=f"Conectados: {server.get_connections_count()} \u200b")
        finally:
            win.after(1500, monitor)
    atualizar_status_local(server.is_running())
    monitor()
    win.update_idletasks()
    w = win.winfo_width()
    h = win.winfo_height()
    x = (win.winfo_screenwidth() // 2) - (w // 2)
    y = (win.winfo_screenheight() // 2) - (h // 2)
    win.geometry(f"{w}x{h}+{x}+{y}")

def regenerar_pin():
    server.regenerate_pin()
    atualizar_status_servidor(server.is_running())

def atualizar_status_servidor(ligado):
    if remote_label:
        if ligado:
            remote_label.configure(text=f"Controle Remoto: {server.get_url()}  PIN: {server.get_pin()}")
        else:
            remote_label.configure(text=f"Controle Remoto: desligado")
    try:
        remote_led.configure(text_color="#10b981" if ligado else "#ef4444")
    except Exception:
        pass

def _monitorizar_servidor_footer():
    try:
        atualizar_status_servidor(server.is_running())
    finally:
        app.after(2000, _monitorizar_servidor_footer)

def atualizar_texto_atalhos():
    if config.get("atalhos_habilitados", True):
        try:
            total = len(config.get("botoes", []))
        except Exception:
            total = 10
        if total > 10:
            shortcuts_label.configure(text="⌨️ 1–0 (1–10), Q–P (11–20) | V reinicia | ←/→/↑/↓ navega | Enter aplica")
        else:
            shortcuts_label.configure(text="⌨️ 1–0 tocam | V reinicia | ←/→/↑/↓ navega | Enter aplica")
    else:
        shortcuts_label.configure(text="⌨️ Atalhos: Desabilitados", text_color="#6b7280")
    atualizar_dicas_atalhos()

def _show_playlist_preview(name):
    global playlist_preview_win, playlist_preview_label, playlist_preview_pos_label, playlist_preview_after_id, playlist_preview_frame, playlist_preview_effect_label
    try:
        playlists = listar_playlists()
        try:
            cur_idx = playlists.index(name)
        except ValueError:
            cur_idx = 0
        pos_text = f"{cur_idx+1} de {len(playlists)}"
        if playlist_preview_win is None or not playlist_preview_win.winfo_exists():
            playlist_preview_win = ctk.CTkToplevel(app)
            playlist_preview_win.title("Playlist")
            playlist_preview_win.geometry("600x140")
            playlist_preview_win.resizable(False, False)
            try:
                playlist_preview_win.transient(app); playlist_preview_win.lift()
                try:
                    playlist_preview_win.attributes("-topmost", True)
                    def _unset_topmost():
                        try:
                            if playlist_preview_win and playlist_preview_win.winfo_exists():
                                playlist_preview_win.attributes("-topmost", False)
                        except:
                            pass
                    playlist_preview_win.after(300, _unset_topmost)
                except:
                    pass
            except:
                pass
            playlist_preview_frame = ctk.CTkFrame(playlist_preview_win, fg_color=("#f3f4f6", "#111827"))
            playlist_preview_frame.pack(expand=True, fill="both")
            playlist_preview_label = ctk.CTkLabel(playlist_preview_frame, text=name, font=("Arial", 40, "bold"))
            playlist_preview_label.pack(pady=(18, 4))
            playlist_preview_pos_label = ctk.CTkLabel(playlist_preview_frame, text=pos_text, font=("Arial", 14))
            playlist_preview_pos_label.pack(pady=(0, 8))
            ctk.CTkLabel(playlist_preview_frame, text="↑/↓ muda • Enter aplica • Esc fecha", font=("Arial", 14)).pack()
            playlist_preview_effect_label = ctk.CTkLabel(playlist_preview_frame, text="", font=("Arial", 16))
            playlist_preview_win.update_idletasks()
            w = playlist_preview_win.winfo_width(); h = playlist_preview_win.winfo_height()
            x = (playlist_preview_win.winfo_screenwidth() // 2) - (w // 2)
            y = (playlist_preview_win.winfo_screenheight() // 2) - (h // 2)
            playlist_preview_win.geometry(f"{w}x{h}+{x}+{y}")
            try:
                playlist_preview_win.bind("<Escape>", lambda e: _hide_playlist_preview())
                playlist_preview_win.bind("<Return>", lambda e: _apply_playlist_from_preview())
                playlist_preview_win.bind("<KP_Enter>", lambda e: _apply_playlist_from_preview())
            except Exception:
                pass
        else:
            playlist_preview_label.configure(text=name)
            try:
                playlist_preview_pos_label.configure(text=pos_text)
            except Exception:
                pass
        try:
            if playlist_preview_after_id:
                playlist_preview_win.after_cancel(playlist_preview_after_id)
        except Exception:
            pass
        try:
            playlist_preview_after_id = playlist_preview_win.after(5000, _hide_playlist_preview)
        except Exception:
            pass
    except:
        pass

def _apply_playlist_from_preview():
    global playlist_preview_win, playlist_preview_frame, playlist_preview_effect_label, playlist_preview_name
    try:
        if playlist_preview_name:
            try:
                if playlist_preview_effect_label:
                    playlist_preview_effect_label.configure(text="✔ Aplicando...")
                    playlist_preview_effect_label.pack(pady=(6, 10))
            except Exception:
                pass
            try:
                if playlist_preview_frame:
                    playlist_preview_frame.configure(fg_color=("#d1fae5", "#064e3b"))
            except Exception:
                pass
            def _do_apply():
                try:
                    if playlist_preview_frame:
                        playlist_preview_frame.configure(fg_color=("#f3f4f6", "#111827"))
                except Exception:
                    pass
                try:
                    trocar_playlist(playlist_preview_name, silent=True)
                except Exception:
                    pass
                _hide_playlist_preview()
            try:
                if playlist_preview_win:
                    playlist_preview_win.after(220, _do_apply)
                else:
                    _do_apply()
            except Exception:
                _do_apply()
    except Exception:
        pass

def _hide_playlist_preview():
    global playlist_preview_win, playlist_preview_label, playlist_preview_after_id, playlist_preview_name, playlist_preview_frame, playlist_preview_effect_label
    try:
        if playlist_preview_win and playlist_preview_win.winfo_exists():
            try:
                if playlist_preview_after_id:
                    playlist_preview_win.after_cancel(playlist_preview_after_id)
            except Exception:
                pass
            playlist_preview_win.destroy()
    except:
        pass
    playlist_preview_after_id = None
    playlist_preview_win = None
    playlist_preview_label = None
    playlist_preview_name = None
    playlist_preview_frame = None
    playlist_preview_effect_label = None

def on_key(event):
    try:
        focus_widget = app.focus_get()
    except Exception:
        focus_widget = None
    try:
        focus_top = focus_widget.winfo_toplevel() if focus_widget else None
    except Exception:
        focus_top = None
    try:
        focus_class = focus_widget.winfo_class() if focus_widget else ""
    except Exception:
        focus_class = ""
    if focus_top is not None and focus_top != app and focus_top != playlist_preview_win:
        return
    if focus_class in ("Entry", "Text", "TCombobox"):
        return
    ch = (event.char or "").lower()
    ks = event.keysym
    if ks in ('Left','Right','Up','Down','Return','KP_Enter','Escape'):
        if ks == 'Right':
            try:
                globals()['playlist_preview_name'] = current_playlist if not playlist_preview_name else playlist_preview_name
                _show_playlist_preview(globals()['playlist_preview_name'])
            except Exception:
                pass
            return
        if ks == 'Left':
            try:
                _hide_playlist_preview()
            except Exception:
                pass
            return
        if ks in ('Up','Down'):
            try:
                playlists = listar_playlists()
                cur = playlist_preview_name if playlist_preview_name else current_playlist
                try:
                    idx = playlists.index(cur)
                except ValueError:
                    idx = 0
                idx = (idx - 1) % len(playlists) if ks == 'Up' else (idx + 1) % len(playlists)
                new_name = playlists[idx]
                globals()['playlist_preview_name'] = new_name
                _show_playlist_preview(new_name)
            except Exception:
                pass
            return
        if ks in ('Return','KP_Enter'):
            try:
                if playlist_preview_name:
                    _apply_playlist_from_preview()
            except Exception:
                pass
            return
        if ks == 'Escape':
            globals()['playlist_preview_name'] = None
            _hide_playlist_preview()
            return
    if ch.isdigit():
        if not config.get("atalhos_habilitados", True):
            return
        tecla = int(ch)
        index = 9 if tecla == 0 else tecla - 1
        if 0 <= index < len(button_refs):
            tocar_som(index)
    elif ch in "qwertyuiop":
        if not config.get("atalhos_habilitados", True):
            return
        base = {"q":10, "w":11, "e":12, "r":13, "t":14, "y":15, "u":16, "i":17, "o":18, "p":19}
        index = base.get(ch, -1)
        if 0 <= index < len(button_refs):
            tocar_som(index)
    elif event.keysym == 'space':
        pausar_retomar()
    elif event.char.lower() == 'v':
        reiniciar_musica()

app.bind_all("<Key>", on_key)
app.bind_all("<KeyPress>", on_key)

app.protocol("WM_DELETE_WINDOW", lambda: (pygame.mixer.music.stop(), app.destroy()))
atualizar_estilos()
atualizar_texto_atalhos()
def _start_remote():
    _monitorizar_servidor_footer()
# Popup de WhatsApp removido
def _ensure_app_focus():
    try:
        app.attributes("-topmost", True)
    except:
        pass
    # evitar forçar foco para não causar erros de janela destruída
    try:
        def _unset_top():
            try:
                if app and app.winfo_exists():
                    app.attributes("-topmost", False)
            except:
                pass
        app.after(300, _unset_top)
    except:
        pass
try:
    app.after(200, _ensure_app_focus)
except:
    pass
app.mainloop()
