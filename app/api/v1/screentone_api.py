"""
Screentone processing API endpoints
"""
from fastapi import APIRouter, UploadFile, File, Depends
from fastapi.responses import FileResponse
from PIL import Image
import numpy as np
import cv2
import tempfile
import io

from app.core.security import verify_api_key
from app.core.exceptions import FileProcessingError, ValidationError
from app.config import get_settings
from app.services.screentone import ScreentoneProcessor

router = APIRouter()

@router.post("/process")
async def process_screentone(
                                file: UploadFile = File(...),
                                api_key: str = Depends(verify_api_key)
                            ):
    """Process image with screentone patterns"""
    settings = get_settings()

    # Validate file
    if not file.filename.lower().endswith(tuple(settings.allowed_extensions)):
        raise ValidationError("Invalid file format")
    
    if file.size > settings.max_file_size:
        raise ValidationError("File too large")
    
    try:
        # Read and process image
        contents = await file.read()
        image = Image.open(io.BytesIO(contents))
        
        # Convert to numpy arrays
        rgb_image = np.array(image.convert("RGB"))
        hsv_image = cv2.cvtColor(rgb_image, cv2.COLOR_RGB2HSV)
        
        # Process with screentone
        processor = ScreentoneProcessor(rgb_image.shape)
        screentone_result = processor.apply(hsv_image, rgb_image)
        superimposed_result = processor.superimpose(rgb_image, screentone_result)
        
        # Save result
        with tempfile.NamedTemporaryFile(delete=False, suffix='.jpg') as tmp_file:
            result_image = Image.fromarray(superimposed_result)
            result_image.save(tmp_file.name, 'JPEG')
            
            return FileResponse(
                                    tmp_file.name,
                                    media_type='image/jpeg',
                                    filename=f"screentone_{file.filename}"
                                )
            
    except Exception as e:
        raise FileProcessingError(f"Failed to process image: {str(e)}")