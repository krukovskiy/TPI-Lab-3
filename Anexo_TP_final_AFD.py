import tkinter as tk
from tkinter import filedialog
from PIL import Image, ImageDraw, ImageTk, ImageFont
import cv2
import webcolors
from typing import Tuple, Optional, List


class ColorInspectorApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("Color Under Cursor")

        self.image: Optional[Image.Image] = None
        self.tk_image: Optional[ImageTk.PhotoImage] = None
        self.marked_points: List[Tuple[int, int, str]] = []

        self._load_image()
        self._setup_widgets()

        # Precompute RGB tuples for faster closest name lookup
        self.css3_rgb = {
                            name: webcolors.name_to_rgb(name)
                            for name in webcolors.names()
                        }

    def _load_image(self):
        self.root.withdraw()
        filename = filedialog.askopenfilename(title="Select Image")
        self.root.deiconify()

        if not filename:
            raise FileNotFoundError("No image selected.")

        image_bgr = cv2.imread(filename)
        image_rgb = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2RGB)
        self.image = Image.fromarray(image_rgb)

    def _setup_widgets(self):
        self.tk_image = ImageTk.PhotoImage(self.image)
        self.label = tk.Label(self.root, image=self.tk_image)
        self.label.pack()

        self.color_label = tk.Label(
            self.root, text="Move the cursor or click to mark a color"
        )
        self.color_label.pack()

        self.label.bind("<Motion>", self._on_mouse_move)

    def _get_color_name(self, rgb: Tuple[int, int, int]) -> str:
        try:
            return webcolors.rgb_to_name(rgb)
        except ValueError:
            # Find closest color
            r1, g1, b1 = rgb
            closest_name = min(
                                self.css3_rgb,
                                key=lambda name: (r1 - self.css3_rgb[name][0]) ** 2 +
                                                (g1 - self.css3_rgb[name][1]) ** 2 +
                                                (b1 - self.css3_rgb[name][2]) ** 2
                                )
            return f"Closest: {closest_name.capitalize()}"

    def _on_mouse_move(self, event: tk.Event):
        if self.image is None:
            return

        x, y = event.x, event.y
        if not (0 <= x < self.image.width and 0 <= y < self.image.height):
            return

        rgb = self.image.getpixel((x, y))
        name_text = self._get_color_name(rgb)

        image_copy = self.image.copy()
        draw = ImageDraw.Draw(image_copy)

        self._draw_overlay(draw, x, y, name_text)

        self.tk_image = ImageTk.PhotoImage(image_copy)
        self.label.config(image=self.tk_image)
        self.color_label.config(text=f"RGB({x}, {y}) = {rgb} → {name_text}")

    def _draw_overlay(self, draw, x, y, text):

        font = ImageFont.load_default(size=40)
        text_color = "black"

        draw.ellipse((x - 5, y - 5, x + 5, y + 5), outline=text_color, width=2)
        draw.text((x + 10, y - 10), text, fill=text_color, font=font)

if __name__ == "__main__":
    root = tk.Tk()
    app = ColorInspectorApp(root)
    root.mainloop() 
