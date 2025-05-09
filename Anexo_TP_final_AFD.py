import tkinter as tk
import tkinter.font as tkFont
import webcolors
import numpy as np
from PIL import Image, ImageDraw, ImageTk
from typing import Tuple, Optional, List
from image_loader import ImageLoader

class ColorInspectorApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("Color Under Cursor")
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

        self.image: Optional[Image.Image] = None
        self.tk_image: Optional[ImageTk.PhotoImage] = None
        self.marked_points: List[Tuple[int, int, str]] = []
        
        image_rgb, *_ = ImageLoader.load()
        self.image = Image.fromarray(image_rgb)
        self.image_np = np.array(self.image)
        self.font = tkFont.Font(family="Helvetica", size=20)
        self.last_pixel: Optional[Tuple[int, int]] = None

        self.css3_rgb = {
                            name: webcolors.name_to_rgb(name)
                            for name in webcolors.names()
                        }

        self._setup_widgets()

    def _setup_widgets(self):
        self.tk_image = ImageTk.PhotoImage(self.image)

        self.canvas = tk.Canvas(self.root, width=self.image.width, height=self.image.height)
        self.canvas.pack()
        self.canvas_img = self.canvas.create_image(0, 0, anchor=tk.NW, image=self.tk_image)

        self.color_label = tk.Label(self.root, text="Move the cursor or click to mark a color")
        self.color_label.pack()

        self.canvas.bind("<Motion>", self._on_mouse_move)

    def _get_color_name(self, rgb: Tuple[int, int, int]) -> str:
        try:
            return webcolors.rgb_to_name(rgb).capitalize()
        except ValueError:
            r1, g1, b1 = rgb
            closest_name = min(
                                self.css3_rgb,
                                key=lambda name: (
                                                    (r1 - self.css3_rgb[name][0]) ** 2 +
                                                    (g1 - self.css3_rgb[name][1]) ** 2 +
                                                    (b1 - self.css3_rgb[name][2]) ** 2
                                                )
                                )
            return f"Closest: {closest_name.capitalize()}"

    def _on_mouse_move(self, event: tk.Event):
        if self.image is None:
            return

        x, y = event.x, event.y
        if not (0 <= x < self.image.width and 0 <= y < self.image.height):
            return

        if self.last_pixel == (x, y):
            return
        self.last_pixel = (x, y)

        rgb = tuple(int(c) for c in self.image_np[y, x])
        name_text = self._get_color_name(rgb)

        # Clear previous overlays
        self.canvas.delete("overlay")

        # Draw circle and text
        self.canvas.create_oval(x - 5, y - 5, x + 5, y + 5, outline="black", width=2, tags="overlay")
        self.canvas.create_text(x + 10, y - 10, text=name_text, fill="black", anchor="nw", font=self.font, tags="overlay")

        self.color_label.config(text=f"RGB({x}, {y}) = {rgb} → {name_text}")

    def _draw_overlay(self, draw: ImageDraw.ImageDraw, x: int, y: int, text: str):
        text_color = "black"
        draw.ellipse((x - 5, y - 5, x + 5, y + 5), outline=text_color, width=2)
        draw.text((x + 10, y - 10), text, fill=text_color, font=self.font)

    def _on_close(self):
        self.root.quit()
        self.root.destroy()

if __name__ == "__main__":
    root = tk.Tk()
    app = ColorInspectorApp(root)
    root.mainloop()

