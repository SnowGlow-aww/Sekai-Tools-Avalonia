# SekaiText 合并实施路线图

> **DEPRECATED：对应已废弃的 MAUI 路线。现行方案见 [`avalonia-merge-plan.md`](./avalonia-merge-plan.md)。**

## 文档目的

本文档用于把 `SekaiText -> SekaiTools` 的合并计划拆成可执行任务，作为实际开发顺序和验收依据。

相关文档：

- [sekaitext-merge-plan.md](./sekaitext-merge-plan.md)
- [sekai-platform-required-changes.md](./sekai-platform-required-changes.md)

## 一期范围

一期只做 `SekaiTools` 侧可落地部分，不修改 `SekaiPlatform` 代码。

一期目标：

1. 以 `SekaiToolsMauiText` 作为新的跨平台翻译入口
2. 保留本地文件翻译模式
3. 预留并接入 `SekaiPlatform` 客户端骨架
4. 在平台接口具备后，能直接切换到平台工作流

## 非一期范围

以下内容不在一期完成：

- `SekaiPlatform` 服务端接口实现
- `SekaiText` 全部功能一比一复刻
- 搜索、草稿、比较、自动恢复的完整闭环
- 原 `Tauri` 工程整体迁入

## 里程碑

## M1：文档和边界收敛

完成标准：

- 明确 `SekaiTools` 是唯一实施仓库
- 明确 `SekaiPlatform` 只记录需求，不直接在本仓库代改
- 明确跨平台壳使用 `SekaiToolsMauiText`

当前状态：

- 已完成

## M2：平台模式骨架

完成标准：

- `SekaiToolsMauiText` 中有独立的 `SekaiPlatform` 客户端
- 支持保存平台地址
- 支持登录、登出、刷新会话、切换租户
- 支持剧情类型/剧情集/剧情/译文版本的 UI 占位

当前状态：

- 已完成一期骨架

## M3：编辑器双模式统一

完成标准：

- 本地模式可继续加载旧剧本和旧翻译文件
- 平台模式和本地模式复用同一套行编辑组件
- 平台行具备独立的 `source_line_id` / `line_no` 元信息

当前状态：

- 已完成一期骨架

## M4：平台上传链路

完成标准：

- 当前编辑内容可以转换成平台请求 DTO
- 上传动作不依赖本地 `txt` 文件格式
- 上传行为定义为“创建新译文版本”

当前状态：

- 进行中

## M5：体验补齐

完成标准：

- 平台模式具备合理的错误提示和空状态
- 本地模式与平台模式切换不互相污染状态
- README 和测试说明更新完成

当前状态：

- 未开始

## 一期任务拆分

## A. 文档

### A1. 合并方案文档

- 说明为什么选 `MAUI`
- 说明为什么不迁 `Tauri`
- 说明和 `SekaiPlatform` 的边界

状态：

- 已完成

### A2. 平台依赖文档

- 列出最小所需接口
- 列出增强接口
- 说明字段和行为预期

状态：

- 已完成

### A3. 实施路线图

- 里程碑
- 任务拆分
- 验收标准

状态：

- 已完成

## B. 客户端基础

### B1. 平台模型定义

- Auth Session
- Tenant
- StoryGroup
- Story
- SourceLine
- TranslationVersion
- TranslationLine

状态：

- 已完成

### B2. 平台 HTTP 客户端

- Base URL 配置
- Token 持久化
- 错误消息解析
- 会话接口
- 剧情接口
- 译文接口

状态：

- 已完成

### B3. DI 和页面实例管理

- `App`
- `AppShell`
- `MauiProgram`

状态：

- 已完成

## C. 平台翻译工作台

### C1. 页面结构

- 登录区
- 租户区
- 剧情浏览区
- 本地兼容模式区
- 编辑列表区

状态：

- 已完成骨架

### C2. 页面交互

- 登录
- 刷新会话
- 切换租户
- 加载剧情类型
- 加载剧情集
- 加载剧情
- 加载平台版本
- 上传新版本

状态：

- 已完成一期骨架

### C3. 编辑数据统一

- 本地 `Story` -> 行模型
- 平台 `SourceLine` -> 行模型
- 行模型保留平台元信息

状态：

- 已完成基础映射

### C4. 服务层抽离

- Platform Session Service
- Platform Story Service
- Local Translation Workspace Service

状态：

- 已完成第一轮抽离

## D. 本地兼容模式

### D1. 剧本加载

- `.json`
- `.asset`

状态：

- 已保留

### D2. 翻译文件加载

- `.txt`
- 对照翻译

状态：

- 已保留

### D3. 本地保存

- 编辑结果导出到 `.txt`

状态：

- 已保留

## E. 下一阶段待办

### E1. 从新 SekaiText 迁体验

- 更细的故事导航
- 更合理的状态提示
- 更接近新工具的工作流

### E2. 迁高级能力

- 文本检查
- 行长检查
- 自动恢复
- 版本比较

### E3. 文档收尾

- 更新 `README.md`
- 更新 `SekaiToolsMauiText/README.md`
- 增加 smoke test

### E4. 构建验证

- 在具备 `.NET 10` SDK 的环境中完成真实编译验证
- 补充平台接口可用时的联调验证记录

## 一期验收标准

满足下列条件即可认为一期完成：

1. `SekaiToolsMauiText` 可作为唯一跨平台翻译入口继续演进
2. 本地剧本/翻译文件模式不退化
3. 平台模式具备登录、浏览、载入、上传的客户端闭环
4. 平台所需服务端改动全部以文档方式记录在 `SekaiTools/docs`
5. 不再在 `SekaiTools` 开发过程中直接改 `SekaiPlatform`
6. 一期代码结构不再把所有平台流程都堆在页面后置代码中

## 当前阻塞

### 1. 平台接口未真正实现

影响：

- 客户端平台模式目前只能做到骨架和请求接线

处理：

- 继续完善客户端
- 平台需求只在文档中维护

### 2. 本地环境 SDK 限制

当前环境是 `.NET SDK 9.0.306`，目标项目是 `net10.0`。

影响：

- 当前无法在本机完成真实编译验证

处理：

- 先做源码级整理
- 等具备 `net10.0` SDK 的环境后再做构建验证
