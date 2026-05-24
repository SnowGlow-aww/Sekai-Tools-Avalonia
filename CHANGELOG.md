# SekaiTools Avalonia v1.0.1 更新日志

## 设置

- 新增模板匹配阈值自定义（设置页 Slider 调节）
- 支持分别调整：对话名牌/对话内容（普通/特殊）、横幅、地点角标
- 阈值持久化到 setting.json，重启保持

---

# SekaiTools Avalonia v1.0-pre 更新日志

## 跨平台重构

- 从 WPF (Windows-only) 完整迁移至 Avalonia UI，支持 macOS / Windows / Linux
- 文本渲染从 GDI+ 迁移至 SkiaSharp，跨平台一致性更好
- .NET 10 目标框架

## 对话识别修复

- 修复阈值初始化 bug：`struct default` 导致所有阈值为 0 的问题
- 修复 NaN 检测逻辑：`Compare(mat, mat, CmpType.NotEqual)` 替代错误的 NaN==NaN 比较
- 修复 Alpha 通道问题：SkiaSharp 抗锯齿产生渐变 alpha，添加二值化处理
- 重新校准匹配阈值适配 SkiaSharp 模板（对话 0.70，横幅/标记 0.50）

## 下载页增强

- 剧情分类从 5 种扩展至 11 种（活动、卡面、主线、地图对话、特殊、支线等）
- 新增搜索功能，实时过滤候选剧本
- 新增进度条显示（刷新/下载进度）
- 候选列表显示文件大小
- 下载队列底部显示总大小和磁盘可用空间
- 「载入剧本」按钮改为打开下载目录
- 数据源排序优化：Moesekai JP 默认 → Haruki NEO → Sekai Best → Moesekai CN
- 使用源服务器原始标题
- 列表默认倒序排列（最新在前）
- 修复 List* 类重复加载数据 bug

## 压制性能优化

- 新增 HEVC 硬件编码支持（VideoToolbox / NVENC / QSV）
- 新增 AV1 NVENC / QSV 硬件编码支持（RTX 4000+ / Intel Arc）
- 新增 SVT-AV1、Libx265 软编码选项（仅探测到时显示）
- 编码器自动选择优先级：AV1 > HEVC > H264（硬件优先）
- HEVC/AV1 输出自动添加 `-tag:v hvc1` 确保 Apple 设备兼容
- Windows NVENC 优化：有字幕时仍使用 CUDA 解码
- NVENC 参数调优：`-tune ull -rc vbr -multipass 0` 最大化吞吐
- x264 软编码参数从 placebo 级降至 balanced（subme 7, ref 4），速度提升 4-7x
- 轴机：智能帧跳过，状态稳定时降频采样，速度提升约 1.5x
- 修复编码器在完成任务后重置为软编码的 bug
- 编码器列表精简：仅显示实际可用的编码器

## UI 改进

- ComboBox / TextBox / RadioButton 文本垂直居中
- 编码器选择 UI（自动探测可用硬件编码器）
- 硬件解码加速开关
- 字体选择器新增「从文件添加」按钮（支持 .ttf/.otf/.ttc）
- 修复 GridSplitter 光标闪烁问题（改用自定义拖拽实现）
- Windows 端 socket 权限错误时提示管理员模式

## 错误处理

- 未捕获异常自动写入日志（~/SekaiTools/Logs/）
- 错误弹窗提示用户发送日志给开发者

## 打包

- macOS：.app / .dmg / .zip 完整打包流程
- Windows：zip 便携包
- Linux：tar.gz 便携包
- CI：GitHub Actions 多平台自动构建
