"""
Lanza la ventana del inspector de color.
"""

import tkinter as tk
from color_inspector import run_color_inspector

def run_color_inspector():
    window = tk.Toplevel()
    app = run_color_inspector(window)
    return app