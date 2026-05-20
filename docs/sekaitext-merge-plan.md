# SekaiText 合并到 SekaiTools 计划

> **DEPRECATED：本文档对应已废弃的 MAUI 路线，仅作历史参考。**
>
> 现行方案见 [`avalonia-merge-plan.md`](./avalonia-merge-plan.md)。
>
> 废弃原因：MAUI 路线无法低成本承接 `SekaiToolsGUI` 的 30 个 WPF 页面，且 MacCatalyst 的桌面体验与维护成本不可接受。改走 Avalonia 后 WPF XAML 可近乎平移，`SekaiToolsCore` 直接复用，跨平台打包链路简单。

## 背景

- 当前目录下存在一个新写的 `SekaiText` 项目。
- `SekaiTools` 里曾经已经合并过一个旧版 `SekaiText`，对应当前的 `SekaiToolsMauiText`。
- 本次目标不是继续维护独立的 `SekaiText` 桌面应用，而是把它的新能力并入 `SekaiTools`。
- 目标平台以 `Windows` 和 `macOS` 为主，需要统一的跨平台 UI。

## 合并原则

### 1. 以 `SekaiToolsMauiText` 作为承载壳

不把 `SekaiText` 的 `Tauri + Vue + Go` 整套直接嵌进 `SekaiTools`。

原因：

- `SekaiTools` 当前主技术栈是 `.NET`。
- `SekaiToolsMauiText` 已经具备跨平台基础，覆盖 `Windows` 和 `macOS`。
- 直接迁入 `Tauri` 会形成第二套桌面壳、第二套应用生命周期和第二套打包链路，维护成本过高。

结论：

- UI 载体使用 `MAUI`。
- `SekaiText` 的可复用部分按“能力迁移”而不是“工程整体搬运”处理。

### 2. 先迁工作流，再迁高级功能

优先支持：

- 登录 `SekaiPlatform`
- 选择剧情
- 拉取原文
- 编辑译文
- 上传译文

后续再补：

- 复杂校验
- 搜索与筛选
- 自动恢复
- 对照比较
- 语音、线索、闪回分析等增强功能

### 3. 保留本地文件模式作为兜底

在平台接口未齐备前，`SekaiToolsMauiText` 仍需支持：

- 载入本地剧本文件
- 打开本地翻译文件
- 打开本地对照翻译
- 导出本地翻译文件

这样可以保证迁移过程中工具不中断可用。

## 目标架构

### SekaiTools 内部职责

`SekaiToolsMauiText` 负责：

- 平台登录和租户切换
- 剧情浏览与加载
- 原文/译文编辑 UI
- 平台上传
- 本地文件兼容模式

`SekaiToolsBase` 可继续承载：

- 剧本解析
- 旧文本格式兼容
- 基础行模型与校验逻辑

### 不直接迁入的内容

以下内容不建议原样搬进 `SekaiTools`：

- `SekaiText` 的 `Tauri` 壳
- `SekaiText` 的 `Go HTTP backend`
- `SekaiText` 的前端工程组织方式

这些部分应该被翻译成：

- `MAUI` 页面
- `SekaiPlatform` 客户端调用
- `SekaiTools` 内部服务层

## 分阶段实施

## 阶段 1：平台工作流打通

目标：

- 在 `SekaiToolsMauiText` 中支持平台登录
- 支持剧情类型/剧情集/剧情选择
- 支持拉取原文行
- 支持加载平台已有译文版本
- 支持上传新译文版本

交付物：

- `SekaiPlatform` 客户端
- 平台模式下的翻译工作台页面
- 本地模式与平台模式共存

## 阶段 2：迁移 SekaiText 的核心交互体验

目标：

- 把新 `SekaiText` 的编辑体验迁移到 `MAUI`
- 对齐新项目中的浏览、状态和编辑流

重点迁移能力：

- 更完整的故事导航
- 更稳定的翻译版本载入流程
- 行级编辑体验
- 对照翻译加载
- 更清晰的状态提示

## 阶段 3：迁移高级编辑功能

目标：

- 对齐新 `SekaiText` 中已经实现但尚未迁入的增强能力

候选项：

- 文本检查
- 行长检查
- 特殊替换
- 自动恢复
- 比较与差异辅助
- 本地缓存和草稿

## 阶段 4：统一文档与发布方式

目标：

- 把翻译能力正式并入 `SekaiTools`
- 明确运行、打包、发布和测试方式

需要更新：

- `SekaiTools/README.md`
- `SekaiToolsMauiText/README.md`
- 开发和测试文档

## SekaiText 能力迁移清单

建议按下列顺序吸收新 `SekaiText` 的能力：

1. 故事导航和远端加载
2. 译文版本加载与保存
3. 编辑器状态管理
4. 文本校验与比较
5. 恢复、缓存、调试能力

## 当前明确不做的事

- 不在 `SekaiTools` 中嵌入 `Tauri` 子应用
- 不在 `SekaiTools` 中继续维护 `Go backend`
- 不把 `SekaiPlatform` 的待开发能力偷放到 `SekaiTools` 本地伪造实现

## 风险

### 1. 平台接口未齐

`SekaiText` 的新工作流天然依赖远端故事和译文接口，而 `SekaiPlatform` 当前未必全部具备。

应对：

- 在 `SekaiTools` 文档中列清接口缺口
- `SekaiToolsMauiText` 保留本地模式

### 2. 旧文本模型与平台模型并存

本地 `txt` 翻译格式与平台行模型不完全一致。

应对：

- UI 层允许两种来源并存
- 核心编辑组件尽量复用
- 上传动作单独走平台 DTO

### 3. MAUI 与原 Vue 交互体验差异

新 `SekaiText` 的部分体验在 `MAUI` 中需要重新设计。

应对：

- 先迁工作流
- 再逐步补体验

## 下一步建议

1. 在 `SekaiTools` 内继续完善 `SekaiToolsMauiText` 的平台模式。
2. 按本文档拆出后续任务，不再直接修改 `SekaiPlatform` 仓库。
3. `SekaiPlatform` 需要补的接口和行为，见 `docs/sekai-platform-required-changes.md`。
4. 实施路线图见 `docs/sekaitext-implementation-roadmap.md`。
5. `MAUI` 工作台的当前设计说明见 `docs/sekaitext-maui-translation-workbench.md`。
