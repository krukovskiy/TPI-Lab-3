"""
Lógica principal para aplicar screentones basada en la detección de color HSV.
Genera un resultado en escala de grises y lo superpone sobre la imagen original.
"""

import cv2
import numpy as np
from PIL import Image
import matplotlib.pyplot as plt
import os
from patterns import create_pattern
from color_ranges import color_ranges

class ScreentoneProcessor:
    def __init__(self, image_shape):
        height, width = image_shape[:2]
        pattern_size = max(30, min(width, height) // 10)
        self.pattern_map = {
                            'red1': create_pattern('horizontal_stripes', pattern_size),
                            'red2': create_pattern('horizontal_stripes', pattern_size),
                            'orange': create_pattern('grid', pattern_size),
                            'yellow': create_pattern('vertical_stripes', pattern_size),
                            'green': create_pattern('large_dots', pattern_size),
                            'cyan': create_pattern('double_diagonals', pattern_size),
                            'blue': create_pattern('diagonal_stripes', pattern_size),
                            'purple': create_pattern('zigzag', pattern_size),
                            'pink': create_pattern('waves', pattern_size),
                            }

    def apply(self, hsv_image, rgb_image):
        result_image = np.ones(rgb_image.shape[:2], dtype=np.uint8) * 255

        for color, (low, high) in color_ranges.items():
            mask = cv2.inRange(hsv_image, np.array(low), np.array(high))
            mask = cv2.GaussianBlur(mask, (5, 5), 0)
            _, mask = cv2.threshold(mask, 50, 255, cv2.THRESH_BINARY)
            mask = cv2.dilate(mask, np.ones((3, 3), np.uint8), iterations=1)

            tile = np.tile(self.pattern_map[color], (result_image.shape[0] // self.pattern_map[color].shape[0] + 1,
                                                     result_image.shape[1] // self.pattern_map[color].shape[1] + 1))
            tile = tile[:result_image.shape[0], :result_image.shape[1]]

            result_image = np.where(mask == 255, tile, result_image)

        return cv2.convertScaleAbs(result_image, alpha=1.2, beta=0)
    
    def superimpose(self, rgb_image, screentone_gray):
        screentone_rgb = cv2.cvtColor(screentone_gray, cv2.COLOR_GRAY2RGB)
        mask = screentone_gray < 100
        result = rgb_image.copy()
        result[mask] = screentone_rgb[mask]
        return result

    def show(self, rgb_image, processed_image, superimposed_image):
        fig, (ax1, ax2, ax3) = plt.subplots(1, 3, figsize=(18, 6))
        ax1.imshow(rgb_image)
        ax1.set_title('Original Image')
        ax1.axis('off')
        ax2.imshow(processed_image, cmap='gray')
        ax2.set_title('Screentone Result')
        ax2.axis('off')
        ax3.imshow(superimposed_image)
        ax3.set_title("Superimposed")
        ax3.axis('off')
        plt.tight_layout()
        plt.show()

    def save(self, original_image, screentone_image, superimposed_image, original_path):
        base_name = os.path.splitext(os.path.basename(original_path))[0]
        folder = os.path.dirname(original_path)

        Image.fromarray(original_image).save(os.path.join(folder, f"{base_name}_original.jpg"))
        Image.fromarray(screentone_image).save(os.path.join(folder, f"{base_name}_screentone.jpg"))
        Image.fromarray(superimposed_image).save(os.path.join(folder, f"{base_name}_superimposed.jpg"))