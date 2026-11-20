# 额度来源与窗口跟随 / Sources and window targets

Orbitlet 是 Windows 桌宠。右键卡片或系统托盘图标，可以分别选择「额度来源」和「跟随窗口」。例如在 Cursor 用 MiniMax 的订阅密钥，可选择 Cursor 作为窗口，再选择对应地区的 MiniMax 额度来源。只需要桌宠时，选择「仅桌宠」。

Orbitlet runs on Windows. Right-click the card or tray icon to choose the quota source under **额度来源** and the window under **跟随窗口**. For example, you can follow Cursor and show your MiniMax subscription quota. Choose **仅桌宠** for the pet without account data.

可跟随 Codex、Qoder、QoderWork、Claude Desktop、Cursor、Windsurf、VS Code（含 Insiders）、TRAE、Kiro、Antigravity、Zed 和 Windows Terminal。选择终端会跟随整个 Terminal 窗口。同一个窗口可以显示不同来源的额度。工具自有的个人订阅如果未列在下表，可选「仅桌宠」或查看实际接入的模型提供方。

| 来源 / Source | 显示 / Display | 接入 / Setup |
| --- | --- | --- |
| Codex | 5h 和本周剩余 / 5h and weekly remaining | 登录官方 CLI / Sign in to the official CLI |
| Claude Code | 5h 和本周剩余 / 5h and weekly remaining | 下方状态栏设置 / Status line setup below |
| Qoder, Qoder CN | 套餐、附加积分，适用时显示组织包 / Plan, add-on and eligible organization credits | 下方 SDK 设置 / SDK setup below |
| Z.ai GLM, 智谱 GLM | 模型和 MCP 剩余比例 / Model and MCP remaining percent | `ZAI_API_KEY` or `BIGMODEL_API_KEY` |
| MiniMax, MiniMax CN | 文本模型 5h 和本周剩余 / Text model 5h and weekly remaining | `MINIMAX_API_KEY` or `MINIMAX_CN_API_KEY` with a subscription key |
| OpenRouter | Key 余额占限额比例、累计用量 / Key remaining versus cap, plus cumulative usage | `OPENROUTER_API_KEY` |
| DeepSeek | API 总余额、赠金 / API total and granted balances | `DEEPSEEK_API_KEY` |
| Kimi API CN, Kimi API | API 可用余额、现金余额 / API available and cash balances | `MOONSHOT_API_KEY` or `MOONSHOT_GLOBAL_API_KEY` |

For API sources, set the listed variable in Windows **User environment variables**. Select the matching region and refresh Orbitlet. Each key is sent to its provider's quota or balance endpoint. Currency amounts appear as text. An absent or unlimited percentage appears as `--`.

API 来源使用 Windows「用户环境变量」中的对应密钥。选择区服并刷新后，小环只向对应平台的余额或额度接口发送该密钥。余额以金额显示，缺失或无上限的百分比显示 `--`。

## Qoder

Install Node.js 18 or newer and sign in to the matching Qoder CLI (`qodercli login` or `qoderclicn login`). The SDK can also use `QODER_PERSONAL_ACCESS_TOKEN` or `QODERCN_PERSONAL_ACCESS_TOKEN` supplied in Orbitlet's environment. Run the corresponding command in the extracted Orbitlet directory, then select the source.

安装 Node.js 18 或更新版本，先登录对应版本的 CLI。下列安装脚本只需运行一次，之后即可在「额度来源」里选择对应版本。

```powershell
npm.cmd install --prefix .\integrations\qoder @qoder-ai/qoder-agent-sdk@1.0.60 zod@4
npm.cmd install --prefix .\integrations\qoder-cn @qodercn-ai/qodercn-agent-sdk@1.0.60 zod@4
```

The adapter queries `getUsageInfo()` without sending a user message. Qoder reports the plan expiration date when available. The add-on bucket may be absent. [Qoder SDK](https://docs.qoder.com/cli/sdk/cost-usage) · [Qoder CN SDK](https://docs.qoder.cn/cli/sdk/references-typescript)

## Claude Code

Use the following object as your Claude Code `statusLine` setting. Replace the path with the full path to the executable. If you already have a status line, pipe the incoming JSON to the Orbitlet command as part of your existing command. Select **Claude Code** in Orbitlet. The values arrive through the next eligible Claude Code session update.

把以下对象放入 Claude Code 的 `statusLine` 设置，并换成实际程序路径。已有状态栏时，可以让现有命令也把输入 JSON 交给小环。然后在 Orbitlet 选择 Claude Code，等待正常会话更新。该数据适用于官方状态栏提供订阅额度的会话。

```json
{
  "statusLine": {
    "type": "command",
    "command": "\"C:/Apps/Orbitlet/Orbitlet.exe\" --claude-statusline"
  }
}
```

The helper saves only the two quota windows to `claude-usage.settings` beside the executable. Orbitlet discards expired windows and treats an eight-hour-old file as stale. [Claude Code status line](https://code.claude.com/docs/en/statusline#rate-limit-usage)

## Provider references

[Z.ai usage plugin](https://docs.z.ai/devpack/extension/usage-query-plugin) · [MiniMax quota client](https://github.com/MiniMax-AI/cli/blob/main/src/sdk/quota/index.ts) · [OpenRouter key info](https://openrouter.ai/docs/api/api-reference/api-keys/get-current-key) · [DeepSeek balance](https://api-docs.deepseek.com/zh-cn/api/get-user-balance/) · [Kimi CN balance](https://platform.kimi.com/docs/api/balance) · [Kimi global balance](https://platform.kimi.ai/docs/api/balance)
