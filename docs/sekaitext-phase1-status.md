# SekaiText 合并一期状态

> **DEPRECATED：本状态记录针对已废弃的 MAUI 路线。现行方案见 [`avalonia-merge-plan.md`](./avalonia-merge-plan.md)。**

## 结论

`SekaiTools` 侧的一期工作已经在源码层面完成，`SekaiPlatform` 侧的一期最小接口也已经开始落地。

当前状态可以概括为：

- 文档边界已收敛
- `SekaiToolsMauiText` 已成为新的跨平台翻译工作台入口
- 本地模式保留
- 平台模式骨架完成
- `SekaiPlatform` 已补最小语言资产接口和新建译文版本接口

## 已完成项

### 文档

- 合并总方案
- `SekaiPlatform` 依赖清单
- 实施路线图
- `MAUI` 工作台设计说明

### `SekaiToolsMauiText`

- 平台客户端 DTO 和 HTTP 客户端
- 平台会话服务
- 平台剧情浏览服务
- 本地工作区服务
- 平台模式页面骨架
- 本地模式与平台模式共用编辑器
- 平台译文加载
- 平台对照版本加载
- 平台新版本上传请求构造
- 本地剧本 / 翻译 / 对照 / 导出保留

### README

- 仓库 README 已更新为当前真实状态
- `SekaiToolsMauiText` README 已更新为当前真实状态

## 当前外部阻塞

### 1. 当前环境缺少 `.NET 10` SDK

当前环境只有：

- `.NET SDK 9.0.306`

影响：

- 无法在本地对 `net10.0` 目标做真实编译验证

## 当前平台实现进展

- `SekaiPlatform/apps/asset-service` 已新增语言资产查询内部接口
- `SekaiPlatform/apps/asset-service` 已新增 `POST /internal/stories/{storyId}/translation-versions`
- `SekaiPlatform/apps/api-service` 已新增对应 `/api/...` 代理接口
- 已补一组语言资产集成测试，覆盖浏览与新建译文主路径

## 下一步

1. 在具备 `.NET 10` SDK 的环境中进行真实编译和集成测试验证
2. 用真实 `SekaiPlatform` 实例与 `SekaiToolsMauiText` 做联调
3. 根据联调结果补平台筛选、错误提示和高级体验能力

## 说明

如果后续继续在 `SekaiTools` 内推进，优先级应是：

- 编译修正
- 联调修正
- 再迁新 `SekaiText` 的高级体验能力
