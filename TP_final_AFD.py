import cv2
import numpy as np
from PIL import Image, ImageDraw
import matplotlib.pyplot as plt
from tkinter import Tk, filedialog
import os

# Se necesitan 6 pasos para lograr el efecto deseado, por lo que se crearán seis funciones distintas para:
# Cargar la imagen.
# Crear patrones visuales que cambien la imagen a escala de grises.
# Crear la imagen con los screentones para aumentar el contraste entre los colores.
# Mostrar el resultado.
# Guardar el resultado.
# Ejecutar el código (main)


# Función para cargar imagen usando diálogo de selección de archivos
def cargar_imagen():
    Tk().withdraw()  # Oculta la ventana principal de Tkinter
    filename = filedialog.askopenfilename(title="Seleccionar imagen")
    if not filename:
        raise FileNotFoundError("No se seleccionó ninguna imagen.")
    imagen_bgr = cv2.imread(filename)  # Carga imagen en formato BGR
    imagen_rgb = cv2.cvtColor(imagen_bgr, cv2.COLOR_BGR2RGB)  # Convierte a RGB
    return imagen_rgb, filename

# Función para crear patrones visuales en escala de grises
def crear_patron(tipo, tamaño=6):
    img = Image.new('L', (tamaño, tamaño), 255)  #Crea una imagen en escala de grises rellena de blano (255)
    draw = ImageDraw.Draw(img) # Habilita que se dibujen los patrones en la imagen.
    if tipo == 'rayas_horizontales':
        for i in range(0, tamaño, 2):
            draw.line((0, i, tamaño, i), fill=0, width=1)  # Patrón de líneas horizontales
    elif tipo == 'puntos_grandes':
        for x in range(1, tamaño, 6):
            for y in range(1, tamaño, 6):
                draw.ellipse((x, y, x+3, y+3), fill=0)  #Patrón de puntos circulares. Dibuja puntos negros.
    elif tipo == 'diagonales_dobles':
        for i in range(-tamaño, tamaño, 2):
            draw.line((i, tamaño, i+tamaño, 0), fill=0, width=1)  #Patrón de Diagonal /
            draw.line((i, 0, i+tamaño, tamaño), fill=0, width=1)  #Patrón de Diagonal \
    elif tipo == 'cuadricula':
        for x in range(0, tamaño, 2):
            for y in range(0, tamaño, 2):
                draw.rectangle((x, y, x+2, y+2), outline=0, width=1)  #Patrón de cuadros
    return np.array(img)  #Devuelve como array NumPy para su uso posterior.

# Función que aplica los screentones a cada color. Aplica un patrón distinto a cada color. 4 patrones para 4 colores.
def aplicar_screentones(imagen_hsv, imagen_rgb):
    #Rangos HSV para distintos colores. Define los rangos HSV (hue, saturation, value) que indican qué píxeles son de cada color relevante.
    rangos_colores = {
        'rojo':  [(0, 70, 50), (10, 255, 255)],
        'verde': [(36, 25, 25), (86, 255,255)],
        'azul':  [(94, 80, 2), (126, 255, 255)],
        'amarillo': [(15, 100, 100), (35, 255, 255)]
    }

    #Patrones visuales asignados a cada color
    patrones = {
        'verde': crear_patron('rayas_horizontales'),
        'azul': crear_patron('puntos_grandes'),
        'rojo': crear_patron('diagonales_dobles'),
        'amarillo': crear_patron('cuadricula')
    }

    #Imagen en blanco donde se aplicarán los patrones
    imagen_resultado = np.ones(imagen_rgb.shape[:2], dtype=np.uint8) * 255

    for color, (bajo, alto) in rangos_colores.items():
        #Crear máscara para detectar zonas del color actual
        mascara = cv2.inRange(imagen_hsv, np.array(bajo), np.array(alto))

        #Suavizar bordes de la máscara
        kernel = np.ones((3, 3), np.uint8)
        mascara = cv2.dilate(mascara, kernel, iterations=1)

        #Repetir patrón hasta cubrir toda la imagen
        patron = patrones[color]
        tile = np.tile(patron, (imagen_resultado.shape[0] // patron.shape[0] + 1,
                                imagen_resultado.shape[1] // patron.shape[1] + 1))
        tile = tile[:imagen_resultado.shape[0], :imagen_resultado.shape[1]]

        #Aplicar patrón en las zonas donde la máscara es blanca (255)
        imagen_resultado = np.where(mascara == 255, tile, imagen_resultado)

    #Aumenta ligeramente el contraste del resultado final. Esto mejora la calidad de la imagen final.
    imagen_resultado = cv2.convertScaleAbs(imagen_resultado, alpha=1.2, beta=0)
    return imagen_resultado

#Muestra la imagen original y la imagen procesada lado a lado
def mostrar_resultado(imagen_rgb, imagen_resultado):
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 6))
    ax1.imshow(imagen_rgb)
    ax1.set_title('Imagen Original')
    ax1.axis('off')
    ax2.imshow(imagen_resultado, cmap='gray')
    ax2.set_title('Resultado con screentones')
    ax2.axis('off')
    plt.tight_layout()
    plt.show()

# Guarda el resultado como archivo PNG junto a la imagen original
def guardar_resultado(imagen_resultado, original_path):
    #Extrae nombre base sin extensión
    nombre = os.path.splitext(os.path.basename(original_path))[0]
    #Extrae carpeta de la imagen original
    carpeta = os.path.dirname(original_path)
    #Convierte array en imagen PIL y guarda como JPG
    resultado_img = Image.fromarray(imagen_resultado)
    ruta_guardado = os.path.join(carpeta, f"{nombre}_procesada.jpg")
    resultado_img.save(ruta_guardado, format='JPEG')
    print(f"Imagen guardada como {ruta_guardado}")

# Función principal del programa
def main():
    imagen_rgb, ruta = cargar_imagen()
    imagen_hsv = cv2.cvtColor(imagen_rgb, cv2.COLOR_RGB2HSV)
    imagen_resultado = aplicar_screentones(imagen_hsv, imagen_rgb)
    mostrar_resultado(imagen_rgb, imagen_resultado)
    guardar_resultado(imagen_resultado, ruta)

# Llama a main si se ejecuta este archivo directamente
if __name__ == "__main__":
    main()
