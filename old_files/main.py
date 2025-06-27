"""
Menú principal con GUI para elegir entre aplicar screentone o inspeccionar colores.
"""

import tkinter as tk
from screentone_gui import run_screentone_app
from color_inspector_gui import run_color_inspector

class MainApplication:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("Image Processing Suite")
        self.root.geometry("600x400")
        self.setup_ui()
        
    def setup_ui(self):
        tk.Label(
            self.root, 
            text="Image Processing Suite", 
            font=("Helvetica", 18, "bold")
        ).pack(pady=20)
        
        tk.Label(
            self.root, 
            text="Seleccione una opción:", 
            font=("Helvetica", 12)
        ).pack(pady=10)

        tk.Button(
            self.root, 
            text="1. Aplicar Screentone", 
            font=("Helvetica", 14), 
            width=25,
            height=2,
            command=self.run_screentone
        ).pack(pady=10)

        tk.Button(
            self.root, 
            text="2. Inspeccionar Colores", 
            font=("Helvetica", 14), 
            width=25,
            height=2,
            command=self.run_color_inspector
        ).pack(pady=10)

        tk.Button(
            self.root, 
            text="Salir",
            font=("Helvetica", 14), 
            width=25,
            height=1,
            command=self.on_close
        ).pack(pady=20)

    def run_screentone(self):
        """Launch screentone application as separate window"""
        self.root.withdraw()
        try:
            screentone_app = run_screentone_app()
            screentone_app.run()
        except Exception as e:
            print(f"Error running screentone: {e}")
        finally:
            self.root.deiconify()

    def run_color_inspector(self):
        """Launch color inspector as separate window"""
        self.root.withdraw()
        try:
            color_app = run_color_inspector()
            color_app.run()
        except Exception as e:
            print(f"Error running color inspector: {e}")
        finally:
            self.root.deiconify()

    def on_close(self):
        """Clean shutdown"""
        self.root.quit()
        self.root.destroy()

    def run(self):
        """Start the main application"""
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)
        self.root.mainloop()

if __name__ == "__main__":
    app = MainApplication()
    app.run()