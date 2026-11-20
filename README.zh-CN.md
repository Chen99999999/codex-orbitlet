# Orbitlet · 小环

[English](README.md) · [下载 Windows 版](https://github.com/Chen99999999/codex-orbitlet/releases/latest)

**跟随 AI 编程工具的 Windows 额度桌宠。**

白底黑字、灰阶双环，标准 136 × 136px，小号 116 × 116px。双环根据额度来源显示时间窗口或积分，API 余额以金额显示。Codex 底部分别显示 5h 结束时间和本周额度结束日期。

卡片里的小猫放大至 28px（小号 24px），在正方形卡片内由重力、摩擦与碰撞驱动，会跳上圆弧、攀爬、滑行、失去支撑后落下并缓冲停稳，也会张望、坐下、舔爪、伸懒腰和打盹，路线避开百分比和时间文字。折叠后暂停，开启「减少动态效果」后静止。

银灰虎斑猫使用 AI 生成的照片质感素材，共十二帧，并非实拍视频或骨骼动画。透明图片和帧坐标随程序保存在本地 `assets/`，运行时无需下载猫咪图片；源码的 `web/assets/` 中保留原始素材和生成提示词。

## 选择跟随窗口与额度来源

- 默认锁定在 Codex。右键桌宠或托盘图标，在「跟随窗口」里切换到 Qoder、Cursor、Windsurf、VS Code 等。
- 小环随目标窗口移动和缩放。切走或最小化时自动隐藏。
- 「额度来源」可以独立选择 Codex、Claude Code、Qoder、智谱、MiniMax、OpenRouter、DeepSeek 和 Kimi API。具体区服和设置见[接入指南](docs/PROVIDERS.md)。

这是独立社区项目，不是 OpenAI 官方产品，也未获得 OpenAI 背书。本版本支持 Windows。

## 使用

1. 在 [Releases](https://github.com/Chen99999999/codex-orbitlet/releases/latest) 下载 Windows ZIP 并完整解压。
2. 运行 `Orbitlet.exe`，从系统托盘选择窗口与额度来源。
3. 希望以后自动带起小环，运行一次 `Enable-Follow.cmd`。

便携版使用系统 .NET Framework。Qoder 额度适配需安装 Node.js 和官方 SDK。目前的 exe 尚未签名。

绑定模式下，目标应用不在前台时小环隐藏。需要独立桌面模式时，先退出当前小环，再运行 `Launch-Standalone.cmd`。

## 操作

- 拖动：调整在目标窗口内的位置。
- 双击：折叠 / 展开。
- 右键：锁定窗口、小号模式、刷新、自动启动和退出。
- F5：立即读取真实额度。
- Esc：手动隐藏。
- Ctrl + Alt + O 或托盘图标：切换可见性。

取消右键「锁定在目标窗口」可切换到独立桌面模式。运行 `Disable-Follow.cmd` 或取消「随目标应用启动 / 关闭」，可移除当前用户启动项并停止助手。

自动启动助手每两秒检查一次目标窗口。若移动程序目录，请先从旧位置停用，再在新位置启用。

## 数据来自哪里

Codex 通过已安装的官方 CLI 调用只读 `account/rateLimits/read`，按 300 / 10080 分钟匹配窗口。其他平台使用各自的只读查询或状态栏，详见[接入指南](docs/PROVIDERS.md)。

- 每 60 秒读取一次，不创建聊天或发起模型推理。
- 未知额度显示 `--`；失败保留上次成功数据和结束时间。
- 「本周」按各平台返回的时间窗口显示。API 金额和额度比例分别显示。
- 账户型 API 只读取所选来源的环境变量。CLI 登录由平台处理。

可从右键菜单打开实时浏览器预览，使用同一份内存数据。预览仅监听本机 loopback，程序退出后地址失效。直接打开 HTML 只会显示未连接状态，不会填入演示额度。

更多信息：[隐私说明](PRIVACY.md)、[官方协议](https://learn.chatgpt.com/docs/app-server#6-rate-limits-chatgpt)。

## 构建

```powershell
powershell -NoProfile -File .\scripts\build.ps1
powershell -NoProfile -File .\scripts\test.ps1
powershell -NoProfile -File .\scripts\package.ps1
```

输出位于 `dist/windows`。65 项断言覆盖额度换算、未知值、桌面识别、CLI 排除、可见性、位置约束与窗口移动。原生集成测试使用自己的屏幕外窗口，检查窗口归属、移动、缩放、最小化、恢复和关闭，前台窗口身份由测试模拟；不会操作 Codex，也不需要真实账户。

小猫导航另外检查了 86,400 步随机移动、十四种动作、滑动能量、支撑脱离与碰撞边界和 200 次尺寸切换，确保避开文字显示区域。

欢迎改进稳定性、无障碍、本地化与多显示器适配。MIT 开源。
