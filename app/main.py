"""
Color Blindness Assistance API
A FastAPI application providing screentone processing and color inspection services.
"""
import os
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.security import HTTPBearer
from fastapi.middleware.cors import CORSMiddleware
from fastapi.middleware.trustedhost import TrustedHostMiddleware
from fastapi.staticfiles import StaticFiles
import uvicorn

from app.config import get_settings
from app.api.v1 import screentone_api, color_inspector_api
from app.core.exceptions import APIException, api_exception_handler

# Security
security = HTTPBearer()

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan events"""
    settings = get_settings()
    os.makedirs(settings.upload_dir, exist_ok=True)
    os.makedirs(settings.output_dir, exist_ok=True)
    yield

def create_app() -> FastAPI:
    """Application factory"""
    settings = get_settings()
    
    app = FastAPI(
                    title="Color Blindness Assistance API",
                    description="API for screentone processing and color inspection to assist color blind users",
                    version="1.0.0",
                    docs_url="/docs" if settings.environment == "development" else None,
                    redoc_url="/redoc" if settings.environment == "development" else None,
                    lifespan=lifespan
                )
    
    # Middleware
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.allowed_origins,
        allow_credentials=True,
        allow_methods=["GET", "POST", "PUT", "DELETE"],
        allow_headers=["*"],
    )
    
    app.add_middleware(
        TrustedHostMiddleware,
        allowed_hosts=settings.allowed_hosts
    )
    
    # Exception handlers
    app.add_exception_handler(APIException, api_exception_handler)
    
    # Routers
    app.include_router(screentone_api.router, prefix="/api/v1/screentone", tags=["screentone"])
    app.include_router(color_inspector_api.router, prefix="/api/v1/colors", tags=["colors"])
    
    # Static files
    app.mount("/static", StaticFiles(directory=settings.static_dir), name="static")

    return app

app = create_app()

if __name__ == "__main__":
    settings = get_settings()
    uvicorn.run(
        "app.main:app",
        host=settings.host,
        port=settings.port,
        reload=settings.environment == "development"
    )