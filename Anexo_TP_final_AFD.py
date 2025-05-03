import tkinter as tk
from tkinter import filedialog
from PIL import Image, ImageTk
import cv2
import webcolors

def cargar_imagen(root):
    root.withdraw()
    filename = filedialog.askopenfilename(title="Seleccionar imagen")
    root.deiconify()
    if not filename:
        raise FileNotFoundError("No se seleccionó ninguna imagen.")
    imagen_bgr = cv2.imread(filename)
    imagen_rgb = cv2.cvtColor(imagen_bgr, cv2.COLOR_BGR2RGB)
    return imagen_rgb, filename

def get_color(event):
    x, y = event.x, event.y
    if 0 <= x < img.width and 0 <= y < img.height:
        color = img.getpixel((x, y))

        try:
            closest_name = actual_name = webcolors.rgb_to_name(color)
        except ValueError:
            distances = {}
            for name in webcolors.names():
                r_c, g_c, b_c = webcolors.name_to_rgb(name)
                rd = (r_c - color[0]) ** 2
                gd = (g_c - color[1]) ** 2
                bd = (b_c - color[2]) ** 2
                distances[name] = rd + gd + bd
            closest_name = min(distances, key=distances.get)
            actual_name = None

        if actual_name:
            name_text = f"{actual_name}"
        else:
            name_text = f"Closest: {closest_name}"

        color_label.config(text=f"RGB({x}, {y}) = {color} → {name_text}")

root = tk.Tk()
root.title("Color bajo el cursor")

cv_img, path = cargar_imagen(root)
img = Image.fromarray(cv_img)
tk_image = ImageTk.PhotoImage(img)

label = tk.Label(root, image=tk_image)
label.pack()

color_label = tk.Label(root, text="Mueve el cursor sobre la imagen para ver el color")
color_label.pack()

label.bind("<Motion>", get_color)

root.mainloop()
