# Usage Guide

## Getting Started

### First Run — Setup Wizard

If no config file exists and no `--base-url`/`--model` CLI flags are provided, termuddle launches an interactive setup wizard:

1. Enter the base URL for your API provider
2. Enter an API key (press Enter to skip for providers like Ollama)
3. termuddle connects to the server and lists available models
4. Select a model by number

The config is saved automatically for future sessions.

### Startup Validation

On every startup, termuddle validates:
- **Server reachability** — confirms the API endpoint responds
- **Model availability** — checks that the configured model exists on the server

If the server is unreachable, termuddle shows an error and exits. If the model isn't found, it lists available models and asks you to pick one.

### Running with CLI Options

Examples below use `termuddle` for the published binary. When running from source, substitute `dotnet run --` (e.g., `dotnet run -- --ask "hello"`).

```bash
# Connect to Ollama with a specific model
termuddle --base-url http://localhost:11434/v1 --model llama3

# Connect to OpenAI
termuddle --base-url https://api.openai.com/v1 --api-key sk-... --model gpt-4o

# Connect to LM Studio
termuddle --base-url http://localhost:1234/v1 --model my-model

# Enable streaming and TPS display
termuddle --base-url http://localhost:11434/v1 --model llama3 --stream --tps

# Ask a single question and exit (no interactive session)
termuddle --ask "What is the capital of France?"

# Combine with other options
termuddle --model llama3 --ask "Explain recursion in one sentence"

# Attach files to a prompt (requires --ask)
termuddle --ask "What's in this photo?" --attach photo.jpg
termuddle --ask "Compare these" --attach img1.png --attach img2.png

# Disable tool use (for models that don't support it)
termuddle --ask "Describe this" --attach photo.jpg --no-tools

# Force a specific API provider (skip auto-detection)
termuddle --base-url http://localhost:11434/v1 --model llama3 --use-ollama-api
termuddle --base-url https://api.openai.com/v1 --api-key sk-... --model gpt-4o --use-openai-api
```

### Quick Question Mode

The `--ask` option sends a single question to the model, prints the response to stdout, and exits immediately — no interactive session is started. This makes termuddle scriptable and pipe-friendly:

```bash
# Use in a pipeline
termuddle --ask "Summarize this error" < error.log

# Capture output
ANSWER=$(termuddle --ask "What is 2 + 2?")
```

Output goes to plain stdout (no colors or formatting), and errors go to stderr with a non-zero exit code.

### File Attachments

The `--attach` option sends one or more files along with the `--ask` prompt. Repeat the flag for multiple files:

```bash
# Send an image to a vision-capable model
termuddle --ask "Describe this image" --attach screenshot.png

# Multiple attachments
termuddle --ask "What's different between these?" --attach before.jpg --attach after.jpg

# Non-image files are inlined as text
termuddle --ask "Review this code" --attach src/app.cs
termuddle --ask "Parse this data" --attach data.csv

# Vision with Ollama (auto-detected, uses native API for proper image support)
termuddle --base-url http://localhost:11434/v1 --model gemma4:e2b --ask "Describe this photo" --attach photo.png --no-tools

# Vision with a LAN-based Ollama server
termuddle --base-url http://my-server:11434/v1 --model gemma4:e2b --ask "What do you see?" --attach image.jpg --no-tools
```

**Supported image formats** (sent as binary for vision models): `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`

**Other file types** (`.txt`, `.md`, `.json`, `.xml`, `.csv`, `.html`, `.cs`, etc.) are read as text and included inline with a filename header.

**Note:** Some models (e.g., `gemma3`) support vision but not tool use. Use `--no-tools` to disable the built-in tools when the model reports a tools-related error.

### API Provider Selection

By default, termuddle auto-detects whether the server is Ollama by checking the server's root endpoint. When Ollama is detected, the native Ollama API is used via [OllamaSharp](https://github.com/awaescher/OllamaSharp) for full feature support (vision, audio, etc.). For all other servers, the OpenAI-compatible API is used via the official OpenAI SDK.

You can override auto-detection with explicit flags:

```bash
# Force Ollama native API (skips detection)
termuddle --base-url http://localhost:11434/v1 --model gemma4:e2b --use-ollama-api

# Force OpenAI-compatible API (skips detection)
termuddle --base-url http://localhost:11434/v1 --model llama3 --use-openai-api
```

**When to use these flags:**

- `--use-ollama-api` — if auto-detection fails (e.g., behind a reverse proxy) but you know the server is Ollama
- `--use-openai-api` — if you want to use Ollama's OpenAI compatibility layer instead of the native API
- If neither flag is specified, termuddle detects the provider automatically

## Chatting

Type your message at the `>` prompt and press Enter. The model's response appears in blue above the input area.

While the model is generating a response, you can keep typing — your keystrokes are captured and will appear pre-filled in the next prompt.

### Multi-line Input

End a line with `\` to continue on the next line:

```
> Tell me about the following languages:\
  ... Python, Rust, and Go
```

## Commands Reference

All commands start with `/` and support tab completion.

### Session

| Command | Description |
|---|---|
| `/help` | Display the help message with all available commands |
| `/info` | Show session details: URL, model, API key (masked), streaming status, message counts |
| `/clear` | Clear the conversation history (does not affect config) |
| `/bye` | Exit termuddle with a random goodbye message |

### Model Management

| Command | Description |
|---|---|
| `/model list` | Fetch and display all models available on the connected server. The active model is marked. |
| `/model use <name\|N>` | Switch to a different model. Accepts a full name, a number from `/model list`, or a partial name match. |

**Examples:**
```
/model list
/model use 3
/model use llama3
/model use llam        # partial match
```

If a partial name matches multiple models, termuddle lists the matches and asks you to be more specific.

### Configuration

| Command | Description |
|---|---|
| `/config show` | Display all current config values and the config file path |
| `/config set <key>=<value>` | Update a config value. Changes are validated and persisted immediately. |
| `/config reset` | Reload config from disk and reconnect to the server |

**Config keys:**

| Key | Type | Description |
|---|---|---|
| `base_url` | string | API endpoint URL |
| `api_key` | string | Authentication key |
| `model` | string | Model name |
| `stream` | bool | `true` or `false` — enable streaming responses |
| `tps` | bool | `true` or `false` — show tokens-per-second stats |

**Examples:**
```
/config set base_url=http://localhost:1234/v1
/config set model=gpt-4o
/config set stream=true
/config set tps=true
```

When changing `base_url`, `api_key`, or `model`, termuddle validates the new settings against the server before applying them. If validation fails, the change is reverted automatically.

### Quick Toggles

| Command | Description |
|---|---|
| `/stream` | Toggle streaming responses on/off |
| `/tps` | Toggle tokens-per-second display on/off |

### Backup & Restore

| Command | Description |
|---|---|
| `/backup` | Create a timestamped backup of the current config |
| `/backup list` | List all config backups with their settings |
| `/backup load <N>` | Restore the Nth backup (validated before applying) |
| `/backup remove <N>` | Delete the Nth backup (with confirmation prompt) |

**Example workflow:**
```
/backup                  # save current config
/config set base_url=http://new-server:11434/v1
# ... if something goes wrong ...
/backup list             # see available backups
/backup load 1           # restore the previous config
```

## Built-in Tools

termuddle registers tools that the model can call automatically during conversation:

| Tool | Description |
|---|---|
| **Web Search** | Searches the web via DuckDuckGo and returns top results |
| **Web Fetch** | Fetches a web page and returns it as plain text (HTML stripped, max 20K chars) |
| **Date/Time** | Returns the current local date, time, and timezone |

These tools are invoked by the model when relevant — no manual activation needed. For example, asking "What's the weather in NYC?" may trigger a web search.

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `Enter` | Submit input |
| `Up/Down` | Navigate input history (or move cursor in multi-line input) |
| `Left/Right` | Move cursor within input |
| `Home/End` | Jump to start/end of input |
| `Tab` | Cycle through command/subcommand completions |
| `Backspace/Delete` | Delete characters |
| `Ctrl+C` | Exit termuddle |

## Configuration File

The config is stored as `termuddle-config.json` in the application directory:

```json
{
  "BaseUrl": "http://localhost:11434/v1",
  "ApiKey": "",
  "Model": "llama3",
  "StreamResponses": true,
  "ShowTps": false
}
```

Backups are stored alongside it as `termuddle-config_backup_YYYY-MM-DD_HH-mm-ss.json`.

## Troubleshooting

### "Cannot reach server"
- Verify the server is running and the URL is correct
- Check that the URL includes the `/v1` path if required
- Ensure no firewall is blocking the connection

### "Model not available"
- Run `/model list` to see what's available on the server
- For Ollama, you may need to pull the model first: `ollama pull <model>`

### Response seems slow
- Enable TPS display with `/tps` to see throughput stats
- Try a smaller model if running locally
- Check if streaming is enabled with `/config show` — streaming provides faster perceived response times

### Logs
Logs are written to `logs/termuddle-YYYYMMDD.log` in the application directory with daily rotation (7-day retention).
