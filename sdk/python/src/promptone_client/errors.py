"""Failures callers can distinguish without importing the inference engine."""
from typing import Any


class PromptOneError(Exception):
    """Base class for client errors."""


class PromptOneValidationError(PromptOneError, ValueError):
    """An invalid client setting or request, rejected before sending it."""


class PromptOneAPIError(PromptOneError):
    """A non-2xx HTTP response; detail preserves the server's JSON value."""

    def __init__(self, status_code: int, detail: Any = None):
        super().__init__("PromptOne returned HTTP %d" % status_code)
        self.status_code = status_code
        self.detail = detail


class PromptOneConnectionError(PromptOneError):
    """The request or response could not be transported."""


class PromptOneTimeoutError(PromptOneConnectionError):
    """A blocking I/O operation timed out; server inference may still finish."""


class PromptOneResponseError(PromptOneError):
    """A successful HTTP response did not satisfy the prediction contract."""
