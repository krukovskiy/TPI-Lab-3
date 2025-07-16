"""
Lanza la ventana del inspector de color.
"""
import sys
from tkinter import filedialog
from PIL import Image
from app.services.color_inspector import inspect_colors

def run_color_inspector():
    file_path = filedialog.askopenfilename(
                filetypes=[("Image Files", "*.png *.jpg *.jpeg *.bmp")]
            )
    if not file_path:
        return

    image = Image.open(file_path)
    inspect_colors(image)  # Pasa la imagen a la función
    sys.exit()
