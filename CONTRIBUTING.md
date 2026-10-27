# Contributing

Keep Orbitlet small and quiet. Reliability and clear state handling take priority over adding dashboards or notifications.

1. Build and run `scripts/test.ps1` on Windows.
2. Use synthetic quota fixtures in tests and screenshots. Never add login files, environment secrets, raw RPC responses, personal paths or conversations.
3. Preserve duration-based quota mapping and unknown-vs-zero behavior.
4. For attachment changes, check foreground changes, minimizing, moving, resizing and multiple DPI scales. Never modify the Codex app to make a test pass.
5. Include a concise description and before/after images for visual changes. Identify illustrative values as sample data.

Please open an issue before substantial changes to the product's scope. Pull requests do not require signing into Codex for CI tests.
