"""
Lanza la ventana del inspector de color.
"""

import tkinter as tk
from legacy.cursor_with_color_name import ColorInspectorApp

def run_color_inspector():
    inspector_window = tk.Toplevel()
    app = ColorInspectorApp(inspector_window)