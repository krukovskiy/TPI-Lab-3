import cv2
import numpy as np
import webcolors
from image_loader import load_image

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

def run_color_inspector():
    rgb_image, _ = load_image()
    image_np = np.array(rgb_image)
    display_bgr = cv2.cvtColor(image_np, cv2.COLOR_RGB2BGR)
    original_bgr = display_bgr.copy()

    color_name_map = build_color_name_map(image_np)

    def on_mouse(event, x, y, flags, param):
        nonlocal display_bgr
        if event == cv2.EVENT_MOUSEMOVE and 0 <= x < image_np.shape[1] and 0 <= y < image_np.shape[0]:
            rgb = tuple(int(c) for c in image_np[y, x])
            name = color_name_map[y, x]
            display_bgr = original_bgr.copy()
            cv2.circle(display_bgr, (x, y), 5, (0, 0, 0), 2)
            cv2.putText(display_bgr, f"{name}", (x + 10, y - 10),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 0), 2)

    cv2.namedWindow("Color Inspector")
    cv2.setMouseCallback("Color Inspector", on_mouse)

    while True:
        if cv2.getWindowProperty("Color Inspector", cv2.WND_PROP_VISIBLE) < 1:
            break
        cv2.imshow("Color Inspector", display_bgr)
        cv2.waitKey(1) 
    cv2.destroyAllWindows()