# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

tuichat is a terminal-based REPL chat application that connects to any OpenAI-compatible chat/completions API (Ollama, OpenAI, LM Studio, vLLM, Together AI, etc.). Built with C# targeting .NET 10.0.

## Build & Run Commands

```bash
dotnet build              # Build (Debug)
dotnet build -c Release   # Build (Release)
dotnet run                # Build and run (first run triggers setup wizard)
dotnet clean              # Clean build artifacts
```

## Preferences

Settings are persisted in `preferences.json` (next to the executable). On first run, a setup wizard guides the user through base URL, API key, and model selection. Preferences can be changed at runtime via `/preferences set <key>=<value>`.

Preference keys: `base_url`, `api_key`, `model`, `stream`, `tps`

## Dependencies

- `OpenAI` — OpenAI .NET SDK (used for any OpenAI-compatible `/v1` endpoint)
- `Microsoft.Extensions.AI.OpenAI` — Microsoft Extensions AI abstraction providing `IChatClient`

## Architecture

```
Program.cs          — Entry point, preferences loading, first-run wizard, REPL loop
ChatSession.cs      — Mutable session state (base URL, API key, model, client, history, preferences)
CommandHandler.cs   — Slash command processing (/bye, /info, /help, /clear, /models, /switch, /preferences)
ConsoleHelper.cs    — Static helpers for colored console output
Preferences.cs      — Load/save preferences to preferences.json
ModelService.cs     — REST client for /v1/models endpoint (list models, health check)
```

The app uses `Microsoft.Extensions.AI.IChatClient` (via OpenAI SDK) for chat interaction and maintains a `List<ChatMessage>` as conversation history. The `ChatSession` class holds mutable state so commands like `/switch` and `/preferences set` can reconnect mid-session.
