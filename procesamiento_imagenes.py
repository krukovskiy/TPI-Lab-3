import cv2
from matplotlib import pyplot as plt

url_imagen_original = "./Imagenes/hydrangea.jpg"


# Leer la imagen desde la carpeta correcta (mayúscula en 'Imagenes')
imagen_original = cv2.imread("Imagenes/hydrangea.jpg")

# Si la imagen está en RGB originalmente, la convertimos a BGR para OpenCV
imagen_bgr = cv2.cvtColor(imagen_original, cv2.COLOR_RGB2BGR)

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
    plt.close()

for niveles in [2, 4, 8, 16, 32, 64, 128, 256]:
    reducir_tonos_grises_imagen(imagen_gris, niveles)

# -----------------------
# Detección de bordes con Canny
# -----------------------

def detectar_bordes_canny(imagen_gris, umbral1=100, umbral2=200, nombre_archivo="hydrangea_bordes_canny.jpg"):
    bordes = cv2.Canny(imagen_gris, umbral1, umbral2)
    plt.imshow(bordes, cmap='gray')
    plt.axis("off")
    plt.title(f"Bordes Canny ({umbral1}, {umbral2})")
    plt.savefig(f'Imagenes/{nombre_archivo}')
    plt.close()

detectar_bordes_canny(imagen_gris, 50, 150, "hydrangea_bordes_canny_50_150.jpg")
detectar_bordes_canny(imagen_gris, 150, 250, "hydrangea_bordes_canny_150_250.jpg")
