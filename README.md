# Hauling Post Plus

![搬运站扩容 / Hauling Post Plus](mod/thumbnail.jpg)

[中文说明](#中文说明) · [English](#english)

Hauling Post Plus 是一个为 Timberborn 搬运站增加大容量员工控制的模组。当前版本为 **0.1.3**，面向 Timberborn **1.1.2.4**。

Hauling Post Plus adds high-capacity worker controls to Timberborn's Hauling Posts. The current version is **0.1.3**, targeting Timberborn **1.1.2.4**.

## 中文说明

### 功能

- 将神尾和铁牙搬运站的员工容量从 10 提高到 1000。
- 每座搬运站可以独立设置期望员工数，范围为 1～1000。
- 提供滑块、精确数字输入和 `10 / 50 / 100 / 500 / 1000` 五个快捷按钮。
- `10` 按钮用于快速回到原版容量上限；新建搬运站的原版初始期望人数仍为 5。
- 保留原版的暂停、工作优先级、海狸/机器人类型切换以及加减按钮。
- 搬运站不再生成成百上千个员工头像，而是保留简洁的人数摘要，避免建筑面板超出屏幕。
- 包含简体中文、繁体中文和英文界面文本，跟随游戏语言设置。

本模组修改的是“期望员工数”。它不会增加城镇人口，也不会强制岗位立即招满；实际到岗人数仍取决于可用劳动力、工作时间和工作优先级。

### 语言设置

在游戏设置中选择简体中文、繁體中文或 English，并按游戏提示重启。模组面板会自动使用对应语言，无需额外语言包或语言切换模组。

- 简体中文：`zhCN`，面板标题为“搬运站扩容”。
- 繁體中文：`zhTW`，面板標題為「搬運站擴容」。在遊戲設定中選擇繁體中文並重新啟動遊戲即可使用。
- English：`enUS`，面板标题为“Hauling Post Plus”。

标题、人数摘要、拖动预览、操作提示和容量警告均提供三种语言。此功能针对游戏内面板；工坊标题、介绍和缩略图不会随游戏语言自动改变。语言切换不改变存档中的员工数。

### 依赖

唯一必需的第三方模组是 [Harmony 2.4.1 或更新版本](https://steamcommunity.com/sharedfiles/filedetails/?id=3284904751)。不需要 Mod Settings、TimberAPI 或 TimberUI。

### 安装

1. 安装并启用 Harmony。
2. 将发布包解压到 Timberborn 的本地模组目录，确保目录结构为 `HaulingPostPlus/manifest.json`，而不是额外嵌套一层文件夹。
3. 启动游戏，在 Mod Manager 中启用 `Hauling Post Plus` 和 `Harmony`，然后按游戏提示重启。

Windows 默认本地模组目录为：

```text
Documents/Timberborn/Mods
```

从 Steam 创意工坊订阅时无需手动解压；发布后的工坊页面还需要把 Harmony 标记为“必需物品”。

### 存档与卸载

模组继续使用游戏原生的 `Workplace.DesiredWorkers` 字段保存每座搬运站的人数，不创建额外存档组件。

- 启用后，旧搬运站原有的期望人数会保留。
- 停用模组后重新载入存档，超过原版上限的期望人数预计会被限制回 10。
- 存档可能记录曾启用过的模组，因此停用后可能出现缺失模组提示。

请先使用新测试档或存档副本验证，不要直接覆盖唯一的正式存档。

### 兼容性和性能

- 已为原版员工面板和 Second Shift 1.1.1.0 的替代员工面板提供头像隐藏适配；Second Shift 不是必需依赖。
- 当前适配保留 Second Shift 的双班制控件，但不重写它的班次数学逻辑。开启双班制后，Second Shift 会改变总岗位上限、人数步长和保存行为；本模组的滑块仍只设置 1～1000 的总期望人数。
- 完全替换员工面板或同时修改搬运站容量的其他模组可能需要单独适配。
- 1000 是可设置的岗位上限，不是性能保证。大量实际工人仍会增加游戏的寻路、任务分配和模拟负载。

## English

### Features

- Raises the worker capacity of both Folktails and Iron Teeth Hauling Posts from 10 to 1000.
- Stores a separate desired worker count for every Hauling Post, from 1 to 1000.
- Provides a slider, precise numeric input, and `10 / 50 / 100 / 500 / 1000` preset buttons.
- The `10` preset quickly returns a building to the vanilla capacity. Newly built Hauling Posts still start with the vanilla desired count of 5.
- Keeps the vanilla pause control, workplace priority, beaver/bot selection, and plus/minus buttons.
- Replaces the potentially huge Hauling Post portrait grid with a compact worker-count summary so the entity panel remains usable.
- Includes Simplified Chinese, Traditional Chinese, and English localization, following the game's language setting.

The mod changes the *desired* worker count. It does not create population or instantly fill jobs. Actual staffing still depends on available workers, working hours, and workplace priority.

### Language settings

Choose Simplified Chinese (`zhCN`), Traditional Chinese (`zhTW`), or English (`enUS`) in the game's settings and restart when prompted. The mod panel follows that setting automatically; no extra language pack or language-switching mod is required.

All panel labels, staffing summaries, drag previews, hints, and capacity warnings are translated. This applies to the in-game panel, not the Workshop title, description, or thumbnail. Changing language does not change saved worker counts.

### Requirements

The only required third-party mod is [Harmony 2.4.1 or newer](https://steamcommunity.com/sharedfiles/filedetails/?id=3284904751). Mod Settings, TimberAPI, and TimberUI are not required.

### Installation

1. Install and enable Harmony.
2. Extract the release into Timberborn's local Mods directory. The resulting path must contain `HaulingPostPlus/manifest.json` without an extra nested folder.
3. Start the game, enable `Hauling Post Plus` and `Harmony` in Mod Manager, and restart when prompted.

The default local Mods directory on Windows is:

```text
Documents/Timberborn/Mods
```

No manual extraction is needed for a Steam Workshop subscription. The Workshop item must also list Harmony as a required item.

### Saves and removal

The mod uses Timberborn's native `Workplace.DesiredWorkers` field for each building and does not add a custom save component.

- Existing Hauling Posts keep their current desired count when the mod is enabled.
- After disabling the mod and loading a save, desired counts above the vanilla limit are expected to be clamped back to 10.
- Save metadata may remember enabled mods, so a missing-mod warning may appear after removal.

Test with a new save or a copy first. Do not overwrite your only production save while evaluating the mod.

### Compatibility and performance

- Portrait suppression supports the vanilla workplace panel and the replacement panel used by Second Shift 1.1.1.0. Second Shift is optional.
- Its two-shift controls remain available, but this mod does not replace Second Shift's staffing rules. With two shifts enabled, Second Shift changes total capacity, adjustment steps, and save behavior; this mod's slider still controls a total desired count from 1 to 1000.
- Other mods that fully replace the workplace panel or modify Hauling Post capacity may require a dedicated compatibility adapter.
- A capacity of 1000 is not a performance guarantee. Large numbers of actual workers still add pathfinding, job-assignment, and simulation cost.

## Development / 开发

The plugin targets .NET Standard 2.1 and references locally installed Timberborn and Harmony assemblies without redistributing them. Building is supported from Windows PowerShell:

本插件以 .NET Standard 2.1 为目标，只引用本机安装的 Timberborn 与 Harmony 程序集，不会把它们打进发行包。Windows PowerShell 构建命令：

```powershell
# Download and verify the pinned project-local .NET SDK when needed.
# 首次需要时下载并校验项目独立的固定版本 .NET SDK。
.\scripts\setup-sdk.ps1

# Compile, run regression checks, and create a ZIP in dist/.
# 编译、执行回归检查并在 dist/ 生成 ZIP。
.\scripts\build.ps1

# Also install into the local Mods directory; Timberborn must be closed.
# 同时安装到本地 Mods 目录；执行时必须完全退出 Timberborn。
.\scripts\build.ps1 -Install
```

The automated suite checks worker-count boundaries, preset values, Blueprint merges, current game API contracts, all three localization files and their format placeholders, package contents, and Harmony behavior against both vanilla and Second Shift-style mock panels.

自动测试覆盖人数边界、预设值、Blueprint 合并、当前游戏 API 入口、三种语言的翻译文件与格式占位符、发行包内容，以及原版与 Second Shift 风格模拟面板的 Harmony 行为。

## Project layout / 项目结构

```text
mod/        Runtime manifest, Blueprint patches, and localization
src/        Mod source code
tests/      Offline and Harmony regression checks
scripts/    Reproducible SDK setup and build/package scripts
```

Game binaries, Harmony binaries, build outputs, private notes, logs, saves, screenshots, and development documents are intentionally excluded from version control.

游戏文件、Harmony 文件、构建产物、私人笔记、日志、存档、截图及开发文档均不会纳入版本控制。
