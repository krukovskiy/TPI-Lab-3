"""
Este módulo gestiona la carga de imágenes mediante un selector de archivos GUI.
Devuelve la imagen seleccionada en formato RGB junto con la ruta del archivo.
"""

import cv2
from tkinter import Tk, filedialog

def load_image():
    """Ventana de diálogo para seleccionar un archivo e importa la imagen como RGB."""
    Tk().withdraw()
    filename = filedialog.askopenfilename(title="Select Image")
    if not filename:
        raise FileNotFoundError("No image selected.")
    image_bgr = cv2.imread(filename)
    if image_bgr is None:
        return None, None

    image_rgb = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2RGB)
    return image_rgb, filename
