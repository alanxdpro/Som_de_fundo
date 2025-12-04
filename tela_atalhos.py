import tkinter as tk
import customtkinter as ctk

def abrir_tela_atalhos(config_win=None):
    if config_win:
        config_win.withdraw()
    
    tela_atalhos = ctk.CTkToplevel()
    tela_atalhos.title("Atalhos do Programa")
    tela_atalhos.geometry("320x240")
    tela_atalhos.resizable(False, False)
    tela_atalhos.grab_set()

    def ao_fechar():
        if config_win:
            config_win.deiconify()
        tela_atalhos.destroy()

    tela_atalhos.protocol("WM_DELETE_WINDOW", ao_fechar)

    texto = (
        "Atalhos disponíveis:\n\n"
        "1 - 0 \t-  Botões de 1 a 10\n"
        "Espaço \t-  Pausar/Parar\n"
        "R \t-  Reiniciar música\n"
        "Seta ↑ \t-  Playlist anterior\n"
        "Seta ↓ \t-  Próxima playlist"
    )

    label = ctk.CTkLabel(tela_atalhos, text=texto, font=("Arial", 15), justify="left")
    label.pack(padx=20, pady=20)

    botao_frame = ctk.CTkFrame(tela_atalhos, fg_color="transparent")
    botao_frame.pack(pady=10)
    
    ctk.CTkButton(botao_frame, text="Fechar", command=ao_fechar, 
                  width=150, height=35, font=("Arial", 12, "bold")).pack()

    return tela_atalhos