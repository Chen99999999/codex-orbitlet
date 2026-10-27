# Privacy

Orbitlet is a local Windows application. It has no telemetry or application-owned cloud backend.

## Authentication and quota

The installed Codex CLI owns authentication. Orbitlet starts a child `codex app-server` process over standard input/output and calls `initialize` and `account/rateLimits/read`. It does not read authentication files, copy tokens, start inference turns, send account emails or consume reset credits.

The CLI may contact OpenAI and manage its own authentication state according to its existing configuration. Orbitlet retains only normalized percentages, reset timestamps and the time/status of the latest read in memory. Raw RPC messages are not saved.

## Window attachment

Orbitlet checks the foreground window, official Codex process/package identity and client-window geometry to position its own window and control its own visibility. It does not inspect page content, capture your screen, inject code into Codex or modify the official app.

## Local preview

An optional HTTP preview listens on `127.0.0.1` on an ephemeral port. It serves the bundled preview and a minimal read-only quota snapshot. Host/Origin checks, a restrictive content policy and no CORS reduce exposure to other websites. Other local software running as your user may still access a loopback service; it is not an isolation boundary against malicious local applications.

## Files

Runtime preferences, the local preview URL and error summaries are kept beside the portable executable. Optional diagnostic flags write a small local binding-status file. None belong in Git or bug-report attachments without review.

Enabling automatic start creates a current-user Startup shortcut. Disabling it removes the shortcut and stops the helper. No Windows service, scheduled task or machine-wide setting is installed.
