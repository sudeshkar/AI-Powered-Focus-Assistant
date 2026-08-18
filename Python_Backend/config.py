"""Environment-driven settings for the Flask backend.

Values come from the repository-root .env (see .env.example). Previously host,
port and debug were hard-coded in app.py and the .env file was never read.
"""
import os

from dotenv import load_dotenv

BACKEND_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(BACKEND_DIR)

# Repository-root .env, with a Python_Backend/.env override for local tweaks.
load_dotenv(os.path.join(REPO_ROOT, ".env"))
load_dotenv(os.path.join(BACKEND_DIR, ".env"), override=True)

_TRUTHY = {"1", "true", "yes", "on"}


def _flag(name: str, default: bool = False) -> bool:
    raw = os.getenv(name)
    if raw is None:
        return default
    return raw.strip().lower() in _TRUTHY


def _int(name: str, default: int) -> int:
    raw = os.getenv(name)
    if raw is None or not raw.strip():
        return default
    try:
        return int(raw)
    except ValueError:
        print(f"Ignoring non-numeric {name}={raw!r}; using {default}")
        return default


HOST = os.getenv("FLASK_HOST", "127.0.0.1").strip() or "127.0.0.1"
PORT = _int("FLASK_PORT", 5000)

# Off by default: the reloader forks a second process the WPF client cannot stop,
# and the Werkzeug debugger exposes an interactive console.
DEBUG = _flag("FLASK_DEBUG", False)

_model_dir = os.getenv("MODEL_DIR", "models").strip() or "models"
MODEL_DIR = _model_dir if os.path.isabs(_model_dir) else os.path.join(BACKEND_DIR, _model_dir)

MODEL_SAVE_INTERVAL = max(1, _int("MODEL_SAVE_INTERVAL", 10))
MODEL_BACKUP_LIMIT = max(0, _int("MODEL_BACKUP_LIMIT", 5))
