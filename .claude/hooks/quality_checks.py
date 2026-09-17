#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///

"""
Quality checks hook - runs build, format-check, and test.
Intended to run at the end of Claude's response via the Stop hook.

Adapted from corjl-webapp/.claude/hooks/quality_checks.py (pnpm test:affected /
lint:affected / typecheck:affected) to dotnet build / dotnet format
--verify-no-changes / dotnet test. Like the corjl-webapp original, this file is
NOT wired into .claude/settings.json's Stop hooks by default: dotnet test runs
the whole suite (no "affected only" equivalent readily available the way pnpm's
Nx-based affected commands provide), so auto-blocking every response on a full
test run would get slow as the suite grows. Wire it in manually if the team
wants a hard gate.
"""

import json
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path


def run_command(cmd: list[str], cwd: str, name: str) -> tuple[str, bool, str]:
    """Run a command and return (name, success, output)."""
    try:
        result = subprocess.run(
            cmd,
            cwd=cwd,
            capture_output=True,
            text=True,
            timeout=300,  # 5 minute timeout per command
        )
        output = result.stdout + result.stderr
        return name, result.returncode == 0, output
    except subprocess.TimeoutExpired:
        return name, False, f"Command timed out: {' '.join(cmd)}"
    except Exception as e:
        return name, False, f"Command failed: {e}"


def main():
    try:
        # Read JSON input from stdin
        input_data = json.load(sys.stdin)

        # Prevent infinite loops - skip if stop hook already active
        stop_hook_active = input_data.get("stop_hook_active", False)
        if stop_hook_active:
            sys.exit(0)

        cwd = input_data.get("cwd", ".")
        project_dir = Path(cwd)

        # Check if we're in the right directory with the solution file
        if not (project_dir / "AppleStore.slnx").exists():
            sys.exit(0)

        # Define checks to run in parallel
        checks = [
            (["dotnet", "build"], "build"),
            (["dotnet", "format", "--verify-no-changes"], "format"),
            (["dotnet", "test"], "test"),
        ]

        failures = []

        # Run all checks in parallel
        with ThreadPoolExecutor(max_workers=3) as executor:
            futures = [
                executor.submit(run_command, cmd, cwd, name)
                for cmd, name in checks
            ]
            for future in as_completed(futures):
                name, success, output = future.result()
                if not success:
                    failures.append((name, output))

        if failures:
            # Build error message
            error_parts = ["Quality checks failed:"]
            for check_name, output in failures:
                # Truncate output to avoid overwhelming Claude
                truncated = output[:2000] if len(output) > 2000 else output
                error_parts.append(f"\n\n### {check_name}\n{truncated}")

            result = {
                "decision": "block",
                "reason": "\n".join(error_parts),
            }
            print(json.dumps(result))

        sys.exit(0)

    except json.JSONDecodeError:
        sys.exit(0)
    except Exception:
        sys.exit(0)


if __name__ == "__main__":
    main()
