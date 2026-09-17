#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = [
#     "python-dotenv",
# ]
# ///

"""
User prompt submit hook for apple-store.

Adapted from corjl-webapp/.claude/hooks/user_prompt_submit.py: the original
detected which Vue app (auth/designer/enduser/demo) and which package
(core/editor/extensions/plugins) a prompt likely touched. This repo has one web
app and four projects instead, so detection is rewritten against
docs/roadmap.md's milestones (M1-M8) and this repo's actual project layout
(Domain/Infrastructure/Web/Tests). Branch-type detection and the TDD-reminder
injection are unchanged, generic logic.

Features:
- Log all user prompts
- Inject context about current git branch
- Detect which roadmap milestone and project layer a prompt likely touches
"""

import argparse
import json
import os
import sys
import subprocess
import re
from pathlib import Path
from datetime import datetime
from utils.constants import ensure_session_log_dir

try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass


def get_git_branch():
    """Get current git branch name."""
    try:
        result = subprocess.run(
            ['git', 'branch', '--show-current'],
            capture_output=True,
            text=True,
            timeout=5
        )
        if result.returncode == 0:
            return result.stdout.strip()
    except Exception:
        pass
    return None


def get_git_status_summary():
    """Get a brief git status summary."""
    try:
        result = subprocess.run(
            ['git', 'status', '--porcelain'],
            capture_output=True,
            text=True,
            timeout=5
        )
        if result.returncode == 0:
            lines = result.stdout.strip().split('\n')
            if lines and lines[0]:
                modified = len([l for l in lines if l.startswith(' M') or l.startswith('M ')])
                added = len([l for l in lines if l.startswith('A ') or l.startswith('??')])
                deleted = len([l for l in lines if l.startswith(' D') or l.startswith('D ')])
                return {'modified': modified, 'added': added, 'deleted': deleted, 'total': len(lines)}
    except Exception:
        pass
    return None


def detect_affected_milestones(prompt):
    """
    Detect which docs/roadmap.md milestone(s) a prompt likely touches.
    Returns list of milestone labels.
    """
    milestones = []
    prompt_lower = prompt.lower()

    milestone_keywords = {
        'M1 auth/OTP': ['login', 'log in', 'register', 'otp', 'password', 'auth', 'sign up'],
        'M2 catalog': ['search', 'filter', 'compare', 'category', 'variant', 'attribute', 'catalog'],
        'M3 cart/checkout': ['cart', 'checkout', 'voucher', 'place order'],
        'M4 payment': ['vnpay', 'momo', 'payment', 'cod', 'gateway'],
        'M5 orders/shipping': ['order status', 'tracking', 'shipment', 'shipping', 'confirm order'],
        'M6 stock/pricing': ['stock', 'inventory', 'intake', 'promotion', 'price update'],
        'M7 admin/reports': ['report', 'export', 'revenue', 'admin', 'dashboard'],
        'M8 reviews': ['review', 'rating', 'comment'],
    }

    for milestone, keywords in milestone_keywords.items():
        for keyword in keywords:
            if keyword in prompt_lower:
                if milestone not in milestones:
                    milestones.append(milestone)
                break

    return milestones


def detect_affected_layers(prompt):
    """
    Detect which project layer a prompt likely touches.
    Returns list of project names.
    """
    layers = []
    prompt_lower = prompt.lower()

    layer_keywords = {
        'AppleStore.Domain': ['entity', 'enum', 'domain model'],
        'AppleStore.Infrastructure': ['dbcontext', 'ef core', 'migration', 'entity framework', 'configuration class'],
        'AppleStore.Web': ['controller', 'view', 'razor', 'cshtml', 'route', 'endpoint'],
        'AppleStore.Tests': ['test', 'xunit', 'fixture'],
    }

    for layer, keywords in layer_keywords.items():
        for keyword in keywords:
            if keyword in prompt_lower:
                if layer not in layers:
                    layers.append(layer)
                break

    return layers


def generate_context_message(branch, git_status, affected_milestones, affected_layers):
    """
    Generate context message to inject into the prompt.
    Printed to stdout - will be added to the prompt.
    """
    context_parts = []

    # Branch context
    if branch:
        branch_type = None
        if branch.startswith('feat/'):
            branch_type = 'feature'
        elif branch.startswith('fix/'):
            branch_type = 'bug fix'
        elif branch.startswith('chore/'):
            branch_type = 'chore'
        elif branch.startswith('refactor/'):
            branch_type = 'refactoring'

        if branch_type:
            context_parts.append(f"Working on {branch_type} branch: {branch}")

    # Affected milestones/layers
    if affected_milestones:
        context_parts.append(f"Likely roadmap milestone(s): {', '.join(affected_milestones)}")
    if affected_layers:
        context_parts.append(f"Likely affected project(s): {', '.join(affected_layers)}")

    # Git status
    if git_status and git_status.get('total', 0) > 0:
        context_parts.append(f"Uncommitted changes: {git_status['modified']} modified, {git_status['added']} added")

    return '\n'.join(context_parts) if context_parts else None


def log_user_prompt(session_id, input_data, context_data=None):
    """Log user prompt to session directory."""
    log_dir = ensure_session_log_dir(session_id)
    log_file = log_dir / 'user_prompt_submit.json'

    if log_file.exists():
        with open(log_file, 'r') as f:
            try:
                log_data = json.load(f)
            except (json.JSONDecodeError, ValueError):
                log_data = []
    else:
        log_data = []

    # Add context data if available
    if context_data:
        input_data['_context'] = context_data

    log_data.append(input_data)

    with open(log_file, 'w') as f:
        json.dump(log_data, f, indent=2)


def validate_prompt(prompt):
    """
    Validate the user prompt for security or policy violations.
    Returns tuple (is_valid, reason).
    """
    blocked_patterns = [
        # Add any patterns you want to block
    ]

    prompt_lower = prompt.lower()

    for pattern, reason in blocked_patterns:
        if pattern.lower() in prompt_lower:
            return False, reason

    return True, None


def main():
    try:
        parser = argparse.ArgumentParser()
        parser.add_argument('--validate', action='store_true',
                          help='Enable prompt validation')
        parser.add_argument('--log-only', action='store_true',
                          help='Only log prompts, no context injection')
        parser.add_argument('--inject-context', action='store_true',
                          help='Inject context information into prompt')
        args = parser.parse_args()

        input_data = json.loads(sys.stdin.read())

        session_id = input_data.get('session_id', 'unknown')
        prompt = input_data.get('prompt', '')

        # Gather context
        context_data = {
            'timestamp': datetime.now().isoformat(),
            'branch': get_git_branch(),
            'git_status': get_git_status_summary(),
            'affected_milestones': detect_affected_milestones(prompt),
            'affected_layers': detect_affected_layers(prompt),
        }

        # Log the user prompt with context
        log_user_prompt(session_id, input_data, context_data)

        # Validate prompt if requested
        if args.validate and not args.log_only:
            is_valid, reason = validate_prompt(prompt)
            if not is_valid:
                print(f"Prompt blocked: {reason}", file=sys.stderr)
                sys.exit(2)

        # Inject context if requested (print to stdout)
        if args.inject_context and not args.log_only:
            context_message = generate_context_message(
                context_data['branch'],
                context_data['git_status'],
                context_data['affected_milestones'],
                context_data['affected_layers']
            )
            if context_message:
                # This will be appended to the prompt
                print(f"\n<user-prompt-submit-hook>\n{context_message}\n</user-prompt-submit-hook>")

        sys.exit(0)

    except json.JSONDecodeError:
        sys.exit(0)
    except Exception:
        sys.exit(0)


if __name__ == '__main__':
    main()
