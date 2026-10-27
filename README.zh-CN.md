# Orbitlet · 小环

[English](README.md) · [下载 Windows 版](https://github.com/Chen99999999/codex-orbitlet/releases/latest)

**只陪着 Codex 的极简额度伴侣。**

两条圆弧，分别显示 5 小时、本周剩余百分比，底部显示最后成功更新时间。白底黑字、灰阶双环，标准 100 × 102px，小号 88 × 90px。

## 和 Codex 窗口绑定

- 默认锁定在 Codex 窗口内部，可拖动调整相对位置。
- 随 Codex 移动和缩放，位置不会散落到桌面。
- 切换到其他应用、最小化或关闭 Codex 时自动隐藏，回到 Codex 时再显示。
- 可启用随 Codex 启动；额度窗退出时会停止额度读取。
- 无需修改官方 Codex 程序、原有快捷方式或配置。

这是独立社区项目，不是 OpenAI 官方产品，也未获得 OpenAI 背书。本版本支持 Windows。

## 使用

1. 在 [Releases](https://github.com/Chen99999999/codex-orbitlet/releases/latest) 下载 `Orbitlet-v0.5.0-windows.zip` 并完整解压。
2. 确保本机官方 Codex 已安装，并已登录 ChatGPT 账户。
3. 打开 Codex，再运行 `Orbitlet.exe`。
4. 希望以后自动带起小环，运行一次 `Enable-Codex-Follow.cmd`。

运行不需要 Node.js、Python 或额外安装依赖，使用系统 .NET Framework。目前的 exe 尚未签名。

默认绑定模式下，Codex 不在前台时小环隐藏。需要独立桌面模式时，先退出当前小环，再运行 `Launch-Standalone.cmd`。

## 操作

- 拖动：调整在 Codex 窗口内的位置。
- 双击：折叠 / 展开。
- 右键：锁定窗口、小号模式、刷新、自动启动和退出。
- F5：立即读取真实额度。
- Esc：手动隐藏。
- Ctrl + Alt + O 或托盘图标：切换可见性；绑定模式仍尊重 Codex 的前台状态。

取消右键「锁定在 Codex 窗口」可切换到独立桌面模式。运行 `Disable-Codex-Follow.cmd` 或取消「随 Codex 启动 / 关闭」，可移除当前用户启动项并停止助手。

自动启动助手在后台每两秒检查一次官方桌面窗口，排除 CLI 和 app-server。额度窗主动退出后不会在同一次 Codex 会话中反复弹回。若移动程序目录，请先从旧位置停用，再在新位置启用。

## 数据来自哪里

通过已安装的官方 CLI 调用只读 `account/rateLimits/read`。优先选择 `rateLimitsByLimitId.codex`，按 300 / 10080 分钟匹配窗口，用 `100 - usedPercent` 显示剩余比例。

- 每 60 秒读取一次，不创建聊天或发起模型推理。
- 未知额度显示 `--`；失败保留上次成功数据和时间。
- 「本周」对应官方滚动 7 天窗口，不是自然周。
- 不将 token 数、自定义 API 余额或费用估算伪装成官方剩余额度。
- 程序不读取或复制登录凭证，不读取对话或项目源码。

可从右键菜单打开实时浏览器预览，使用同一份内存数据。预览仅监听本机 loopback，程序退出后地址失效。直接打开 HTML 只会显示未连接状态，不会填入演示额度。

更多信息：[隐私说明](PRIVACY.md)、[官方协议](https://learn.chatgpt.com/docs/app-server#6-rate-limits-chatgpt)。

## 构建

```powershell
powershell -NoProfile -File .\scripts\build.ps1
powershell -NoProfile -File .\scripts\test.ps1
powershell -NoProfile -File .\scripts\package.ps1
```

输出位于 `dist/windows`。65 项断言覆盖额度换算、未知值、桌面识别、CLI 排除、可见性、位置约束与窗口移动。原生集成测试使用自己的屏幕外窗口，检查窗口归属、移动、缩放、最小化、恢复和关闭，前台窗口身份由测试模拟；不会操作 Codex，也不需要真实账户。

欢迎改进稳定性、无障碍、本地化与多显示器适配。MIT 开源。
