# termuddle

A terminal-based chat client for interacting with an Inference Engine's API Endpoint that support the OpenAI v1 API.

Works with [Ollama](https://ollama.com), [OpenAI](https://platform.openai.com), [LM Studio](https://lmstudio.ai), and any other provider exposing a compatible `/v1` endpoint.

Importantly it was built as a tool to quickly connect to and interact with LOCAL or LAN based engines, so if you want to run Ollama on your machine or on your network, you can your the IP of the machine to connect e.g. 127.0.0.1:11434/v1 (after you've installed an engine and some test models) 

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

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

## Quick Start

```bash
# Clone and build
git clone https://github.com/cordelllawrence/termuddle.git
cd termuddle
dotnet build

# Run (launches setup wizard on first use)
dotnet run

# Or pass connection details directly
dotnet run -- --base-url http://localhost:11434/v1 --model llama3
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

CLI options override saved config values for that session and are persisted.

### Quick Question Mode

Use `--ask` to send a single question, print the response to stdout, and exit — useful for scripts and pipelines:

```bash
dotnet run -- --ask "What is the capital of France?"
dotnet run -- --model llama3 --ask "Summarize this error" < error.log
```

## Commands

See [USAGE.md](USAGE.md) for the full command reference.

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
