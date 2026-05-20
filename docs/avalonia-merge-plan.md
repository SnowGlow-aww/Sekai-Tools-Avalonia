# SekaiTools 跨平台重构与 SekaiText 合并方案（Avalonia 路线）

> 状态：现行方案，权威文档。
> 取代：`sekaitext-merge-plan.md` / `sekaitext-implementation-roadmap.md` / `sekaitext-maui-translation-workbench.md` / `sekaitext-phase1-status.md`（旧 MAUI 路线，已废弃）。

## 目标

1. **`SekaiTools` 在 Windows 与 macOS 都能正常运行**，包括字幕生成主程序，不只是翻译工作台。
2. **将 `SekaiText` 仓库合并进来**，单仓库维护。
3. 单一可执行 GUI 应用：字幕生成、剧本下载、压制、翻译工作台共用一个壳。

## 技术选型：Avalonia

### 为什么是 Avalonia 而不是 MAUI / Tauri

| 维度 | MAUI | **Avalonia** | Tauri+Vue |
|---|---|---|---|
| 现有 WPF/XAML 复用度 | 低 | **高（语法/绑定/控件名近乎一致）** | 0 |
| `SekaiToolsCore` 复用 | 直接 | **直接** | 需 sidecar/IPC |
| `Emgu.CV` 改造 | 中（需替换 `Emgu.CV.Wpf`） | **小（5 处 `Mat.ToBitmapSource`）** | 大（IPC，预览成本高） |
| macOS 体验 | MacCatalyst（启动慢、原生感弱） | **原生** | 原生 webview |
| 打包 | 复杂（苹果链路） | **`dotnet publish` 三 RID 即可** | 中 |
| 工具链 | .NET 单栈 | **.NET 单栈** | Rust + Node + Go 三栈 |

### 限制

- 仅锁定 **Windows + macOS 桌面**。如果将来要 iOS/Android/iPad 才考虑回 MAUI。
- 仅 .NET 路线。如果将来要 web/嵌浏览器才考虑回 Tauri。

## 仓库结构终态

```text
SekaiTools/
├── SekaiToolsApp/            # 新：Avalonia 桌面应用，唯一 UI 入口
├── SekaiToolsCore/           # 改造：跨平台 net8.0，OCR/字幕生成
├── SekaiToolsBase/           # 保持：跨平台 net8.0，剧本与解析
├── SekaiDataFetch/           # 保持：跨平台 net8.0，远端数据
├── SekaiToolsPlatform/       # 新（拆自 MAUI 工作台）：SekaiPlatform 客户端 + DTO + 服务层
├── docs/                     # 文档
└── SekaiTools.sln
```

迁完后从 `sln` 中移除：
- `SekaiToolsGUI`（WPF, Windows-only）
- `SekaiToolsMauiText`（MAUI 过渡产物）
- `Updater`（Windows-only，使用 Velopack 替代）

`SekaiText` 仓库整个归档，只读保存为参考。

## 关键技术债与处理

### 1. `SekaiToolsCore` 的 `System.Drawing` 依赖

`System.Drawing.Common` 自 .NET 6+ 在非 Windows 上**官方不再支持**。

涉及文件：
- `SekaiToolsCore/Match/TemplateMatcher/TemplateManager.cs`
- `SekaiToolsCore/Utils/UtilFunc.cs`
- `SekaiToolsCore/Process/Model/VideoInfo.cs`
- `SekaiToolsCore/Process/Model/GaMat.cs`
- `SekaiToolsCore/Process/FrameSet/*.cs`
- `SekaiToolsCore/SubtitleMaker.cs`
- `SekaiToolsCore/Match/TemplateMatcher/*.cs`

处理：
- 仅使用 `Point`/`PointF`/`Size`/`Rectangle` **结构**的代码，可保留 `System.Drawing.Common`（这些不依赖 GDI+ 原生库）。
- 真正使用 `Bitmap` / `Graphics` / `Font` / `Pen`（GDI+）的代码——主要是 `TemplateManager` 渲染 OCR 模板——改用 `SkiaSharp`。

### 2. `Emgu.CV` 的 native runtime

替换：
- 移除 `Emgu.CV.runtime.windows`
- 改为按 RID 引入：
  - `Emgu.CV.runtime.mini.windows`（win-x64）
  - `Emgu.CV.runtime.mini.macos`（osx-x64 / osx-arm64）

`Emgu.CV.Wpf` 仅在原 GUI 中用过 5 处（`Mat.ToBitmapSource`），改为统一的 `Mat -> Avalonia.Media.Imaging.WriteableBitmap` 扩展。

### 3. `WPF-UI` (Fluent Design)

在 Avalonia 生态中由 `FluentAvalonia` 替代，控件外形与 WPF-UI 高度一致，迁移成本低。

### 4. `Updater`

Windows-only。改用 `Velopack`（跨平台 .NET 自动更新，单一 API 处理 Win + macOS）。

## 里程碑

### M0 — 解决方案与共用库可跨平台编译

- [x] 把 `SekaiToolsCore.csproj` `TargetFramework` 由 `net8.0-windows` 改为 `net8.0`，并按 RID 引入 Emgu.CV native。
- [x] 把 `SekaiToolsBase` 中只用 `System.Drawing` 结构体的部分留下，确认无 GDI+ 真实调用。
- [x] 新建 `SekaiToolsApp/SekaiToolsApp.csproj`（Avalonia 11.2、FluentAvalonia、CommunityToolkit.Mvvm、SkiaSharp）。
- [x] 加入 `SekaiTools.sln`，调通空壳能在 Windows 跑起来；macOS 能 `dotnet build`。
- [x] 写 `EmguCvAvaloniaInterop`：`Mat → WriteableBitmap`。

完成标准：`dotnet build SekaiTools.sln` 在 Windows 与 macOS 都通过；空 Avalonia 应用启动出窗口。

### M1 — 字幕生成主程序迁到 Avalonia

按页面优先级：
1. `MainWindow` + 导航壳（`FluentAvalonia.NavigationView`）
2. `View/Subtitle/*`（OCR 预览 + 字幕生成主流程）
3. `View/Setting/*`
4. `View/Download/*`
5. `View/Suppress/*`

并行：
- `TemplateManager` / `UtilFunc` 中 GDI+ 文本渲染改 `SkiaSharp`。
- 5 处 `Mat.ToBitmapSource` 改 `Mat.ToAvaloniaBitmap`。

完成标准：在 macOS 与 Windows 都能完成"载入视频 → OCR → 生成字幕"完整流程。

### M2 — 翻译工作台并入

状态：核心工作流已接通，恢复/草稿、校验、对照和导航可见性已补齐；下载目录、下载历史、压制页的跨平台 ffmpeg 回退，以及 macOS bundle / release 资产也已接通；CI 现在会在 macOS 打包前确保 ffmpeg 可用。当前只剩 Velopack 与文档收口。

来源：
- `SekaiToolsMauiText/Services/*`、`ViewModel/*`、`Models/*`：直接平移到 `SekaiToolsPlatform` 项目，**纯 .NET，无需改动**。
- `SekaiToolsMauiText/View/Translate/*`：5 个 XAML 重写为 Avalonia 版（结构一对一）。

来自 `SekaiText` 仓库的能力，按以下优先级重写迁入：
1. 故事导航（远端加载、面包屑、列表）
2. 译文版本载入与保存
3. 编辑器状态管理
4. 文本校验（行长、占位符、特殊替换）
5. 对照比较（diff 视图）
6. 自动恢复 / 草稿缓存

完成标准：原 `SekaiText` 用户的核心工作流可在 `SekaiToolsApp` 内完成。

### M3 — 跨平台打包与发布

- [x] `dotnet publish -r win-x64`、`-r osx-arm64`、`-r osx-x64`
- [x] macOS `.app` bundle（`scripts/package-macos-app.sh`，会把 `ffmpeg` 一并打进包，并额外产出 `.zip` / `.dmg`）
- [ ] 引入 `Velopack` 替代 `Updater`
- [x] CI（GitHub Actions）矩阵构建
- [ ] 文档：开发、构建、签名、发布

### M4 — 旧项目清理

- [x] 从 `SekaiTools.sln` 移除 `SekaiToolsGUI` / `SekaiToolsMauiText` / `Updater`
- [x] 这些项目目录加 `README.md` 标记 archived，仍可作为历史参考留在仓库
- [ ] `SekaiText` 仓库整体归档（只读），README 指向 `SekaiTools`

## SekaiText 能力迁移清单

仅记录需要迁入 `SekaiToolsApp` 的能力，技术形态全部翻译为 .NET / Avalonia：

| 来源能力 | 来源位置（SekaiText 仓库） | 目标位置 | 优先级 |
|---|---|---|---|
| 故事导航与列表 | `src/pages/`、`src/components/` | `SekaiToolsApp/Views/Translate/` | 高 |
| 平台 API 客户端 | `src/api/` | 已在 `SekaiToolsPlatform`，对齐补全 | 高 |
| 译文版本管理 | `src/stores/` | `SekaiToolsApp/ViewModels/Translate/` | 高 |
| 文本校验 | `src/composables/` | `SekaiToolsApp/Services/Validation/` | 中 |
| 对照比较 | `src/components/` | `SekaiToolsApp/Views/Translate/Diff/` | 中 |
| 自动恢复 | `src/stores/` | `SekaiToolsApp/Services/Recovery/` | 中 |
| Tauri commands → Go backend | `src-tauri/`、`backend/` | **不迁**（直接用 `SekaiPlatform` 客户端取代） | — |

## 不做的事

- 不在 `SekaiToolsApp` 内嵌 Tauri / Vue / Go。
- 不再在 `SekaiPlatform` 仓库直接改代码；需要平台补的接口只记录在 `docs/sekai-platform-required-changes.md`。
- 不维护 MAUI 工作台，迁移完成后从 `sln` 移除。
- 不维护原 WPF GUI，迁移完成后从 `sln` 移除。

## 验收

满足以下条件即视为重构完成：
1. `SekaiToolsApp` 在 Windows 与 macOS 都能 build / run / publish。
2. 字幕生成主流程（OCR → 生成 `.ass`）双平台均可。
3. 翻译工作台双模式（本地文件 / `SekaiPlatform`）双平台均可。
4. `SekaiText` 仓库已归档，本仓库 `README.md` 包含完整开发/构建/发布指引。
5. `sln` 不再包含 `SekaiToolsGUI` / `SekaiToolsMauiText` / `Updater`。
