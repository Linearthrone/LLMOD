"""Configuration management for House Victoria MCP Server."""

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

from dotenv import load_dotenv

# Load environment variables
load_dotenv()


@dataclass
class ServerConfig:
    """Server configuration settings."""

    host: str = field(default_factory=lambda: os.getenv("SERVER_HOST", "localhost"))
    port: int = field(default_factory=lambda: int(os.getenv("SERVER_PORT", "8080")))

    # Database settings
    database_path: str = field(
        default_factory=lambda: os.getenv(
            "DATABASE_PATH", str(Path(__file__).parent.parent / "data" / "memory.db")
        )
    )
    # Optional bridge to the WPF app database (Data/HouseVictoria.db)
    app_database_path: str = field(
        default_factory=lambda: os.getenv(
            "APP_DATABASE_PATH", str(Path(__file__).parent.parent.parent / "Data" / "HouseVictoria.db")
        )
    )

    # Data banks settings
    data_banks_path: str = field(
        default_factory=lambda: os.getenv(
            "DATA_BANKS_PATH", str(Path(__file__).parent.parent / "data" / "banks")
        )
    )
    projects_path: str = field(
        default_factory=lambda: os.getenv(
            "PROJECTS_PATH", str(Path(__file__).parent.parent / "data" / "projects")
        )
    )

    # Logging settings
    log_level: str = field(default_factory=lambda: os.getenv("LOG_LEVEL", "INFO"))
    log_file: str = field(
        default_factory=lambda: os.getenv(
            "LOG_FILE", str(Path(__file__).parent.parent / "logs" / "server.log")
        )
    )

    # Memory settings
    memory_max_entries: int = field(default_factory=lambda: int(os.getenv("MEMORY_MAX_ENTRIES", "10000")))
    memory_retention_days: int = field(
        default_factory=lambda: int(os.getenv("MEMORY_RETENTION_DAYS", "365"))
    )

    # TT (Task & Workflow) settings
    task_timeout_seconds: int = field(
        default_factory=lambda: int(os.getenv("TASK_TIMEOUT_SECONDS", "3600"))
    )
    workflow_max_concurrent: int = field(
        default_factory=lambda: int(os.getenv("WORKFLOW_MAX_CONCURRENT", "10"))
    )

    def __post_init__(self):
        """Ensure directories exist after initialization."""
        # Create necessary directories
        for path_key in ["database_path", "app_database_path", "data_banks_path", "projects_path", "log_file"]:
            path_value = getattr(self, path_key)
            if path_key == "database_path":
                Path(path_value).parent.mkdir(parents=True, exist_ok=True)
            elif path_key == "app_database_path":
                Path(path_value).parent.mkdir(parents=True, exist_ok=True)
            elif path_key == "log_file":
                Path(path_value).parent.mkdir(parents=True, exist_ok=True)
            else:
                Path(path_value).mkdir(parents=True, exist_ok=True)


# Global configuration instance
config = ServerConfig()


def get_config() -> ServerConfig:
    """Get the global configuration instance.

    Returns:
        ServerConfig: The global configuration instance.
    """
    return config


def update_config(**kwargs) -> None:
    """Update configuration values.

    Args:
        **kwargs: Configuration key-value pairs to update.
    """
    for key, value in kwargs.items():
        if hasattr(config, key):
            setattr(config, key, value)
