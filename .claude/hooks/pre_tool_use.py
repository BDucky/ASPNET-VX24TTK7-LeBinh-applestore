#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.8"
# ///

"""
Pre-tool-use hook for the apple-store ASP.NET Core solution.

Adapted from corjl-webapp/.claude/hooks/pre_tool_use.py. Dropped entirely (had no
C#/.NET equivalent worth building): the npm/yarn package-manager block (.NET has
no analogous package-manager ambiguity), the TypeScript `any`-type block (C#'s
`object`/`dynamic` are used legitimately far more often than TS `any`, so a
regex block would be noisy rather than useful), the cross-package relative-import
block (the C# compiler already enforces this through project references, since
AppleStore.Domain has zero references and cannot accidentally reach Infrastructure
or Web), and the Vue `<script setup>` / file-location checks (no Vue in this repo).

Kept, adapted: dangerous rm protection (directory names updated to this repo's
actual layout), protected-branch push blocking (unchanged, generic gh CLI logic),
sensitive-file warnings (unchanged, generic). Added: a block on generated EF Core
migration files being hand-edited (the direct analog of corjl's GraphQL
generated-file protection), and a block on `Console.WriteLine` in source files
(the direct analog of corjl's console.log block, matching this repo's
CLAUDE.md rule to use ILogger instead).

Protections:
- Block direct pushes to GitHub-protected branches (fetched dynamically)
- Block rm commands outside project
- Warn on dangerous rm patterns within project
- Protect EF Core generated migration designer/snapshot files from hand-editing
- Block Console.WriteLine in C# source files (use ILogger)
"""

import json
import sys
import re
import os
from pathlib import Path

from utils.constants import ensure_session_log_dir


def get_project_root():
    """Get the project root directory."""
    hook_path = Path(__file__).resolve()
    return hook_path.parent.parent.parent


def resolve_path(path_str, cwd=None):
    """Resolve a path string to an absolute path."""
    if cwd is None:
        cwd = Path.cwd()

    path_str = os.path.expanduser(path_str)
    path_str = os.path.expandvars(path_str)

    path = Path(path_str)
    if not path.is_absolute():
        path = cwd / path

    try:
        return path.resolve()
    except (OSError, ValueError):
        return path


def is_path_within_project(path_str, project_root, cwd=None):
    """Check if a given path is within the project directory."""
    resolved = resolve_path(path_str, cwd)
    try:
        resolved.relative_to(project_root)
        return True
    except ValueError:
        return False


def extract_paths_from_rm_command(command):
    """Extract file/directory paths from an rm command."""
    parts = command.split()
    paths = []

    for part in parts:
        if part == 'rm':
            continue
        if part.startswith('-'):
            continue
        paths.append(part)

    return paths


def check_dangerous_rm_within_project(command):
    """
    Check for dangerous rm patterns within project.
    Returns (is_dangerous, message) tuple.
    """
    normalized = ' '.join(command.split())

    # Critical directories that should never be deleted entirely
    critical_patterns = [
        (r'\brm\s+.*-r.*\s+src/?$', 'BLOCKED: Cannot delete entire src/ directory'),
        (r'\brm\s+.*-r.*\s+tests/?$', 'BLOCKED: Cannot delete entire tests/ directory'),
        (r'\brm\s+.*-r.*\s+\.claude/?$', 'BLOCKED: Cannot delete .claude/ directory'),
        (r'\brm\s+.*-r.*\s+docs/?$', 'BLOCKED: Cannot delete entire docs/ directory'),
    ]

    for pattern, message in critical_patterns:
        if re.search(pattern, normalized):
            return True, message

    # Warn patterns (not blocked, just logged)
    warn_patterns = [
        (r'\brm\s+.*-r.*\s+bin/?', 'WARNING: Deleting bin/ output - next build will regenerate it'),
        (r'\brm\s+.*-r.*\s+obj/?', 'WARNING: Deleting obj/ - next restore will regenerate it'),
        (r'\brm\s+.*-r.*\s+Migrations/?', 'WARNING: Deleting EF Core migrations - this loses schema history'),
    ]

    for pattern, message in warn_patterns:
        if re.search(pattern, normalized):
            # Log warning but don't block
            print(message, file=sys.stderr)
            return False, None

    return False, None


def is_dangerous_rm_command(command, cwd=None):
    """Block rm commands that target paths outside the project."""
    normalized = ' '.join(command.split())
    if not re.search(r'\brm\s+', normalized):
        return False, None

    project_root = get_project_root()
    paths = extract_paths_from_rm_command(normalized)

    if cwd is None:
        cwd = Path.cwd()

    for path_str in paths:
        if not path_str.strip():
            continue

        if not is_path_within_project(path_str, project_root, cwd):
            return True, f'BLOCKED: rm command targets path outside project: {path_str}'

    # Check for dangerous patterns within project
    return check_dangerous_rm_within_project(command)


def _get_current_branch():
    """Get the current git branch name."""
    import subprocess
    try:
        result = subprocess.run(
            ['git', 'branch', '--show-current'],
            capture_output=True, text=True, timeout=5
        )
        return result.stdout.strip()
    except Exception:
        return ''


def _get_protected_branches():
    """
    Fetch protected branches from GitHub via gh CLI.
    Checks both legacy branch protection (/branches) and modern rulesets (/rulesets).
    Results are cached for 24 hours in a temp file to avoid repeated API calls.
    """
    import subprocess
    import time
    import tempfile

    cache_file = Path(tempfile.gettempdir()) / 'apple_store_protected_branches.json'
    cache_ttl = 86400  # 24 hours

    # Check cache first
    if cache_file.exists():
        try:
            with open(cache_file, 'r') as f:
                cache = json.load(f)
            if time.time() - cache.get('timestamp', 0) < cache_ttl:
                return set(cache.get('branches', []))
        except (json.JSONDecodeError, ValueError, OSError):
            pass

    branches = set()

    # 1. Fetch legacy branch protection
    try:
        result = subprocess.run(
            ['gh', 'api', 'repos/{owner}/{repo}/branches', '--jq',
             '.[] | select(.protected == true) | .name'],
            capture_output=True, text=True, timeout=10
        )
        if result.returncode == 0:
            for b in result.stdout.strip().splitlines():
                if b.strip():
                    branches.add(b.strip())
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return set()

    # 2. Fetch modern rulesets — these can protect branches not caught by /branches
    try:
        # Get default branch name (for ~DEFAULT_BRANCH resolution)
        default_result = subprocess.run(
            ['gh', 'api', 'repos/{owner}/{repo}', '--jq', '.default_branch'],
            capture_output=True, text=True, timeout=10
        )
        default_branch = default_result.stdout.strip() if default_result.returncode == 0 else ''

        # Get all active rulesets
        rulesets_result = subprocess.run(
            ['gh', 'api', 'repos/{owner}/{repo}/rulesets', '--jq',
             '.[] | select(.enforcement == "active") | .id'],
            capture_output=True, text=True, timeout=10
        )
        if rulesets_result.returncode == 0:
            ruleset_ids = [r.strip() for r in rulesets_result.stdout.strip().splitlines() if r.strip()]
            for rid in ruleset_ids:
                detail_result = subprocess.run(
                    ['gh', 'api', f'repos/{{owner}}/{{repo}}/rulesets/{rid}',
                     '--jq', '.conditions.ref_name.include[]'],
                    capture_output=True, text=True, timeout=10
                )
                if detail_result.returncode == 0:
                    for ref in detail_result.stdout.strip().splitlines():
                        ref = ref.strip()
                        if ref == '~DEFAULT_BRANCH' and default_branch:
                            branches.add(default_branch)
                        elif ref.startswith('refs/heads/'):
                            branches.add(ref.removeprefix('refs/heads/'))
    except (FileNotFoundError, subprocess.TimeoutExpired) as e:
        print(f'WARNING: Ruleset fetch failed: {e}', file=sys.stderr)
    except Exception as e:
        print(f'WARNING: Unexpected error fetching rulesets: {e}', file=sys.stderr)

    # Write cache
    try:
        with open(cache_file, 'w') as f:
            json.dump({'timestamp': time.time(), 'branches': list(branches)}, f)
    except Exception:
        pass

    return branches


def check_git_commands(command):
    """
    Block dangerous git commands.
    Returns (is_blocked, message) tuple.
    """
    normalized = ' '.join(command.split())

    # Only fetch protected branches when a push command is detected
    if re.search(r'\bgit\s+push\b', normalized):
        protected_branches = _get_protected_branches()

        if protected_branches:
            block_msg = 'BLOCKED: Direct push to protected branch ({branch}) is not allowed. Push to a feature branch and create a PR instead.'
            # Build regex alternation from protected branch names
            branches_pattern = '|'.join(re.escape(b) for b in protected_branches)

            # Pattern: git push origin HEAD:main, git push origin branch:main
            refspec_match = re.search(
                r'git\s+push\s+\S+\s+\S+:(' + branches_pattern + r')\b',
                normalized, re.IGNORECASE
            )
            if refspec_match:
                return True, block_msg.format(branch=refspec_match.group(1))

            # Pattern: git push origin main (direct branch name as refspec)
            direct_match = re.search(
                r'git\s+push\s+\S+\s+(' + branches_pattern + r')\b',
                normalized, re.IGNORECASE
            )
            if direct_match:
                return True, block_msg.format(branch=direct_match.group(1))

            # Pattern: bare "git push" or "git push origin" — check current branch
            if re.match(r'git\s+push\s*$', normalized) or re.match(r'git\s+push\s+\S+\s*$', normalized):
                current_branch = _get_current_branch()
                if current_branch in protected_branches:
                    return True, block_msg.format(branch=current_branch)

    # Block hard reset (warn only, don't block)
    if re.search(r'git\s+reset\s+--hard', normalized):
        print('WARNING: git reset --hard detected - uncommitted changes will be lost', file=sys.stderr)

    return False, None


def check_protected_files(tool_name, tool_input):
    """
    Protect EF Core generated migration files from hand-editing.
    Returns (is_blocked, message) tuple.
    """
    protected_patterns = [
        (r'Migrations/.*\.Designer\.cs$', 'BLOCKED: Do not hand-edit EF Core migration designer files. Run "dotnet tool run dotnet-ef migrations add" or "remove" instead'),
        (r'Migrations/AppDbContextModelSnapshot\.cs$', 'BLOCKED: Do not hand-edit the EF Core model snapshot. It is regenerated by "dotnet ef migrations add"'),
    ]

    # Check file paths in Write/Edit tools
    if tool_name in ['Write', 'Edit']:
        file_path = tool_input.get('file_path', '')
        for pattern, message in protected_patterns:
            if re.search(pattern, file_path):
                return True, message

    return False, None


def check_env_files(tool_name, tool_input):
    """
    Warn when modifying environment/credential files.
    Returns warning message or None.
    """
    sensitive_patterns = [
        r'\.env',
        r'credentials',
        r'secrets',
        r'\.pem$',
        r'\.key$',
        r'appsettings\.Production\.json$',
    ]

    if tool_name in ['Write', 'Edit', 'Read']:
        file_path = tool_input.get('file_path', '')
        for pattern in sensitive_patterns:
            if re.search(pattern, file_path, re.IGNORECASE):
                print(f'WARNING: Accessing sensitive file: {file_path}', file=sys.stderr)
                return None  # Warn but don't block

    return None


def _get_edit_write_content(tool_name, tool_input):
    """Extract the relevant content string from an Edit or Write tool input."""
    if tool_name == 'Edit':
        return tool_input.get('new_string', '')
    return tool_input.get('content', '')


def _get_file_path(tool_input):
    """Extract and return the file_path from tool input."""
    return tool_input.get('file_path', '')


def check_no_console_writeline(tool_name, tool_input):
    """
    Block Console.WriteLine in C# source files.
    Returns (is_blocked, message) tuple.
    """
    file_path = _get_file_path(tool_input)

    # Only check .cs files
    if not file_path.endswith('.cs'):
        return False, None

    # Skip test files and Program.cs (top-level statements sometimes need it
    # for pre-DI-container startup diagnostics, and it's not request-path code)
    if re.search(r'Tests?/|\.Tests\.|/Program\.cs$', file_path):
        return False, None

    content = _get_edit_write_content(tool_name, tool_input)
    if re.search(r'\bConsole\.(WriteLine|Write)\s*\(', content):
        return True, (
            'BLOCKED: Do not use Console.WriteLine in source files.\n'
            'Use ILogger<T> instead.\n'
            '  Inject: ILogger<MyClass> logger\n'
            '  Call: logger.LogInformation("message")'
        )

    return False, None


def log_tool_use(session_id, input_data):
    """Log tool usage to session directory."""
    log_dir = ensure_session_log_dir(session_id)
    log_path = log_dir / 'pre_tool_use.json'

    if log_path.exists():
        with open(log_path, 'r') as f:
            try:
                log_data = json.load(f)
            except (json.JSONDecodeError, ValueError):
                log_data = []
    else:
        log_data = []

    log_data.append(input_data)

    with open(log_path, 'w') as f:
        json.dump(log_data, f, indent=2)


def main():
    try:
        input_data = json.load(sys.stdin)

        tool_name = input_data.get('tool_name', '')
        tool_input = input_data.get('tool_input', {})
        session_id = input_data.get('session_id', 'unknown')

        # Log all tool usage
        log_tool_use(session_id, input_data)

        # Check Bash commands
        if tool_name == 'Bash':
            command = tool_input.get('command', '')

            # Check git commands
            is_blocked, message = check_git_commands(command)
            if is_blocked:
                print(message, file=sys.stderr)
                sys.exit(2)

            # Check rm commands
            is_blocked, message = is_dangerous_rm_command(command)
            if is_blocked:
                print(message, file=sys.stderr)
                sys.exit(2)

        # Check protected files
        is_blocked, message = check_protected_files(tool_name, tool_input)
        if is_blocked:
            print(message, file=sys.stderr)
            sys.exit(2)

        # Check Edit/Write content rules
        if tool_name in ('Edit', 'Write'):
            is_blocked, message = check_no_console_writeline(tool_name, tool_input)
            if is_blocked:
                print(message, file=sys.stderr)
                sys.exit(2)

        # Warn on sensitive files (not blocked)
        check_env_files(tool_name, tool_input)

        sys.exit(0)

    except json.JSONDecodeError:
        sys.exit(0)
    except Exception:
        sys.exit(0)


if __name__ == '__main__':
    main()
