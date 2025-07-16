import cv2
import numpy as np
import webcolors
from functools import lru_cache
from typing import Tuple
from PIL import Image

# Pre-compute CSS3 colors for faster lookups
CSS3_NAMES = list(webcolors._definitions._CSS3_NAMES_TO_HEX.keys())
CSS3_RGB = np.array([
                        webcolors.hex_to_rgb(webcolors._definitions._CSS3_NAMES_TO_HEX[name]) 
                        for name in CSS3_NAMES
                    ])

@lru_cache(maxsize=4096)  # Cache color name lookups
def get_nearest_color_name(rgb_tuple: Tuple[int, int, int]) -> str:
    """
    Get the nearest CSS3 color name for an RGB tuple.
    Uses caching to avoid repeated calculations for the same colors.
    """
    try:
        return webcolors.rgb_to_name(rgb_tuple).capitalize()
    except ValueError:
        r, g, b = rgb_tuple
        deltas = ((CSS3_RGB - [r, g, b]) ** 2).sum(axis=1)
        closest_index = np.argmin(deltas)
        return CSS3_NAMES[closest_index].capitalize()
    
     
def build_color_name_map(image_np):
    height, width = image_np.shape[:2]
    color_name_map = np.empty((height, width), dtype=object)

    css3_names = list(webcolors._definitions._CSS3_NAMES_TO_HEX.keys())
    css3_rgb = np.array([webcolors.hex_to_rgb(webcolors._definitions._CSS3_NAMES_TO_HEX[name]) for name in css3_names])

    for y in range(height):
        for x in range(width):
            r1, g1, b1 = image_np[y, x]
            deltas = ((css3_rgb - [r1, g1, b1]) ** 2).sum(axis=1)
            closest_index = np.argmin(deltas)
            color_name_map[y, x] = css3_names[closest_index].capitalize()

    return color_name_map 
    
def inspect_colors(image: Image.Image):
    image_np = np.array(image.convert("RGB"))
    color_name_map = build_color_name_map(image_np)

    display_bgr = cv2.cvtColor(image_np, cv2.COLOR_RGB2BGR)
    original_bgr = display_bgr.copy()

    def on_mouse(event, x, y, flags, param):
        if 0 <= x < image_np.shape[1] and 0 <= y < image_np.shape[0]:
            if event == cv2.EVENT_MOUSEMOVE:
                name = color_name_map[y, x]
                display = original_bgr.copy()
                cv2.circle(display, (x, y), 5, (0, 0, 0), 2)
                
                text_position = (x + 10, y - 10)
                
                # Sombra blanca (texto más grueso)
                cv2.putText(display, f"{name}", text_position,
                            cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 4, cv2.LINE_AA)
                
                # Texto negro (texto más fino)
                cv2.putText(display, f"{name}", text_position,
                            cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 0), 2, cv2.LINE_AA)

                cv2.imshow("Color Inspector", display)

    cv2.namedWindow("Color Inspector")
    cv2.setMouseCallback("Color Inspector", on_mouse)
    cv2.imshow("Color Inspector", display_bgr)
    while True:
        if cv2.getWindowProperty("Color Inspector", cv2.WND_PROP_VISIBLE) < 1:
            break
        key = cv2.waitKey(1)
        if key == 27: 
            break
    cv2.destroyAllWindows()