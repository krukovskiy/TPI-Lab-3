import numpy as np
import webcolors
from functools import lru_cache
from typing import Tuple

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
    
"""     
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

    return color_name_map """