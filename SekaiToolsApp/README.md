# SekaiToolsApp

Avalonia 桌面壳，目标 **Windows + macOS**。`SekaiTools` 重构后的唯一 UI 入口。

> 当前状态：**M2 核心已完成，M3 发布验证中**。字幕主流程、设置、下载目录/历史、压制、翻译工作台已接入，恢复/草稿、校验、对照和导航可见性已补齐；tag release 会产出 Windows 7z 与 macOS `.app.zip` / `.dmg`。详见 `../docs/avalonia-merge-plan.md`。

## 技术栈

- Avalonia `11.2.x` LTS（`net8.0`）
- FluentAvaloniaUI（控件外形对齐 `WPF-UI` 的 Fluent 风格）
- CommunityToolkit.Mvvm
- ReactiveUI（已启用，按需使用）
- SkiaSharp / HarfBuzzSharp（在 `SekaiToolsCore` 已引入，给 M1 替换 GDI+ 用）

## 结构

```text
SekaiToolsApp/
├── App.axaml + App.axaml.cs          # 全局 Application
├── Program.cs                        # STA Main 入口
├── Views/
│   └── MainWindow.axaml + .cs        # M0 占位窗口；M1 改成 NavigationView 三栏
├── Imaging/
│   └── EmguCvAvaloniaInterop.cs      # Mat ↔ Avalonia Bitmap / WriteableBitmap 桥
├── Assets/                           # 图标 / 静态资源
├── app.manifest                      # Windows DPI / OS 兼容
└── SekaiToolsApp.csproj
```

`Imaging/EmguCvAvaloniaInterop` 替代 WPF 时代的 `Mat.ToBitmapSource()`，提供：

- `ToAvaloniaBitmap(Mat)`：单次转换为 `Avalonia.Media.Imaging.Bitmap`
- `WriteTo(Mat, WriteableBitmap)`：原位写入已有 `WriteableBitmap`，给帧预览循环用
- `CreateWriteableBitmap(Mat)`：按 Mat 尺寸建一个 BGRA8888 的可写位图

## 构建

```bash
# 仅构建（单平台、Debug）
dotnet build SekaiToolsApp/SekaiToolsApp.csproj -c Debug

# 各平台 publish（与 CI release/package 流程一致）
dotnet publish SekaiToolsApp/SekaiToolsApp.csproj -c Release -r win-x64 --self-contained
dotnet publish SekaiToolsApp/SekaiToolsApp.csproj -c Release -r osx-x64 --self-contained
dotnet publish SekaiToolsApp/SekaiToolsApp.csproj -c Release -r osx-arm64 --self-contained

# macOS bundle（在 macOS 上运行；会生成 .app 和 zip）
./scripts/package-macos-app.sh osx-arm64 1.0.0
./scripts/package-macos-app.sh osx-x64 1.0.0
```

> macOS bundle 脚本会把发布输出封装成 `.app`，并用 `Assets/icon.png` 生成 `.icns`，同时额外产出 `.zip` 和 `.dmg`。
> 脚本会额外把 `ffmpeg` 复制进 `.app/Contents/MacOS/ffmpeg`；可以通过 `SEKAI_TOOLS_FFMPEG_PATH` 指定要打包的可执行文件，未设置时则从系统 `PATH` 里找。若要让 bundle 在别的机器上直接可用，建议提供自带依赖的静态 ffmpeg。

## 运行（本机）

```bash
dotnet run --project SekaiToolsApp/SekaiToolsApp.csproj
```

## 当前不做

- 不在本项目内嵌 Tauri / Vue / Go。
- 不维护原 WPF GUI，迁移到 M4 后将从 `sln` 移除。
- 不维护 MAUI 翻译工作台，迁移到 M2/M4 后将从 `sln` 移除。
