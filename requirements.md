# tuichat Requirements

## Overview

A terminal-based chat application that connects to an Ollama server (local or network) and provides an interactive REPL (Read-Eval-Print Loop) for conversational interaction. Settings are persisted to a preferences file for a seamless experience across sessions.

## Functional Requirements

### REPL Loop
- Display a prompt with a timestamp: `[yyyy-MM-dd hh:mm:ss tt] You> ` (e.g., `[2026-02-12 03:45:12 PM] You> `)
- Send the user's message to the Ollama server via its OpenAI-compatible endpoint
- Print the model's response to the console
- Repeat until the user exits

### Conversation Context
- Maintain conversation history so the model has context of prior messages
- Send the full message history with each API request

### Slash Commands
User can enter commands prefixed with `/` instead of chat messages:

- `/bye` — Exit the application gracefully with a farewell message
- `/info` — Display session information:
  - Current Ollama host
  - Current model name
  - Number of messages sent by the user
  - Number of messages received from the model
- `/help` — List all available slash commands with descriptions
- `/clear` — Clear the conversation history and start fresh
- `/models` — Query Ollama and list all available models, marking the active one
- `/switch <model>` — Switch to a different model mid-conversation (validates the model exists)
- `/preferences show` — Display current saved preferences and the preferences file path
- `/preferences set <key>=<value>` — Update a preference, save to disk, and apply immediately
  - `ollama.host` — Change the Ollama server host (reconnects)
  - `ollama.model` — Change the default model (switches)

### Exit Handling
- Support exiting via `/bye` or Ctrl+C
- Exit gracefully with a farewell message

### Ollama Integration
- Connect to Ollama's OpenAI-compatible `/v1` endpoint using the OpenAI SDK
- List available models via Ollama's REST API (`GET /api/tags`)
- No API key required (dummy key used for SDK compatibility)
- Display clear errors if the server is unreachable or returns an error

### Preferences System
- Persist settings to `preferences.json` (located next to the executable)
- Store: Ollama host, selected model
- On first run, launch a setup wizard:
  1. Ask for Ollama host (default from `OLLAMA_HOST` env var, or `localhost`)
  2. Connect and fetch available models
  3. Display numbered model list for user selection
  4. Save preferences to disk
- On subsequent runs, load preferences and skip the wizard
- `OLLAMA_HOST` environment variable overrides the saved host preference at runtime

## Non-Functional Requirements

### Console Coloring
- User input prompt and text: one distinct color (e.g., green)
- Model response text: a different distinct color (e.g., cyan)
- Error messages: red
- System messages (startup banner, exit message): yellow

### User Experience
- Display a startup banner with the app name, host, model, and instructions
- Show a loading/thinking indicator while waiting for the API response
- Support multi-line user input via a trailing `\`

### Configuration
- `OLLAMA_HOST` environment variable (optional) — overrides saved host preference
- All other configuration managed via `preferences.json` and `/preferences` commands

### Error Handling
- Handle network errors and API failures gracefully without crashing
- Display user-friendly error messages and allow the user to continue the session
