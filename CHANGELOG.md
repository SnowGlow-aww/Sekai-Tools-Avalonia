# SekaiTools v1.0 更新日志

## 跨平台重构

- 从 WPF (Windows-only) 完整迁移至 Avalonia UI，支持 macOS / Windows / Linux
- 文本渲染从 GDI+ 迁移至 SkiaSharp，跨平台一致性更好
- .NET 10 目标框架

## 对话识别修复

- 修复阈值初始化 bug：`struct default` 导致所有阈值为 0 的问题
- 修复 NaN 检测逻辑：`Compare(mat, mat, CmpType.NotEqual)` 替代错误的 NaN==NaN 比较
- 修复 Alpha 通道问题：SkiaSharp 抗锯齿产生渐变 alpha，添加二值化处理
- 重新校准匹配阈值适配 SkiaSharp 模板（对话 0.70，横幅/标记 0.50）
- 对话识别率：70/70，横幅识别率：3/3

## 性能优化

- 压制：支持 GPU 硬件编码（macOS VideoToolbox / NVIDIA NVENC / Intel QSV）
- 压制：添加 `-hwaccel` 硬件解码加速
- 压制：x264 软编码参数从 placebo 级降至 balanced（subme 7, ref 4），速度提升 3-5x
- 轴机：智能帧跳过，状态稳定时降频采样，速度提升约 1.5x

## 压制功能增强

- 新增编码器选择 UI（自动探测可用硬件编码器）
- 新增硬件解码加速开关
- 默认自动选择最快的硬件编码器

## 打包

- macOS：.app / .dmg / .zip 完整打包流程
- CI：GitHub Actions 多平台自动构建
