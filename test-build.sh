#!/usr/bin/env bash
# ============================================================================
# test-build.sh — Smoke test harness for trimmed termuddle builds
#
# Runs through CLI flags and features to catch trimming/AOT breakage.
# Requires an Ollama server for server-dependent tests (configurable below).
#
# Usage:
#   ./test-build.sh                     # test ./publish/termuddle
#   ./test-build.sh ./bin/termuddle     # test a specific binary
#   VERBOSE=1 ./test-build.sh           # show command output for each test
#
# Environment variables:
#   TERMUDDLE_BASE_URL   — API base URL  (default: http://bullshark:11434/v1)
#   TERMUDDLE_MODEL      — model to use  (default: gemma4:e2b)
#   TERMUDDLE_TEST_IMAGE — image file    (default: ~/Pictures/sample.png)
#   VERBOSE              — set to 1 to show command output inline
# ============================================================================

set -uo pipefail

BINARY="${1:-./publish/termuddle}"
BASE_URL="${TERMUDDLE_BASE_URL:-http://bullshark:11434/v1}"
MODEL="${TERMUDDLE_MODEL:-gemma4:e2b}"
TEST_IMAGE="${TERMUDDLE_TEST_IMAGE:-$HOME/Pictures/sample.png}"
VERBOSE="${VERBOSE:-0}"

# --- Colors ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
CYAN='\033[0;36m'
DIM='\033[2m'
NC='\033[0m'

PASS=0
FAIL=0
SKIP=0
FAILED_NAMES=()

# --- Helpers ---

# Runs a command with a timeout using perl (available on all macOS).
# Usage: run_with_timeout <seconds> <command...>
# Returns: command exit code, or 124 on timeout.
run_with_timeout() {
    local secs="$1"; shift
    perl -e '
        use POSIX ":sys_wait_h";
        my $pid = fork();
        if ($pid == 0) { exec @ARGV; exit(127); }
        my $elapsed = 0;
        while ($elapsed < '"$secs"') {
            my $r = waitpid($pid, WNOHANG);
            if ($r > 0) { exit($? >> 8); }
            select(undef, undef, undef, 0.25);
            $elapsed += 0.25;
        }
        kill("TERM", $pid);
        waitpid($pid, 0);
        exit(124);
    ' -- "$@"
}

print_test_header() {
    printf "${CYAN}[TEST]${NC} %-55s " "$1"
}

show_output() {
    local file="$1"
    if [ "$VERBOSE" = "1" ]; then
        echo ""
        sed 's/^/       /' "$file"
    fi
}

show_fail_output() {
    local file="$1"
    echo ""
    head -10 "$file" | sed 's/^/       /'
}

# run_test <name> <timeout> <command...>
# Expects exit code 0.
run_test() {
    local name="$1" timeout_sec="$2"
    shift 2

    print_test_header "$name"

    local tmpout
    tmpout=$(mktemp)

    local code=0
    run_with_timeout "$timeout_sec" "$@" >"$tmpout" 2>&1 || code=$?

    if [ $code -eq 0 ]; then
        printf "${GREEN}PASS${NC}\n"
        show_output "$tmpout"
        PASS=$((PASS + 1))
    elif [ $code -eq 124 ]; then
        printf "${RED}FAIL (timeout ${timeout_sec}s)${NC}\n"
        show_fail_output "$tmpout"
        FAIL=$((FAIL + 1))
        FAILED_NAMES+=("$name")
    else
        printf "${RED}FAIL (exit $code)${NC}\n"
        show_fail_output "$tmpout"
        FAIL=$((FAIL + 1))
        FAILED_NAMES+=("$name")
    fi

    rm -f "$tmpout"
}

# run_test_expect_fail <name> <timeout> <command...>
# Expects non-zero exit code.
run_test_expect_fail() {
    local name="$1" timeout_sec="$2"
    shift 2

    print_test_header "$name"

    local tmpout
    tmpout=$(mktemp)

    local code=0
    run_with_timeout "$timeout_sec" "$@" >"$tmpout" 2>&1 || code=$?

    if [ $code -eq 124 ]; then
        printf "${RED}FAIL (timeout ${timeout_sec}s)${NC}\n"
        show_fail_output "$tmpout"
        FAIL=$((FAIL + 1))
        FAILED_NAMES+=("$name")
    elif [ $code -ne 0 ]; then
        printf "${GREEN}PASS${DIM} (exit $code)${NC}\n"
        show_output "$tmpout"
        PASS=$((PASS + 1))
    else
        printf "${RED}FAIL (expected non-zero exit)${NC}\n"
        show_fail_output "$tmpout"
        FAIL=$((FAIL + 1))
        FAILED_NAMES+=("$name")
    fi

    rm -f "$tmpout"
}

# run_test_output_contains <name> <expected_text> <timeout> <command...>
# Expects exit code 0 and output containing expected_text (case-insensitive).
run_test_output_contains() {
    local name="$1" expected="$2" timeout_sec="$3"
    shift 3

    print_test_header "$name"

    local tmpout
    tmpout=$(mktemp)

    local code=0
    run_with_timeout "$timeout_sec" "$@" >"$tmpout" 2>&1 || code=$?

    if grep -qi "$expected" "$tmpout" 2>/dev/null; then
        printf "${GREEN}PASS${NC}\n"
        show_output "$tmpout"
        PASS=$((PASS + 1))
    elif [ $code -eq 124 ]; then
        printf "${RED}FAIL (timeout ${timeout_sec}s)${NC}\n"
        show_fail_output "$tmpout"
        FAIL=$((FAIL + 1))
        FAILED_NAMES+=("$name")
    else
        printf "${RED}FAIL (output missing: '$expected')${NC}\n"
        show_fail_output "$tmpout"
        FAIL=$((FAIL + 1))
        FAILED_NAMES+=("$name")
    fi

    rm -f "$tmpout"
}

skip_test() {
    local name="$1" reason="$2"
    printf "${CYAN}[TEST]${NC} %-55s ${YELLOW}SKIP ($reason)${NC}\n" "$name"
    SKIP=$((SKIP + 1))
}

# =============================================
#  Preflight
# =============================================
echo ""
echo "============================================="
echo "  termuddle build smoke tests"
echo "============================================="
echo ""

if [ ! -x "$BINARY" ]; then
    echo -e "${RED}ERROR: Binary not found or not executable: $BINARY${NC}"
    exit 1
fi
echo -e "Binary:     ${CYAN}$BINARY${NC}"
echo -e "Size:       ${CYAN}$(du -h "$BINARY" | cut -f1)${NC}"
echo -e "Base URL:   ${CYAN}$BASE_URL${NC}"
echo -e "Model:      ${CYAN}$MODEL${NC}"
echo -e "Test image: ${CYAN}$TEST_IMAGE${NC}"
echo -e "Verbose:    ${CYAN}$VERBOSE${NC}"

ORIGIN=$(echo "$BASE_URL" | sed 's|/v1$||')
echo ""
printf "Checking server... "
if curl -s --connect-timeout 3 "$ORIGIN" >/dev/null 2>&1; then
    echo -e "${GREEN}reachable${NC}"
    SERVER_UP=true
else
    echo -e "${YELLOW}unreachable (server-dependent tests will be skipped)${NC}"
    SERVER_UP=false
fi

IMAGE_OK=false
[ -f "$TEST_IMAGE" ] && IMAGE_OK=true

# =============================================
#  Section 1: Help & Version (no server needed)
# =============================================
echo ""
echo "---------------------------------------------"
echo " Section 1: Help & Version (no server needed)"
echo "---------------------------------------------"
echo ""

run_test_output_contains "--help flag"                        "Usage:"  5  "$BINARY" --help
run_test                 "--version flag"                     5  "$BINARY" --version
run_test_output_contains "--help shows --use-ollama-api"     "use-ollama-api"  5  "$BINARY" --help
run_test_output_contains "--help shows --use-openai-api"     "use-openai-api"  5  "$BINARY" --help
run_test_output_contains "--help shows --attach"             "attach"          5  "$BINARY" --help
run_test_output_contains "--help shows --no-tools"           "no-tools"        5  "$BINARY" --help
run_test_output_contains "--help shows --generate-image"     "generate-image"  5  "$BINARY" --help
run_test_output_contains "--version outputs version"          "0."              5  "$BINARY" --version

# =============================================
#  Section 2: Validation & error handling
# =============================================
echo ""
echo "---------------------------------------------"
echo " Section 2: Validation & error handling"
echo "---------------------------------------------"
echo ""

run_test_expect_fail     "--attach without --ask"            15  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --attach /dev/null
run_test_expect_fail     "--generate-image without --ask"    15  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --generate-image
run_test_expect_fail     "unreachable server"                15  "$BINARY" --base-url http://127.0.0.1:19999/v1 --model fake --ask "test"

# =============================================
#  Section 3: Server-dependent tests
# =============================================
echo ""
echo "---------------------------------------------"
echo " Section 3: Server-dependent tests"
echo "---------------------------------------------"
echo ""

if [ "$SERVER_UP" = true ]; then

    run_test_output_contains "auto-detect Ollama server"     "Ollama"  60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --ask "Say OK" --no-tools
    run_test                 "--use-ollama-api flag"                    60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --use-ollama-api --ask "Say OK" --no-tools
    run_test                 "--use-openai-api flag"                    60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --use-openai-api --ask "Say OK" --no-tools
    run_test                 "--ask simple question"                    60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --ask "What is 2+2? Reply with just the number." --no-tools
    run_test                 "--ask with --stream"                      60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --stream --ask "Say hello" --no-tools
    run_test                 "--ask with --tps"                         60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --tps --ask "Say hello" --no-tools
    run_test                 "--stream --tps --no-tools combined"       60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --stream --tps --no-tools --ask "Say hi"

    if [ "$IMAGE_OK" = true ]; then
        run_test             "--attach image (vision test)"           120  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --no-tools --ask "Describe this image in one sentence." --attach "$TEST_IMAGE"
        run_test             "--attach image with --stream"           120  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --no-tools --stream --ask "What colors do you see? One sentence." --attach "$TEST_IMAGE"
    else
        skip_test "--attach image (vision test)"  "test image not found: $TEST_IMAGE"
        skip_test "--attach image with --stream"  "test image not found: $TEST_IMAGE"
    fi

    TMPFILE=$(mktemp /tmp/termuddle-test-XXXXXX.txt)
    echo "The quick brown fox jumps over the lazy dog." > "$TMPFILE"
    run_test                 "--attach text file"                      60  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --no-tools --ask "What does the attached file say? Reply in 5 words or less." --attach "$TMPFILE"
    rm -f "$TMPFILE"

    run_test_expect_fail     "--attach nonexistent file"               15  "$BINARY" --base-url "$BASE_URL" --model "$MODEL" --no-tools --ask "test" --attach /tmp/this-file-does-not-exist-12345.png

else
    skip_test "auto-detect Ollama server"           "server unreachable"
    skip_test "--use-ollama-api flag"                "server unreachable"
    skip_test "--use-openai-api flag"                "server unreachable"
    skip_test "--ask simple question"                "server unreachable"
    skip_test "--ask with --stream"                  "server unreachable"
    skip_test "--ask with --tps"                     "server unreachable"
    skip_test "--stream --tps --no-tools combined"   "server unreachable"
    skip_test "--attach image (vision test)"         "server unreachable"
    skip_test "--attach image with --stream"         "server unreachable"
    skip_test "--attach text file"                   "server unreachable"
    skip_test "--attach nonexistent file"            "server unreachable"
fi

# =============================================
#  Summary
# =============================================
echo ""
echo "============================================="
echo "  Results"
echo "============================================="
echo ""
echo -e "  ${GREEN}Passed:  $PASS${NC}"
echo -e "  ${RED}Failed:  $FAIL${NC}"
echo -e "  ${YELLOW}Skipped: $SKIP${NC}"
echo ""

if [ $FAIL -gt 0 ]; then
    echo -e "${RED}Failed tests:${NC}"
    for name in "${FAILED_NAMES[@]}"; do
        echo -e "  ${RED}• $name${NC}"
    done
    echo ""
    exit 1
fi

echo -e "${GREEN}All tests passed!${NC}"
exit 0
