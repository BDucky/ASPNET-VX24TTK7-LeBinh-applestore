#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.8"
# ///

"""
Constants for Claude Code Hooks.
Logs are stored in .ai/logs/ directory.

Session folders use format: YYMMDD-HHMMSS-<short_id>
Example: 251225-143056-c36b742a

Ported from corjl-webapp/.claude/hooks/utils/constants.py unchanged: this module
has no Vue/pnpm-specific logic, it only maps a Claude Code session_id to a log
folder under CLAUDE_PROJECT_DIR.
"""

import json
import os
from datetime import datetime
from pathlib import Path

# Base directory for all logs - using .ai/logs/ directory in project root
# Use CLAUDE_PROJECT_DIR to ensure logs go to project root, not current working directory
_project_dir = os.environ.get("CLAUDE_PROJECT_DIR", ".")
LOG_BASE_DIR = os.environ.get("CLAUDE_HOOKS_LOG_DIR", os.path.join(_project_dir, ".ai/logs"))

# Session index file to map session_id to folder name
SESSION_INDEX_FILE = Path(LOG_BASE_DIR) / "session_index.json"


def _load_session_index() -> dict:
    """Load session index from file."""
    if SESSION_INDEX_FILE.exists():
        try:
            with open(SESSION_INDEX_FILE, 'r') as f:
                return json.load(f)
        except (json.JSONDecodeError, ValueError):
            return {}
    return {}


def _save_session_index(index: dict) -> None:
    """Save session index to file."""
    SESSION_INDEX_FILE.parent.mkdir(parents=True, exist_ok=True)
    with open(SESSION_INDEX_FILE, 'w') as f:
        json.dump(index, f, indent=2)


def _generate_session_folder_name(session_id: str) -> str:
    """
    Generate a human-readable folder name for a session.
    Format: YYMMDD-HHMMSS-<short_id>
    Example: 251225-143056-c36b742a
    """
    timestamp = datetime.now().strftime("%y%m%d-%H%M%S")
    short_id = session_id.split('-')[0] if '-' in session_id else session_id[:8]
    return f"{timestamp}-{short_id}"


def get_session_log_dir(session_id: str) -> Path:
    """
    Get the log directory for a specific session.

    Args:
        session_id: The Claude session ID

    Returns:
        Path object for the session's log directory
    """
    index = _load_session_index()

    if session_id in index:
        return Path(LOG_BASE_DIR) / index[session_id]

    # Session not found, return None (caller should use ensure_session_log_dir)
    return Path(LOG_BASE_DIR) / session_id


def ensure_session_log_dir(session_id: str) -> Path:
    """
    Ensure the log directory for a session exists.
    Creates folder with format: YYMMDD-HHMMSS-<short_id>

    Args:
        session_id: The Claude session ID

    Returns:
        Path object for the session's log directory
    """
    index = _load_session_index()

    # Check if session already has a folder
    if session_id in index:
        folder_name = index[session_id]
        log_dir = Path(LOG_BASE_DIR) / folder_name
        log_dir.mkdir(parents=True, exist_ok=True)
        return log_dir

    # Create new folder with timestamp prefix
    folder_name = _generate_session_folder_name(session_id)
    log_dir = Path(LOG_BASE_DIR) / folder_name

    # Handle rare case of duplicate folder names (same second)
    if log_dir.exists():
        # Add more uniqueness from session_id
        short_id = session_id[:12].replace('-', '')
        folder_name = f"{datetime.now().strftime('%y%m%d-%H%M%S')}-{short_id}"
        log_dir = Path(LOG_BASE_DIR) / folder_name

    log_dir.mkdir(parents=True, exist_ok=True)

    # Save mapping
    index[session_id] = folder_name
    _save_session_index(index)

    return log_dir
