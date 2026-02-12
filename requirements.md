# tuichat Requirements

## Overview

A terminal-based chat application that connects to the OpenAI API and provides an interactive REPL (Read-Eval-Print Loop) for conversational interaction.

## Functional Requirements

### REPL Loop
- Display a prompt with a timestamp: `[yyyy-MM-dd hh:mm:ss tt] You> ` (e.g., `[2026-02-12 03:45:12 PM] You> `)
- Send the user's message to the OpenAI Chat Completions API
- Print the model's response to the console
- Repeat until the user exits

### Conversation Context
- Maintain conversation history so the model has context of prior messages
- Send the full message history with each API request

### Slash Commands
User can enter commands prefixed with `/` instead of chat messages:

- `/bye` — Exit the application gracefully with a farewell message
- `/info` — Display session information:
  - Current model name
  - Number of messages sent by the user
  - Number of messages received from the model
- `/help` — List all available slash commands with descriptions
- `/clear` — Clear the conversation history and start fresh

### Exit Handling
- Support exiting via `/bye` or Ctrl+C
- Exit gracefully with a farewell message

### OpenAI Integration
- Use the OpenAI Chat Completions API
- Read the API key from the `OPENAI_API_KEY` environment variable
- Use a sensible default model (e.g., `gpt-4o`)
- Display a clear error if the API key is missing or the API returns an error

## Non-Functional Requirements

### Console Coloring
- User input prompt and text: one distinct color (e.g., green)
- Model response text: a different distinct color (e.g., cyan)
- Error messages: red
- System messages (startup banner, exit message): yellow

### User Experience
- Display a startup banner with the app name and instructions (how to exit, how to see commands via `/help`)
- Show a loading/thinking indicator while waiting for the API response
- Support multi-line user input (e.g., via a trailing `\` or a special delimiter)

### Configuration
- API key sourced from `OPENAI_API_KEY` environment variable
- Model selection configurable via `OPENAI_MODEL` environment variable with a default fallback

### Error Handling
- Handle network errors and API failures gracefully without crashing
- Display user-friendly error messages and allow the user to continue the session
