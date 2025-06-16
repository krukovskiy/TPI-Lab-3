"""Security utilities and authentication"""

from fastapi import HTTPException, Security, status
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials

from app.config import get_settings

security = HTTPBearer()


async def verify_api_key(
                         credentials: HTTPAuthorizationCredentials = Security(security)
                        ) -> str:
    """Verify API key from Authorization header"""
    settings = get_settings()
    
    if not credentials:
        raise HTTPException(
                                status_code=status.HTTP_401_UNAUTHORIZED,
                                detail="Missing API key"
                            )
    
    api_key = credentials.credentials
    
    if not settings.validate_api_key(api_key):
        raise HTTPException(
                                status_code=status.HTTP_401_UNAUTHORIZED,
                                detail="Invalid API key"
                            )
    
    return api_key