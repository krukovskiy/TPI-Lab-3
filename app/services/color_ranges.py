"""
Define los rangos de color HSV para detectar tonos específicos en una imagen.
Cada color se asocia a una tupla de (límite_inferior, límite_superior) en HSV.
"""

color_ranges = {
                'red1':     [(0, 100, 100), (5, 255, 255)],
                'red2':     [(175, 100, 100), (180, 255, 255)],
                'orange':   [(6, 120, 120), (20, 255, 255)],
                'yellow':   [(21, 100, 100), (30, 255, 255)],
                'green':    [(36, 80, 80), (85, 255, 255)],
                'cyan':     [(86, 80, 80), (95, 255, 255)],
                'blue':     [(96, 80, 80), (130, 255, 255)],
                'purple':   [(131, 80, 80), (160, 255, 255)],
                'pink':     [(161, 80, 80), (169, 255, 255)],
                }