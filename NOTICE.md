# 第三方素材说明（NOTICE）

本仓库的**代码**以 MIT 许可发布（见 [LICENSE](LICENSE)）。但**立绘与音效不属于本仓库**，也不在 MIT 覆盖范围内：

| 素材 | 来源 | 许可 |
|---|---|---|
| 立绘 `DSniang1.png` → `assets/whale.png` | DSH 插件 [`dsh-whale-widget`](https://github.com/MeteorNOX/DeepSeek-Balance-Whale-Widget) 的 `assets/DSniang1.png` | 该插件 `PROVENANCE.md` 明确写：美术素材按 **as-is** 随插件分发，**不授予再许可**，仅供运行该插件使用 |
| 音效 `Ya1.mp3` / `Ya2.mp3` / `D1.mp3` / `D2.mp3` | 同上，`assets/` 目录 | 同上 |
| 泡泡形状 / 尺寸 / 配色常量 | 同上，`assets/whale-widget.js` | 代码部分为 MIT |

因此本仓库**不包含**这些素材：`assets/` 被 `.gitignore` 排除，构建前需用 `tools/prepare-assets.ps1` 从本机已安装的插件目录复制。

如果你要自己发布一个包含素材的分发版（例如 Release 里的 exe），请自行确认是否符合原作者的许可；最稳妥的做法是让使用者自己跑 `prepare-assets.ps1`，或直接引导他们去安装原插件。
