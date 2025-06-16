"""Application configuration management"""

from functools import lru_cache
from typing import List
from pydantic import Field, field_validator
from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    """Application settings"""

    # API Configuration
    api_keys: List[str] = Field(default_factory=list, description="Valid API keys")
    environment: str = Field(default="production", description="Environment (development/production)")

    # Server Configuration
    host: str = Field(default="0.0.0.0", description="Host to bind to")
    port: int = Field(default=8000, description="Port to bind to")

    # Security
    allowed_origins: List[str] = Field(default_factory=lambda: ["*"], description="CORS allowed origins")
    allowed_hosts: List[str] = Field(default_factory=lambda: ["*"], description="Trusted hosts")

    # File handling
    upload_dir: str = Field(default="uploads", description="Upload directory")
    output_dir: str = Field(default="outputs", description="Output directory")
    max_file_size: int = Field(default=10485760, description="Max file size in bytes (10MB)")
    static_dir: str = Field(default="static", description="Static files directory")
    allowed_extensions: List[str] = Field(
                                            default_factory=lambda: [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".heic"],
                                            description="Allowed file extensions"
                                        )

    class Config:
        env_file = ".env"
        case_sensitive = False

    # Validators for comma-separated lists
    @field_validator("api_keys", "allowed_origins", "allowed_hosts", "allowed_extensions", mode='before')
    def split_comma_separated(cls, v):
        if isinstance(v, str):
            return [item.strip() for item in v.split(",") if item.strip()]
        if v is None:
            return []
        return v

    def validate_api_key(self, api_key: str) -> bool:
        """Validate API key"""
        return api_key in self.api_keys if self.api_keys else True


@lru_cache()
def get_settings() -> Settings:
    """Cached settings instance"""
    return Settings()
