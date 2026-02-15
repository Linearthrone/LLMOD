"""Logging utilities for House Victoria MCP Server."""

import logging
import sys
from pathlib import Path
from typing import Optional

from .config import get_config


def setup_logger(
    name: str = "house_victoria_mcp",
    log_file: Optional[str] = None,
    log_level: Optional[str] = None,
) -> logging.Logger:
    """Set up logger for the application.

    Args:
        name: Logger name.
        log_file: Path to log file. If None, uses config.
        log_level: Log level. If None, uses config.

    Returns:
        Configured logger instance.
    """
    config_obj = get_config()
    
    logger = logging.getLogger(name)
    logger.setLevel(getattr(logging, (log_level or config_obj.log_level).upper()))
    
    # Clear existing handlers
    logger.handlers.clear()
    
    # Console handler (stderr only for MCP compatibility)
    console_handler = logging.StreamHandler(sys.stderr)
    console_handler.setLevel(logger.level)
    console_formatter = logging.Formatter(
        "%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    console_handler.setFormatter(console_formatter)
    logger.addHandler(console_handler)
    
    # File handler
    file_path = Path(log_file or config_obj.log_file)
    file_path.parent.mkdir(parents=True, exist_ok=True)
    
    file_handler = logging.FileHandler(file_path, encoding="utf-8")
    file_handler.setLevel(logger.level)
    file_formatter = logging.Formatter(
        "%(asctime)s - %(name)s - %(levelname)s - %(funcName)s:%(lineno)d - %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    file_handler.setFormatter(file_formatter)
    logger.addHandler(file_handler)
    
    return logger


# Global logger instance
logger = setup_logger()


def get_logger(name: Optional[str] = None) -> logging.Logger:
    """Get a logger instance.

    Args:
        name: Logger name. If None, returns the default logger.

    Returns:
        Logger instance.
    """
    if name:
        return logging.getLogger(f"house_victoria_mcp.{name}")
    return logger
