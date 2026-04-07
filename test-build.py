#!/usr/bin/env python3
"""
test-build.py — Smoke test harness for trimmed termuddle builds

Runs through CLI flags and features to catch trimming/AOT breakage.
Requires an Ollama server for server-dependent tests (configurable below).

Usage:
    python3 test-build.py                         # test ./publish/termuddle
    python3 test-build.py ./bin/termuddle          # test a specific binary
    python3 test-build.py --base-url http://...    # override server URL
    python3 test-build.py --model llama3           # override model

Environment variables (alternative to flags):
    TERMUDDLE_BASE_URL   — API base URL  (default: http://bullshark:11434/v1)
    TERMUDDLE_MODEL      — model to use  (default: gemma4:e2b)
    TERMUDDLE_TEST_IMAGE — image file    (default: ~/Pictures/sample.png)
"""

import argparse
import os
import subprocess
import sys
import tempfile
import urllib.request
from pathlib import Path

# --- Colors ---
RED = "\033[0;31m"
GREEN = "\033[0;32m"
YELLOW = "\033[0;33m"
CYAN = "\033[0;36m"
DIM = "\033[2m"
NC = "\033[0m"

# --- Test tracking ---
results = {"pass": 0, "fail": 0, "skip": 0}
failed_tests = []


def print_header(name):
    print(f"{CYAN}[TEST]{NC} {name:<55} ", end="", flush=True)


def run_cmd(args, timeout):
    """Run a command, return (exit_code, combined_output, timed_out)."""
    try:
        proc = subprocess.run(
            args,
            capture_output=True,
            text=True,
            timeout=timeout,
        )
        output = proc.stdout + proc.stderr
        return proc.returncode, output, False
    except subprocess.TimeoutExpired as e:
        output = (e.stdout or "") + (e.stderr or "")
        return 124, output, True


def show_output(output, indent="       "):
    """Print command output with indentation."""
    if output.strip():
        for line in output.strip().splitlines():
            print(f"{indent}{DIM}{line}{NC}")


def run_test(name, args, timeout=30):
    """Expect exit code 0."""
    print_header(name)
    code, output, timed_out = run_cmd(args, timeout)

    if code == 0:
        print(f"{GREEN}PASS{NC}")
        show_output(output)
        results["pass"] += 1
    elif timed_out:
        print(f"{RED}FAIL (timeout {timeout}s){NC}")
        show_output(output)
        results["fail"] += 1
        failed_tests.append(name)
    else:
        print(f"{RED}FAIL (exit {code}){NC}")
        show_output(output)
        results["fail"] += 1
        failed_tests.append(name)


def run_test_expect_fail(name, args, timeout=30):
    """Expect non-zero exit code."""
    print_header(name)
    code, output, timed_out = run_cmd(args, timeout)

    if timed_out:
        print(f"{RED}FAIL (timeout {timeout}s){NC}")
        show_output(output)
        results["fail"] += 1
        failed_tests.append(name)
    elif code != 0:
        print(f"{GREEN}PASS{DIM} (exit {code}){NC}")
        show_output(output)
        results["pass"] += 1
    else:
        print(f"{RED}FAIL (expected non-zero exit){NC}")
        show_output(output)
        results["fail"] += 1
        failed_tests.append(name)


def run_test_output_contains(name, expected, args, timeout=30):
    """Expect output to contain expected text (case-insensitive)."""
    print_header(name)
    code, output, timed_out = run_cmd(args, timeout)

    if expected.lower() in output.lower():
        print(f"{GREEN}PASS{NC}")
        show_output(output)
        results["pass"] += 1
    elif timed_out:
        print(f"{RED}FAIL (timeout {timeout}s){NC}")
        show_output(output)
        results["fail"] += 1
        failed_tests.append(name)
    else:
        print(f"{RED}FAIL (output missing: '{expected}'){NC}")
        show_output(output)
        results["fail"] += 1
        failed_tests.append(name)


def skip_test(name, reason):
    print(f"{CYAN}[TEST]{NC} {name:<55} {YELLOW}SKIP ({reason}){NC}")
    results["skip"] += 1


def check_server(origin):
    """Check if the server is reachable."""
    try:
        req = urllib.request.Request(origin, method="GET")
        urllib.request.urlopen(req, timeout=3)
        return True
    except Exception:
        return False


def main():
    parser = argparse.ArgumentParser(description="termuddle build smoke tests")
    parser.add_argument("binary", nargs="?", default="./publish/termuddle",
                        help="Path to termuddle binary (default: ./publish/termuddle)")
    parser.add_argument("--base-url", default=os.environ.get("TERMUDDLE_BASE_URL", "http://bullshark:11434/v1"))
    parser.add_argument("--model", default=os.environ.get("TERMUDDLE_MODEL", "gemma4:e2b"))
    parser.add_argument("--test-image", default=os.environ.get("TERMUDDLE_TEST_IMAGE",
                        str(Path.home() / "Pictures" / "sample.png")))
    args = parser.parse_args()

    binary = args.binary
    base_url = args.base_url
    model = args.model
    test_image = args.test_image
    origin = base_url.removesuffix("/v1")

    # --- Preflight ---
    print()
    print("=============================================")
    print("  termuddle build smoke tests")
    print("=============================================")
    print()

    binary_path = Path(binary)
    if not binary_path.exists() or not os.access(binary, os.X_OK):
        print(f"{RED}ERROR: Binary not found or not executable: {binary}{NC}")
        sys.exit(1)

    size_mb = binary_path.stat().st_size / (1024 * 1024)
    print(f"Binary:     {CYAN}{binary}{NC}")
    print(f"Size:       {CYAN}{size_mb:.1f} MB{NC}")
    print(f"Base URL:   {CYAN}{base_url}{NC}")
    print(f"Model:      {CYAN}{model}{NC}")
    print(f"Test image: {CYAN}{test_image}{NC}")
    print()

    print("Checking server... ", end="", flush=True)
    server_up = check_server(origin)
    if server_up:
        print(f"{GREEN}reachable{NC}")
    else:
        print(f"{YELLOW}unreachable (server-dependent tests will be skipped){NC}")

    image_ok = Path(test_image).exists()

    # =============================================
    #  Section 1: Help & Version (no server needed)
    # =============================================
    print()
    print("---------------------------------------------")
    print(" Section 1: Help & Version (no server needed)")
    print("---------------------------------------------")
    print()

    run_test_output_contains("--help flag", "Usage:", [binary, "--help"], timeout=5)
    run_test("--version flag", [binary, "--version"], timeout=5)
    run_test_output_contains("--help shows --use-ollama-api", "use-ollama-api", [binary, "--help"], timeout=5)
    run_test_output_contains("--help shows --use-openai-api", "use-openai-api", [binary, "--help"], timeout=5)
    run_test_output_contains("--help shows --attach", "attach", [binary, "--help"], timeout=5)
    run_test_output_contains("--help shows --no-tools", "no-tools", [binary, "--help"], timeout=5)
    run_test_output_contains("--help shows --generate-image", "generate-image", [binary, "--help"], timeout=5)
    run_test_output_contains("--version outputs version", "0.", [binary, "--version"], timeout=5)

    # =============================================
    #  Section 2: Validation & error handling
    # =============================================
    print()
    print("---------------------------------------------")
    print(" Section 2: Validation & error handling")
    print("---------------------------------------------")
    print()

    run_test_expect_fail("--attach without --ask",
                         [binary, "--base-url", base_url, "--model", model, "--attach", "/dev/null"], timeout=15)
    run_test_expect_fail("--generate-image without --ask",
                         [binary, "--base-url", base_url, "--model", model, "--generate-image"], timeout=15)
    run_test_expect_fail("unreachable server",
                         [binary, "--base-url", "http://127.0.0.1:19999/v1", "--model", "fake", "--ask", "test"], timeout=30)

    # =============================================
    #  Section 3: Server-dependent tests
    # =============================================
    print()
    print("---------------------------------------------")
    print(" Section 3: Server-dependent tests")
    print("---------------------------------------------")
    print()

    if server_up:
        run_test_output_contains("auto-detect Ollama server", "Ollama",
                                 [binary, "--base-url", base_url, "--model", model,
                                  "--ask", "Say OK", "--no-tools"], timeout=60)

        run_test("--use-ollama-api flag",
                 [binary, "--base-url", base_url, "--model", model,
                  "--use-ollama-api", "--ask", "Say OK", "--no-tools"], timeout=60)

        run_test("--use-openai-api flag",
                 [binary, "--base-url", base_url, "--model", model,
                  "--use-openai-api", "--ask", "Say OK", "--no-tools"], timeout=60)

        run_test("--ask simple question",
                 [binary, "--base-url", base_url, "--model", model,
                  "--ask", "What is 2+2? Reply with just the number.", "--no-tools"], timeout=60)

        run_test("--ask with --stream",
                 [binary, "--base-url", base_url, "--model", model,
                  "--stream", "--ask", "Say hello", "--no-tools"], timeout=60)

        run_test("--ask with --tps",
                 [binary, "--base-url", base_url, "--model", model,
                  "--tps", "--ask", "Say hello", "--no-tools"], timeout=60)

        run_test("--stream --tps --no-tools combined",
                 [binary, "--base-url", base_url, "--model", model,
                  "--stream", "--tps", "--no-tools", "--ask", "Say hi"], timeout=60)

        if image_ok:
            run_test("--attach image (vision test)",
                     [binary, "--base-url", base_url, "--model", model, "--no-tools",
                      "--ask", "Describe this image in one sentence.", "--attach", test_image], timeout=120)

            run_test("--attach image with --stream",
                     [binary, "--base-url", base_url, "--model", model, "--no-tools", "--stream",
                      "--ask", "What colors do you see? One sentence.", "--attach", test_image], timeout=120)
        else:
            skip_test("--attach image (vision test)", f"image not found: {test_image}")
            skip_test("--attach image with --stream", f"image not found: {test_image}")

        # Text file attachment
        with tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False) as f:
            f.write("The quick brown fox jumps over the lazy dog.\n")
            tmp_path = f.name
        try:
            run_test("--attach text file",
                     [binary, "--base-url", base_url, "--model", model, "--no-tools",
                      "--ask", "What does the attached file say? Reply in 5 words or less.",
                      "--attach", tmp_path], timeout=60)
        finally:
            os.unlink(tmp_path)

        run_test_expect_fail("--attach nonexistent file",
                             [binary, "--base-url", base_url, "--model", model, "--no-tools",
                              "--ask", "test", "--attach", "/tmp/this-file-does-not-exist-12345.png"], timeout=15)
    else:
        for name in [
            "auto-detect Ollama server", "--use-ollama-api flag", "--use-openai-api flag",
            "--ask simple question", "--ask with --stream", "--ask with --tps",
            "--stream --tps --no-tools combined", "--attach image (vision test)",
            "--attach image with --stream", "--attach text file", "--attach nonexistent file",
        ]:
            skip_test(name, "server unreachable")

    # =============================================
    #  Summary
    # =============================================
    print()
    print("=============================================")
    print("  Results")
    print("=============================================")
    print()
    print(f"  {GREEN}Passed:  {results['pass']}{NC}")
    print(f"  {RED}Failed:  {results['fail']}{NC}")
    print(f"  {YELLOW}Skipped: {results['skip']}{NC}")
    print()

    if failed_tests:
        print(f"{RED}Failed tests:{NC}")
        for name in failed_tests:
            print(f"  {RED}* {name}{NC}")
        print()
        sys.exit(1)

    print(f"{GREEN}All tests passed!{NC}")
    sys.exit(0)


if __name__ == "__main__":
    main()
