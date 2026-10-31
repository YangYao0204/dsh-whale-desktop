# 桌面小鲸鱼（DSH Whale Pet · Desktop）

把 **DeepSeek Harness（DSH）** Web 界面右下角那只小鲸鱼，搬到 Windows 桌面上。

透明无边框、始终置顶、独立进程 —— **DSH 关掉它还在**。

| 余额 + 峰谷 | 骚话 |
|---|---|
| ![](docs/balance.png) | ![](docs/quip.png) |

| 等你决策 | 每轮消耗 |
|---|---|
| ![](docs/waiting.png) | ![](docs/turncost.png) |

## 特性

- **外观照搬插件**：立绘占容器 59.45%、贴右下角；思考泡泡用 `dsh-whale-widget` 的 `FRAME_CLASSIC_BIG_D` 原路径（viewBox 1026×700、`#203170` 18 宽描边、尾巴 + 两个小圆点）；字号/颜色/行高按插件的模块配置换算。
- **点击分工**：点鲸鱼出余额卡（余额 / 今日已用 / 峰谷倒计时），点气泡换一句骚话（85 条，按权重抽、不连续重复，长句自动换行 + 缩字号）。
- **峰谷实时**：工作日 9–12 / 14–18 为峰，其余、周末、法定节假日为谷；倒计时跨天不清零。
- **每轮消耗**：读 DSH 账本里的逐轮事件，回合结束自动弹「本轮消耗 ¥x / n tokens」。
- **等你决策**：读 DSH 会话缓存里的 `questions.active`，有挂起问题就显示「正在等待老大回答」并置顶。
- **消耗 API 时自动出现**：`~/.dsh/.dshw-turn.json` 的 `seq` 一变就置顶冒出来（默认开，可在菜单关）。
- **按压手感**：整只挂件 `scaleY .88 / scaleX 1.05`（原点在底部中心），SmoothDamp 临界阻尼 + 1ms 定时器精度。
- **贴边吸附 + 贴左镜像**：拖到屏幕四边 26px 内自动吸附；贴左时只镜像立绘与泡泡，**文字保持正向可读**。
- **手形光标**：悬停是张开的手，按下变握拳（运行时生成 32/48/64 多尺寸 `.cur`）。
- **按压音效**：`Ya1/Ya2`（小黄鸭组）或 `D1/D2`（音效 1 组），时序按插件：按下放 Ya1，松开的 Ya2 与 Ya1 尾音轻微交叠。
- 透明区域**点击穿透**（只有不透明像素吃鼠标），位置/大小/翻转状态持久化，单实例运行。

## 运行

1. 双击 `WhalePet.exe`（首次运行会写「开机自启」，可在右键菜单关）。
2. 需要 **.NET Framework 4.8**（Windows 10/11 自带）。

菜单：每轮消耗提示 · 直连官网刷新余额 · 消耗 API 时自动出现 · 刷新余额 · 大小 · 始终置顶 · 开机自启 · 在桌面创建快捷方式 · 回到右下角 · 退出。

## 构建

素材（立绘 + 音效）来自 `dsh-whale-widget` 插件，**不随本仓库分发**（见 [NOTICE.md](NOTICE.md)）。先在本机把素材准备好，再编译：

```powershell
# 1. 从已安装的 dsh-whale-widget 复制素材到 assets/
powershell -ExecutionPolicy Bypass -File tools\prepare-assets.ps1

# 2. 编译（csc 随 .NET Framework 4.8 自带）
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

产物是 `dist\WhalePet.exe`（素材已嵌入 exe）。

## 数据来源

| 数据 | 位置 | 说明 |
|---|---|---|
| 余额 / 今日已用 / 逐轮事件 | `%USERPROFILE%\.dsh\.dshw-usage.json` | 由 `dsh-whale-widget` 插件写入，本程序只读 |
| 回合序号 | `%USERPROFILE%\.dsh\.dshw-turn.json` | 每轮 +1，用于「开始消耗就冒出来」 |
| 待回答的问题 | `%USERPROFILE%\.dsh\storages\session_projcache\sessions\*.json` | 读最新的那个，看 `userQuestions.val.questions.active` |
| 插件设置 | `%USERPROFILE%\.dsh\.dshw-size.json` | `sound` / `pressOn` / `vol` / `soundSet` / `turnCostOn` / `turnCostCloseMs` |
| API Key（可选） | `%USERPROFILE%\.dsh\.credentials.yaml` | 只有开启「直连官网刷新余额」时才用来请求 `api.deepseek.com/user/balance` |

全部只读（除了自己的 `whale-pet.json` 存位置）；不联网，除非你打开「直连官网」。

调试用环境变量：`DSH_WHALE_USAGE` / `DSH_WHALE_TURN` / `DSH_WHALE_SESSIONS` / `DSH_WHALE_FRAMESHOT` / `DSH_WHALE_POS` / `DSH_WHALE_QUIP`。

## 已知限制

- 「直连官网余额」在受限网络里会失败，失败自动退回读账本，不影响使用。
- 余额预警阈值存在 DSH 网页的 localStorage 里，桌面版读不到，未实现。
- 法定节假日表内置的是 **2026 年**（与插件 `HOLIDAY_VALLEY` 一致），次年需要补。
- 音效走 MCI（按下）+ Windows Media Player COM（松开）；没有媒体组件的 Windows N 版会静音。

## 许可

代码 MIT。立绘 / 音效的版权不属于本项目，见 [NOTICE.md](NOTICE.md)。
