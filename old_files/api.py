from fastapi import FastAPI, UploadFile, File
from fastapi.responses import FileResponse, JSONResponse
import shutil
import os
import numpy as np
import cv2
from screentone_processor import ScreentoneProcessor
from image_loader import load_image_from_path
from color_inspector import inspect_colors 
from PIL import Image

app = FastAPI()

UPLOAD_DIR = "uploads"
OUTPUT_DIR = "outputs"

os.makedirs(UPLOAD_DIR, exist_ok=True)
os.makedirs(OUTPUT_DIR, exist_ok=True)

@app.post("/apply_screentone/")
async def apply_screentone(file: UploadFile = File(...)):
    input_path = os.path.join(UPLOAD_DIR, file.filename)
    with open(input_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)
    image = load_image_from_path(input_path)
    processor = ScreentoneProcessor(image_shape=image.size)
    rgb_image = np.array(image.convert("RGB"))
    hsv_image = cv2.cvtColor(rgb_image, cv2.COLOR_RGB2HSV)
    processed_image = processor.apply(hsv_image, rgb_image)
    output_path = os.path.join(OUTPUT_DIR, f"screentone_{file.filename}")
    Image.fromarray(processed_image).save(output_path)
    return FileResponse(output_path, media_type="image/png", filename=f"screentone_{file.filename}")

@app.post("/inspect_colors/")
async def inspect_colors_api(file: UploadFile = File(...)):
    input_path = os.path.join(UPLOAD_DIR, file.filename)
    with open(input_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)
    image = load_image_from_path(input_path)
    result = inspect_colors(image)  # Esta función debe devolver un dict o lista serializable
    return JSONResponse(content=result)