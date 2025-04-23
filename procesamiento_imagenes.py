import cv2
from matplotlib import pyplot as plt

url_imagen_original = "Imagenes/hydrangea.jpg"


# Leer la imagen desde la carpeta correcta (mayúscula en 'Imagenes')
imagen_original = cv2.imread("Imagenes/hydrangea.jpg")

# Si la imagen está en RGB originalmente, la convertimos a BGR para OpenCV
imagen_bgr = cv2.cvtColor(imagen_original, cv2.COLOR_RGB2BGR)

# armar el gráfico
plt.imshow(imagen_bgr)
plt.axis("off")
plt.title("Imagen en BGR")
plt.savefig('Imagenes/hydrangea_BGR.jpg')
plt.close()


# pasarla a escala de grises
url_imagen_BGR = "Imagenes/hydrangea_BGR.jpg"

imagen_gris = cv2.imread(url_imagen_original, cv2.IMREAD_GRAYSCALE)

plt.imshow(imagen_gris, cmap='gray')
plt.axis("off")
plt.title("Imagen en escala de grises")
plt.savefig('Imagenes/hydrangea_escala_gris.jpg')
plt.close() 

# Funcion para reducir la cantidad de tonos de gris en la imagen:

def reducir_tonos_grises(imagen, niveles):
    factor = 256 // niveles
    imagen_cuantizada = (imagen//factor) * factor
    return imagen_cuantizada


# Funcion para graficar las nuevas imagenes grises:

def reducir_tonos_grises_imagen(imagen, niveles):
    nueva_imagen = reducir_tonos_grises(imagen, niveles)
    plt.imshow(nueva_imagen, cmap='gray')
    plt.axis("off")
    plt.title(f"Imagen en escala {niveles} de grises")
    plt.savefig(f'Imagenes/hydrangea_escala_gris_reducida_{niveles}.jpg')
    plt.close


reducir_tonos_grises_imagen(imagen_gris, 4)
reducir_tonos_grises_imagen(imagen_gris, 8)
reducir_tonos_grises_imagen(imagen_gris, 16)
reducir_tonos_grises_imagen(imagen_gris, 32)
reducir_tonos_grises_imagen(imagen_gris, 64)
reducir_tonos_grises_imagen(imagen_gris, 128)
reducir_tonos_grises_imagen(imagen_gris, 256)