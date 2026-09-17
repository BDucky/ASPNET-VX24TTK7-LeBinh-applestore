#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.8"
# ///

"""
Post-tool-use hook for the apple-store ASP.NET Core solution.

Adapted from corjl-webapp/.claude/hooks/post_tool_use.py: the output parsers
targeted ESLint/tsc/Vitest/Playwright formats and auto-fixed via
`pnpm exec eslint --fix`; both are rewritten here for `dotnet build`/`dotnet
format`/`dotnet test` output and `dotnet format --include`.

Features:
- Log all tool usage
- Monitor build/format/test failures
- Track command execution metrics
- Summarize errors for quick feedback
"""

import json
import os
import sys
import re
from pathlib import Path
from datetime import datetime

from utils.constants import ensure_session_log_dir


def parse_build_output(output, tool_result):
    """
    Parse `dotnet build` output for errors.
    Returns dict with error summary.
    """
    errors = {
        'type': 'build',
        'has_errors': False,
        'error_count': 0,
        'errors': [],
    }

    if not output:
        return errors

    # dotnet build error lines look like: "Foo.cs(12,5): error CS1002: ..."
    cs_errors = re.findall(r'error CS\d+:', output)
    if cs_errors:
        errors['has_errors'] = True
        errors['error_count'] = len(cs_errors)

    error_lines = [line for line in output.split('\n') if re.search(r'error CS\d+:|Build FAILED', output)]
    errors['errors'] = [line for line in output.split('\n') if ': error ' in line][:5]

    return errors


def parse_format_output(output, tool_result):
    """
    Parse `dotnet format --verify-no-changes` output for files needing formatting.
    Returns dict with error summary.
    """
    errors = {
        'type': 'format',
        'has_errors': False,
        'error_count': 0,
        'errors': [],
    }

    if not output:
        return errors

    # dotnet format --verify-no-changes prints one "Formatted code file '...'"
    # line per file that WOULD change, and exits non-zero if any did.
    formatted_lines = re.findall(r"Formatted code file '(.+)'", output)
    if formatted_lines:
        errors['has_errors'] = True
        errors['error_count'] = len(formatted_lines)
        errors['errors'] = formatted_lines[:5]

    return errors


def parse_test_output(output, tool_result):
    """
    Parse `dotnet test` output.
    Returns dict with test summary.
    """
    results = {
        'type': 'test',
        'has_failures': False,
        'passed': 0,
        'failed': 0,
        'skipped': 0,
        'failed_tests': [],
    }

    if not output:
        return results

    # dotnet test VSTest summary line, e.g.:
    # "Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 1 s"
    # "Failed!  - Failed: 1, Passed: 11, Skipped: 0, Total: 12, Duration: 1 s"
    summary_match = re.search(
        r'Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+)', output
    )
    if summary_match:
        results['failed'] = int(summary_match.group(1))
        results['passed'] = int(summary_match.group(2))
        results['skipped'] = int(summary_match.group(3))
        results['has_failures'] = results['failed'] > 0

    failed_tests = re.findall(r'Failed\s+(\S+)\s+\[', output)
    results['failed_tests'] = failed_tests[:5]

    return results


def analyze_command_result(command, output, tool_result):
    """
    Analyze command output and return structured result.
    """
    analysis = {
        'command': command,
        'timestamp': datetime.now().isoformat(),
        'exit_code': tool_result.get('exit_code', 0) if isinstance(tool_result, dict) else 0,
        'analysis': None
    }

    if re.search(r'dotnet\s+build', command):
        analysis['analysis'] = parse_build_output(output, tool_result)
    elif re.search(r'dotnet\s+format', command):
        analysis['analysis'] = parse_format_output(output, tool_result)
    elif re.search(r'dotnet\s+test', command):
        analysis['analysis'] = parse_test_output(output, tool_result)

    return analysis


def log_tool_use(session_id, input_data, analysis=None):
    """Log tool usage to session directory."""
    log_dir = ensure_session_log_dir(session_id)
    log_path = log_dir / 'post_tool_use.json'

    if log_path.exists():
        with open(log_path, 'r') as f:
            try:
                log_data = json.load(f)
            except (json.JSONDecodeError, ValueError):
                log_data = []
    else:
        log_data = []

    # Add analysis to input data if available
    if analysis:
        input_data['_analysis'] = analysis

    log_data.append(input_data)

    with open(log_path, 'w') as f:
        json.dump(log_data, f, indent=2)


def log_build_metrics(session_id, analysis):
    """
    Log build/test metrics separately for easy tracking.
    """
    if not analysis or not analysis.get('analysis'):
        return

    log_dir = ensure_session_log_dir(session_id)
    metrics_path = log_dir / 'build_metrics.json'

    if metrics_path.exists():
        with open(metrics_path, 'r') as f:
            try:
                metrics = json.load(f)
            except (json.JSONDecodeError, ValueError):
                metrics = []
    else:
        metrics = []

    metrics.append({
        'timestamp': analysis['timestamp'],
        'command': analysis['command'],
        'type': analysis['analysis'].get('type'),
        'has_errors': analysis['analysis'].get('has_errors', False) or analysis['analysis'].get('has_failures', False),
        'error_count': analysis['analysis'].get('error_count', 0) + analysis['analysis'].get('failed', 0),
    })

    with open(metrics_path, 'w') as f:
        json.dump(metrics, f, indent=2)


def auto_format_fix(tool_name, tool_input):
    """Auto-format the edited file after Write/Edit."""
    if tool_name not in ('Write', 'Edit'):
        return

    file_path = tool_input.get('file_path', '')
    if not file_path:
        return

    # Only format C# source files
    if not file_path.endswith('.cs'):
        return

    skip_patterns = ['/bin/', '/obj/', '/Migrations/']
    if any(p in file_path for p in skip_patterns):
        return

    import subprocess
    try:
        subprocess.run(
            ['dotnet', 'format', 'AppleStore.slnx', '--include', file_path],
            capture_output=True, timeout=60,
            cwd=os.environ.get('CLAUDE_PROJECT_DIR', '.')
        )
    except Exception:
        pass  # Never block the agent


def main():
    try:
        input_data = json.load(sys.stdin)

        tool_name = input_data.get('tool_name', '')
        tool_input = input_data.get('tool_input', {})
        tool_result = input_data.get('tool_result', {})
        session_id = input_data.get('session_id', 'unknown')

        analysis = None

        # Auto-format after file edits
        auto_format_fix(tool_name, tool_input)

        # Analyze Bash command results
        if tool_name == 'Bash':
            command = tool_input.get('command', '')
            output = ''

            # Extract output from tool_result
            if isinstance(tool_result, dict):
                output = tool_result.get('stdout', '') + tool_result.get('stderr', '')
            elif isinstance(tool_result, str):
                output = tool_result

            # Analyze build/format/test commands
            if re.search(r'dotnet\s+(build|format|test)', command):
                analysis = analyze_command_result(command, output, tool_result)

                # Log metrics separately
                log_build_metrics(session_id, analysis)

                # Print summary to stderr for visibility
                if analysis['analysis']:
                    a = analysis['analysis']
                    if a.get('has_errors') or a.get('has_failures'):
                        error_count = a.get('error_count', 0) + a.get('failed', 0)
                        print(f"[{a['type'].upper()}] {error_count} error(s) detected", file=sys.stderr)

        # Log all tool usage
        log_tool_use(session_id, input_data, analysis)

        sys.exit(0)

    except json.JSONDecodeError:
        sys.exit(0)
    except Exception:
        sys.exit(0)


if __name__ == '__main__':
    main()
