"""
Genera patrones de screentone monocromáticos como arreglos numpy en escala de grises.
Se utilizan para superponer texturas en las regiones de color detectadas.
"""

import numpy as np
from PIL import Image, ImageDraw

def create_pattern(pattern_type, size=60):
    thickness = max(1, size //20)
    spacing = max(3, size // 10)
    dot_size = max(1, size // 12)

    img = Image.new('L', (size, size), 255)
    draw = ImageDraw.Draw(img)

    if pattern_type == 'horizontal_stripes':
        for i in range(0, size, spacing):
            draw.line((0, i, size, i), fill=0, width=thickness)
    elif pattern_type == 'vertical_stripes':
        for i in range(0, size, spacing):
            draw.line((i, 0, i, size), fill=0, width=thickness)
    elif pattern_type == 'diagonal_stripes':
        for i in range(-size, size, spacing):
            draw.line((i, size, i + size, 0), fill=0, width=thickness)
    elif pattern_type == 'large_dots':
        for x in range(dot_size, size, 6):
            for y in range(dot_size, size, 6):
                draw.ellipse((x, y, x+dot_size, y+dot_size), fill=0)
    elif pattern_type == 'small_dots':
        for x in range(dot_size, size, 3):
            for y in range(dot_size, size, 3):
                draw.ellipse((x, y, x+dot_size, y+dot_size), fill=0)
    elif pattern_type == 'double_diagonals':
        for i in range(-size, size, spacing):
            draw.line((i, size, i+size, 0), fill=0, width=thickness)
            draw.line((i, 0, i+size, size), fill=0, width=thickness)
    elif pattern_type == 'grid':
        for x in range(0, size, spacing):
            for y in range(0, size, spacing):
                draw.rectangle((x, y, x+2, y+2), outline=0, width=thickness)
    elif pattern_type == 'zigzag':
        for i in range(0, size, spacing):
            draw.line((i, 0, i+1, size), fill=0, width=thickness)
            draw.line((i+1, 0, i, size), fill=0, width=thickness)
    elif pattern_type == 'waves':
        for i in range(0, size, spacing):
            draw.line((i, int(size/2), i+2, int(size/2)-2), fill=0, width=thickness)
            draw.line((i+2, int(size/2)-2, i+4, int(size/2)), fill=0, width=thickness)

    return np.array(img)
