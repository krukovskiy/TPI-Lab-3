"""
Ejecuta el flujo GUI del screentone: carga imagen, aplica patrones, muestra/guarda resultados.
"""

import cv2
import sys
from gui_tkinter.image_loader import load_image
from app.services.screentone import ScreentoneProcessor

def run_screentone_app():
    rgb_image, path = load_image()
    hsv_image = cv2.cvtColor(rgb_image, cv2.COLOR_RGB2HSV)

    processor = ScreentoneProcessor(rgb_image.shape)
    processed_image = processor.apply(hsv_image, rgb_image)
    superimposed_image = processor.superimpose(rgb_image, processed_image)

    processor.show(rgb_image, processed_image, superimposed_image)
    processor.save(rgb_image, processed_image, superimposed_image, path)
    sys.exit()