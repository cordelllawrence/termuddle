# termuddle

A lightweight terminal-based chat client built for developers who want to quickly connect to, switch between, and evaluate different LLM models during development. Point it at any server exposing an OpenAI-compatible `/v1` endpoint and start chatting — no setup files required.

Works with [Ollama](https://ollama.com), [OpenAI](https://platform.openai.com), [LM Studio](https://lmstudio.ai), and any other compatible provider. Designed with local and LAN-based inference in mind — connect to Ollama on your machine or anywhere on your network by IP (e.g., `http://192.168.1.154:11434/v1`).

**Ollama native support:** termuddle auto-detects Ollama servers and switches to the native Ollama API for full feature support, including vision (image analysis) with models like `gemma4`, `llava`, and `llama3.2-vision`.

## Features

- **Streaming responses** with real-time token output
- **Built-in tools** — web search (DuckDuckGo), web page fetching, and date/time — available to the model automatically
- **Slash commands** for managing models, config, and session state
- **Tab completion** for commands and subcommands
- **Input history** with up/down arrow navigation
- **Type-ahead support** — keep typing while the model is responding
- **Thinking animation** while waiting for responses
- **Config persistence** with backup and restore
- **Server validation** — verifies the server is reachable and the model is available on startup and when changing config
- **Tokens-per-second stats** (optional)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later (for building from source)

## Quick Start

### From a release binary

Download the latest release for your platform and run directly:

```bash
# Run (launches setup wizard on first use)
./termuddle

# Or pass connection details directly
./termuddle --base-url http://localhost:11434/v1 --model llama3
```

### From source

```bash
# Clone and build
git clone https://github.com/cordelllawrence/termuddle.git
cd termuddle
dotnet build

# Run via dotnet
dotnet run

# Or publish a trimmed single-file binary
dotnet publish -c Release --self-contained -r osx-arm64 /p:PublishTrimmed=true /p:PublishSingleFile=true -o ./publish
./publish/termuddle
```

On first run without a config file, termuddle walks you through a setup wizard to configure your API endpoint, key, and model.

## CLI Options

| Option | Description |
|---|---|
| `--base-url` | Base URL for the API provider |
| `--api-key` | API key for authentication |
| `--model` | Model name to use |
| `--stream` | Enable streaming responses |
| `--tps` | Show tokens-per-second stats |
| `--ask <question>` | Ask a single question and exit (pipe-friendly) |
| `--attach <file>` | Attach file(s) to the prompt (repeatable, use with `--ask`) |
| `--no-tools` | Disable built-in tool use (web search, fetch, etc.) |
| `--generate-image` | Use the image generation endpoint instead of chat (use with `--ask`) |
| `--use-ollama-api` | Force using Ollama native API (skip auto-detection) |
| `--use-openai-api` | Force using OpenAI-compatible API (skip auto-detection) |

CLI options override saved config values for that session and are persisted.

### API Provider Detection

By default, termuddle auto-detects whether the server is Ollama and uses the appropriate client library:

- **Ollama servers** use the native Ollama API via [OllamaSharp](https://github.com/awaescher/OllamaSharp) for full feature support (vision, audio, etc.)
- **Other servers** use the OpenAI-compatible API via the official OpenAI SDK

You can override auto-detection with `--use-ollama-api` or `--use-openai-api`:

```bash
# Force Ollama native API
termuddle --base-url http://my-server:11434/v1 --model gemma4:e2b --use-ollama-api

# Force OpenAI-compatible API
termuddle --base-url http://my-server:11434/v1 --model llama3 --use-openai-api
```

### Quick Question Mode

Use `--ask` to send a single question, print the response to stdout, and exit — useful for scripts and pipelines:

```bash
termuddle --ask "What is the capital of France?"
termuddle --model llama3 --ask "Summarize this error" < error.log
```

### File Attachments

Use `--attach` with `--ask` to send files (images, text, etc.) to the model. Repeat the flag for multiple files:

```bash
# Describe an image (requires a vision-capable model)
termuddle --ask "What's in this photo?" --attach photo.jpg

# Send multiple files
termuddle --ask "Compare these two images" --attach img1.png --attach img2.png

# Attach a text file for analysis
termuddle --ask "Summarize this code" --attach src/main.cs

# Use --no-tools if the model doesn't support tool use
termuddle --model gemma3:latest --ask "Describe this" --attach photo.jpg --no-tools

# Vision with Ollama (auto-detected, uses native API for proper image support)
termuddle --base-url http://localhost:11434/v1 --model gemma4:e2b --ask "What's in this photo?" --attach photo.png --no-tools
```

Images (jpg, png, gif, webp, bmp) are sent as binary data for vision models. Other file types are inlined as text.

## Interactive Chat Mode

When launched without `--ask`, termuddle starts an interactive chat session with a split-pane terminal UI — your input at the bottom, model responses scrolling above. Type messages at the `>` prompt, and use slash commands to manage the session.

![Interactive chat mode](docs/images/interactive-mode.png)

See [USAGE.md](USAGE.md) for the full usage guide.

### Slash Commands

| Command | Description |
|---|---|
| `/help` | Show available commands |
| `/info` | Show session information |
| `/model list` | List available models on the server |
| `/model use <name\|N>` | Switch model by name, number, or partial match |
| `/config show` | Display current configuration |
| `/config set <key>=<value>` | Update a config value |
| `/config reset` | Reload config from disk |
| `/stream` | Toggle streaming on/off |
| `/tps` | Toggle tokens-per-second display |
| `/clear` | Clear conversation history |
| `/backup` | Create a config backup |
| `/backup list` | List config backups |
| `/backup load <N>` | Restore a backup |
| `/backup remove <N>` | Delete a backup |
| `/bye` | Exit |

## Configuration

Config is stored as JSON at `termuddle-config.json` in the application directory.

```json
{
  "BaseUrl": "http://localhost:11434/v1",
  "ApiKey": "",
  "Model": "llama3",
  "StreamResponses": true,
  "ShowTps": false
}
```

Config keys for `/config set`: `base_url`, `api_key`, `model`, `stream`, `tps`

## License

[Apache License 2.0](LICENSE)
