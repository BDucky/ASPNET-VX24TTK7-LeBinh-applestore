#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = [
#     "python-dotenv",
# ]
# ///

"""
Notification hook for apple-store.

Ported unchanged from corjl-webapp/.claude/hooks/notification.py: this hook has
no Vue/pnpm-specific logic, it only sends macOS notifications.

Features:
- macOS native notifications
- Sound alerts for long-running tasks
- Customizable notification messages
"""

import argparse
import json
import os
import sys
import subprocess
import platform
from pathlib import Path

from utils.constants import ensure_session_log_dir

try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass


def send_macos_notification(title, message, sound=True):
    """
    Send a macOS notification using osascript.
    """
    if platform.system() != 'Darwin':
        return False

    try:
        sound_option = 'with sound name "Glass"' if sound else ''
        script = f'''
        display notification "{message}" with title "{title}" {sound_option}
        '''
        subprocess.run(
            ['osascript', '-e', script],
            capture_output=True,
            timeout=5
        )
        return True
    except Exception:
        return False


def send_terminal_bell():
    """Send terminal bell (audio alert)."""
    try:
        print('\a', end='', file=sys.stderr)
        return True
    except Exception:
        return False


def format_notification_message(input_data):
    """
    Format notification message based on input data.
    Returns tuple (title, message).
    """
    # Default message
    title = "Claude Code"
    message = "Task completed"

    # Try to extract meaningful info from input
    if isinstance(input_data, dict):
        # Check for specific notification types
        notification_type = input_data.get('type', '')

        if notification_type == 'task_complete':
            title = "Task Complete"
            message = input_data.get('message', 'Your task has finished')

        elif notification_type == 'error':
            title = "Error"
            message = input_data.get('message', 'An error occurred')

        elif notification_type == 'waiting':
            title = "Waiting for Input"
            message = input_data.get('message', 'Claude is waiting for your response')

        else:
            # Generic notification
            if 'message' in input_data:
                message = input_data['message']

    return title, message


def log_notification(session_id, input_data, notification_sent=False):
    """Log notification to session directory."""
    log_dir = ensure_session_log_dir(session_id)
    log_file = log_dir / 'notification.json'

    if log_file.exists():
        with open(log_file, 'r') as f:
            try:
                log_data = json.load(f)
            except (json.JSONDecodeError, ValueError):
                log_data = []
    else:
        log_data = []

    # Add notification status
    input_data['_notification_sent'] = notification_sent

    log_data.append(input_data)

    with open(log_file, 'w') as f:
        json.dump(log_data, f, indent=2)


def main():
    try:
        parser = argparse.ArgumentParser()
        parser.add_argument('--notify', action='store_true',
                          help='Enable macOS notifications')
        parser.add_argument('--sound', action='store_true', default=True,
                          help='Play sound with notification')
        parser.add_argument('--bell', action='store_true',
                          help='Only play terminal bell')
        args = parser.parse_args()

        input_data = json.loads(sys.stdin.read())

        session_id = input_data.get('session_id', 'unknown')
        notification_sent = False

        # Send notification if enabled
        if args.notify:
            title, message = format_notification_message(input_data)

            if args.bell:
                # Just terminal bell
                notification_sent = send_terminal_bell()
            else:
                # macOS notification
                notification_sent = send_macos_notification(
                    title,
                    message,
                    sound=args.sound
                )

                # Fallback to terminal bell if notification failed
                if not notification_sent:
                    send_terminal_bell()

        # Log notification
        log_notification(session_id, input_data, notification_sent)

        sys.exit(0)

    except json.JSONDecodeError:
        sys.exit(0)
    except Exception:
        sys.exit(0)


if __name__ == '__main__':
    main()
