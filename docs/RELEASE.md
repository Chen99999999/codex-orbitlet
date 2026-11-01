## Orbitlet v0.6.0

A square quota card with a small cat that plays on the arcs.

- Square layout: 136 × 136 px standard and 116 × 116 px compact, with centered 5-hour and weekly percentages.
- Two footer lines now show the local 5h end time and weekly end date. Hover for full dates and times.
- Fixed-step 2D motion: gravity, friction, climbing traction, arc contact, support loss, wall collisions and damped landings.
- Fourteen cat actions, with walking frames tied to distance traveled. The cat avoids the percentage and end-time text.
- No particles or hollow endpoint markers. Subtle arc highlights remain; folding, hiding and reduced-motion settings pause animation.
- Twelve AI-generated photographic-style cat poses are bundled locally, with their source atlas and prompt. This is frame animation, not real filmed footage or a skeletal simulation.
- Native build and offline checks cover quota/window behavior plus 86,400 cat simulation steps and 200 size switches.

Codex window attachment, optional automatic start and official read-only quota integration remain available.

### 中文

- 恢复正方形卡片：标准 136 × 136px，小号 116 × 116px。
- 底部分两行显示「5h 结束时间」「本周结束日期」，替代刷新时间。
- 小猫使用固定步长二维物理运动，可跳上圆弧、攀爬、受重力滑落并缓冲落地，避开数据文字。
- 保留十四种动作，步态随移动距离变化；取消粒子和空心端点。
- 猫咪为本地 AI 写实帧素材，非实拍视频。减少动态效果、折叠和隐藏时暂停动画。

Download the Windows ZIP and extract the whole folder, including `assets/`. Open Codex and run `Orbitlet.exe`. Keep `Enable-Codex-Follow.cmd` available for optional automatic start. The binary is unsigned; Windows .NET Framework and a signed-in official Codex installation are required.

Independent community project, not an official OpenAI product. Please omit credentials and personal account data from issue reports.
