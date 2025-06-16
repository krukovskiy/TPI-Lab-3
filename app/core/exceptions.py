"""Custom exceptions and handlers"""

from fastapi import status, Request
from fastapi.responses import JSONResponse

class APIException(Exception):
    """Base API exception"""
    def __init__(self, message: str, status_code: int = 400):
        self.message = message
        self.status_code = status_code

async def api_exception_handler(request: Request, exc: APIException):
    return JSONResponse(
                            status_code=exc.status_code,
                            content={"detail": exc.message}
                        )

class FileProcessingError(APIException):
    """File processing related errors"""
    def __init__(self, message: str, details: dict = None):
        super().__init__(message, status.HTTP_422_UNPROCESSABLE_ENTITY, details)


class ValidationError(APIException):
    """Validation related errors"""
    def __init__(self, message: str, details: dict = None):
        super().__init__(message, status.HTTP_400_BAD_REQUEST, details)