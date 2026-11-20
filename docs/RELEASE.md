## Orbitlet v0.7.0

Orbitlet can follow your editor and show quota from a separate source. Choose both from the Windows tray.

- 12 Windows targets, including Codex, Qoder, Claude Desktop, Cursor, Windsurf, VS Code, TRAE and Windows Terminal.
- 12 quota sources including regional editions. Codex and Claude Code show subscription windows, Qoder shows Credits, and GLM and MiniMax show their own quota limits. OpenRouter shows key budget and usage. DeepSeek and Kimi API report wallet balances.
- The portable package includes optional Qoder SDK setup and Claude Code status line helpers. The two rings display percentages where a provider supplies a finite limit.
- Offline fixtures cover quota parsing, missing values, expired windows, and process identity. The new providers require their own signed-in CLI, status line, or API key for a live result.

### 中文

- 右键托盘可分别选择「跟随窗口」和「额度来源」。
- 增加 Qoder、Claude Code、智谱 GLM、MiniMax、OpenRouter、DeepSeek 和 Kimi API，包含对应地区版本。
- Cursor、Windsurf 等编辑器可搭配所接入的额度来源，或选择「仅桌宠」。

Extract the full Windows ZIP and run `Orbitlet.exe`. See the [provider guide](https://github.com/Chen99999999/codex-orbitlet/blob/v0.7.0/docs/PROVIDERS.md) to connect your source.

Independent community project. Please omit credentials and personal account data from issue reports.
