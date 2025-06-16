"""
Color inspection API endpoints
"""

from PIL import Image
import numpy as np
import io

from app.services.color_inspector import get_nearest_color_name
from fastapi import APIRouter, UploadFile, File, Depends
from app.core.security import verify_api_key
from app.core.exceptions import FileProcessingError, ValidationError
from app.config import get_settings

router = APIRouter()

@router.post("/inspect")
async def inspect_colors(
    x: int,
    y: int,
    file: UploadFile = File(...),
    api_key: str = Depends(verify_api_key)
):
    settings = get_settings()
    
    # Validate file
    if not file.filename.lower().endswith(tuple(settings.allowed_extensions)):
        raise ValidationError("Invalid file format")
    
    try:
        # Read image
        contents = await file.read()
        image = Image.open(io.BytesIO(contents))
        image_np = np.array(image.convert("RGB"))
        
        # Check coordinates
        if not (0 <= x < image_np.shape[1] and 0 <= y < image_np.shape[0]):
            raise ValidationError("Coordinates out of bounds")
        
        # Get color information for the specific pixel only
        r, g, b = image_np[y, x]
        
        # Find closest color name for this single pixel
        color_name = get_nearest_color_name((int(r), int(g), int(b)))
        
        return {
            "coordinates": {"x": x, "y": y},
            "rgb": {"r": int(r), "g": int(g), "b": int(b)},
            "hex": f"#{r:02x}{g:02x}{b:02x}",
            "color_name": color_name
        }
        
    except Exception as e:
        raise FileProcessingError(f"Failed to inspect color: {str(e)}")


""" @router.post("/analyze")
async def analyze_image_colors(
                                file: UploadFile = File(...),
                                api_key: str = Depends(verify_api_key)
                                ):
    settings = get_settings()
    
    # Validate file
    if not file.filename.lower().endswith(tuple(settings.allowed_extensions)):
        raise ValidationError("Invalid file format")
    
    try:
        # Read image
        contents = await file.read()
        image = Image.open(io.BytesIO(contents))
        image_np = np.array(image.convert("RGB"))
        
        # Simple color analysis - get most common colors
        pixels = image_np.reshape(-1, 3)
        unique_colors, counts = np.unique(pixels, axis=0, return_counts=True)
        
        # Get top 10 colors
        top_indices = np.argsort(counts)[-10:][::-1]
        dominant_colors = []
        
        for idx in top_indices:
            color = unique_colors[idx]
            count = counts[idx]
            percentage = (count / len(pixels)) * 100
            
            dominant_colors.append({
                                        "rgb": {"r": int(color[0]), "g": int(color[1]), "b": int(color[2])},
                                        "hex": f"#{color[0]:02x}{color[1]:02x}{color[2]:02x}",
                                        "percentage": round(percentage, 2)
                                    })
        
        return {
                    "image_size": {"width": image_np.shape[1], "height": image_np.shape[0]},
                    "dominant_colors": dominant_colors
                }
        
    except Exception as e:
        raise FileProcessingError(f"Failed to analyze colors: {str(e)}") """