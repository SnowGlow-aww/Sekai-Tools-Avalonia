# Sekai Tools

> 状态：正在进行 **Avalonia 路线** 的跨平台重构，目标 Windows + macOS。`SekaiText` 仓库的能力会合并到本仓库。详见 [`docs/avalonia-merge-plan.md`](./docs/avalonia-merge-plan.md)。

## 主要功能

- 根据三周年后的 PJSK 游戏内录剧情文件和游戏内素材文件自动生成适配风格的字幕文件
    - 分辨率自适应
    - 需要视频固定帧速率
- 跨平台翻译工作台
    - 本地剧本 + 翻译文件模式
    - `SekaiPlatform` 平台模式（远端原文 / 译文版本管理 / 上传）
- 剧本文件下载

## 仓库结构（终态）

```text
SekaiTools/
├── SekaiToolsApp/        # Avalonia 桌面 App，唯一 UI 入口（建设中）
├── SekaiToolsCore/       # 跨平台 OCR + 字幕生成
├── SekaiToolsBase/       # 跨平台剧本与解析
├── SekaiDataFetch/       # 跨平台远端数据
├── SekaiToolsPlatform/   # SekaiPlatform 客户端 + 服务层（拆自 MAUI 工作台）
├── docs/
└── SekaiTools.sln
```

迁移完成后从解决方案中移除：

- `SekaiToolsGUI`（WPF / Windows-only，已归档，作为参考保留在仓库目录）
- `SekaiToolsMauiText`（MAUI 过渡产物，已归档，作为参考保留在仓库目录）
- `Updater`（已归档，后续由 `Velopack` 路线替代）

## 当前进度

请见 [`docs/avalonia-merge-plan.md`](./docs/avalonia-merge-plan.md) 中的里程碑章节。

## 开发说明

- 目标 framework：`net8.0`（共享库）+ `net8.0`（Avalonia App）。
- macOS 上需要 `dotnet` 命令可用，无需 Xcode；`scripts/package-macos-app.sh` 会产出 `.app.zip` 和 `.dmg`，并可选用于 `.app` bundle 签名。
- Windows 上保持现有 `dotnet build` 工作流。

详细开发命令在重构进入 M3 阶段后写入 `SekaiToolsApp/README.md`。

## 相关文档

- 现行：[`docs/avalonia-merge-plan.md`](./docs/avalonia-merge-plan.md)
- 平台依赖：[`docs/sekai-platform-required-changes.md`](./docs/sekai-platform-required-changes.md)
- 历史（已废弃 MAUI 路线）：
  - [`docs/sekaitext-merge-plan.md`](./docs/sekaitext-merge-plan.md)
  - [`docs/sekaitext-implementation-roadmap.md`](./docs/sekaitext-implementation-roadmap.md)
  - [`docs/sekaitext-maui-translation-workbench.md`](./docs/sekaitext-maui-translation-workbench.md)
  - [`docs/sekaitext-phase1-status.md`](./docs/sekaitext-phase1-status.md)

## 开发计划

现行、详细的里程碑与状态以 [`docs/avalonia-merge-plan.md`](./docs/avalonia-merge-plan.md) 为准。

当前重点：

- 完成跨平台打包与发布收口
- 验证 Windows / macOS 的实际运行包
- 再处理 `Velopack` 和最终文档整理

## 声明

本项目所使用的素材版权均属于 Colorful Palette 以及 SEGA。
本项目仅用于学习交流。

## 致谢

感谢 PJS 字幕组的成员们对本项目的支持和帮助。
感谢 JetBrains 为本项目提供的开发工具支持。
