# SekaiPlatform 需要补充的能力清单

## 文档目的

本文档只描述 `SekaiTools` 合并 `SekaiText` 后，对 `SekaiPlatform` 的依赖和期望。

注意：

- 本文档不代表这些能力已经在 `SekaiPlatform` 中实现。
- 本文档用于后续由 `SekaiPlatform` 仓库单独落地。
- `SekaiTools` 不应直接承接这些服务端职责。

## 总体目标

`SekaiTools` 需要把翻译工作流切到平台化模式：

- 原文从 `SekaiPlatform` 读取
- 译文向 `SekaiPlatform` 提交
- 用户身份、租户和版本归档都由 `SekaiPlatform` 负责

## 最小必需接口

## 1. 认证与租户

### 已有或预期使用

- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/session`
- `GET /api/auth/tenants`
- `PUT /api/auth/current-tenant`

### 要求

- 桌面客户端可通过 `Cookie` 或 `Bearer Token` 维持会话
- 会话响应中明确返回当前租户
- 未选择租户时，客户端能感知并继续执行租户选择流程

## 2. 剧情浏览

### 需要的接口

- `GET /api/story-types`
- `GET /api/story-groups`
- `GET /api/story-groups/{storyGroupId}`
- `GET /api/stories`
- `GET /api/stories/{storyId}`

### 最低要求

- 支持按 `story_type` 过滤
- 支持按 `story_group_id` 过滤
- 支持标题关键词检索
- 返回稳定排序字段，方便桌面端构建下拉选择

### 建议补充字段

剧情集：

- `id`
- `story_type`
- `title`
- `subtitle`
- `display_no`
- `story_count`

剧情：

- `id`
- `group_id`
- `story_type`
- `scenario_id`
- `title`
- `sort_order`
- `metadata`

## 3. 原文行读取

### 需要的接口

- `GET /api/stories/{storyId}/source-lines`

### 最低要求

- 返回稳定的行顺序
- 每行必须有唯一 `source_line_id`
- 明确 `line_type`

### 建议字段

- `id`
- `story_id`
- `line_no`
- `line_type`
- `speaker`
- `text`
- `metadata`

### 说明

`SekaiTools` 前端会根据 `line_type` 决定用对话行组件还是效果/场景行组件渲染。

## 4. 译文版本浏览

### 需要的接口

- `GET /api/stories/{storyId}/translation-versions`
- `GET /api/translation-versions/{translationVersionId}`
- `GET /api/translation-versions/{translationVersionId}/lines`

### 最低要求

- 只返回当前租户可见版本
- 能按版本号或创建时间排序
- 版本详情能区分创建人和创建时间

### 建议字段

译文版本：

- `id`
- `story_id`
- `version_no`
- `title`
- `created_by`
- `created_by_name`
- `created_at`
- `updated_at`

译文行：

- `id`
- `version_id`
- `source_line_id`
- `story_id`
- `line_no`
- `speaker`
- `text`
- `metadata`

## 5. 新建译文版本

### 需要的接口

- `POST /api/stories/{storyId}/translation-versions`

### 建议请求体

```json
{
  "title": "string",
  "lines": [
    {
      "source_line_id": 1,
      "line_no": 1,
      "speaker": "string|null",
      "text": "string",
      "metadata": "string|null"
    }
  ]
}
```

### 最低要求

- 根据当前租户创建新版本
- 自动分配 `version_no`
- 校验所有 `source_line_id` 都属于目标剧情
- 返回新版本信息和写入行数

### 行为约束

- 当前阶段建议只支持“新建版本”，不要先做覆盖式更新
- 版本创建必须保留历史记录

## 推荐增强接口

以下不是第一阶段必需，但对替代 `SekaiText` 很重要。

## 1. 搜索

### 需要的接口

- `GET /api/search`

### 用途

- 让桌面端按原文、译文、角色名快速检索剧情

## 2. 草稿 / 自动保存

### 候选方向

- 平台端草稿版本
- 未发布版本
- 用户级草稿缓存

### 原因

`SekaiText` 类工具长期编辑时需要恢复能力，只靠本地文件不够稳。

## 3. 版本比较

### 候选接口

- 获取两个版本的差异结果
- 行级 diff

### 原因

桌面端需要做对照、校对和回归检查。

## 4. 行级文本检查辅助

### 候选能力

- 平台提供词典、术语表、敏感格式规则
- 返回统一检查结果

### 原因

这类规则如果完全散落在客户端，后期会难以统一。

## 5. 分页和筛选

### 建议

以下列表接口都应尽快支持分页：

- `story-groups`
- `stories`
- `translation-versions`

### 原因

随着数据量增长，桌面端不能假设所有列表都能一次拉完。

## 版本与状态建议

为了支持桌面翻译工作流，建议 `translation_versions` 后续考虑增加状态字段，例如：

- `draft`
- `submitted`
- `reviewed`
- `published`

当前不是必需，但后续很可能要用。

## 与 SekaiTools 的边界

`SekaiTools` 负责：

- 桌面 UI
- 用户交互
- 本地兼容模式
- 编辑体验

`SekaiPlatform` 负责：

- 登录与租户
- 原文存储与读取
- 译文版本存储
- 权限控制
- 历史归档

## 建议实施顺序

1. 先完成剧情列表、原文行、译文版本只读接口
2. 再完成新建译文版本接口
3. 然后补搜索、分页、草稿、比较等增强能力

## 交付标准

当下列条件满足时，`SekaiTools` 才能真正完成平台化切换：

1. 桌面端可登录并选择租户
2. 桌面端可浏览剧情并加载原文
3. 桌面端可读取当前租户已有译文版本
4. 桌面端可创建新的译文版本并成功上传
5. 平台保证版本历史可追踪
