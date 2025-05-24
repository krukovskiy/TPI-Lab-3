
import tkinter as tk
from screentone_gui import run_screentone_app
from color_inspector import run_color_inspector

def main_menu():
    root = tk.Tk()
    root.title("Menú Principal")

    def on_close():
        root.quit()
        root.destroy()

    root.protocol("WM_DELETE_WINDOW", on_close)

    tk.Label(root, text="Seleccione una opción", font=("Helvetica", 16)).pack(pady=20)

    tk.Button(root, text="1. Aplicar Screentone", font=("Helvetica", 14), width=30,
              command=run_screentone_app).pack(pady=10)

    tk.Button(root, text="2. Inspeccionar Colores", font=("Helvetica", 14), width=30,
              command=lambda: [root.withdraw(), run_color_inspector(), root.deiconify()]).pack(pady=10)

    tk.Button(root, text="Salir", font=("Helvetica", 14), width=30, command=on_close).pack(pady=20)

    root.mainloop()

if __name__ == "__main__":
    main_menu()