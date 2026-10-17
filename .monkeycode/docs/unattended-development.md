# CodeSpirit 无人值守开发手册

本文件是 `.monkeycode/docs/` 下唯一保留的项目文档，作为 Agent 执行 CodeSpirit 开发任务时的统一入口。

## 目标

CodeSpirit 的无人值守开发流程用于把明确需求推进到可验证交付：理解上下文、修改源码、同步模板、更新文档、运行验证、生成预览和说明结果。

适用范围：框架核心、示例项目、VSIX 模板、内置前端资源、测试、README 和项目规格。

## 启动清单

- ✓ 阅读 `.monkeycode/MEMORY.md`，加载项目级指令。
- ✓ 检查 `.gitmodules`，存在时初始化 submodule。
- ✓ 读取 `README.md`，确认项目定位、验证命令和前端边界。
- ✓ 读取 `.monkeycode/specs/project-spec.md`，确认当前统一规格。
- ✓ 判断任务是否影响 `src/CodeSpirit.LibraryManagement`。
- ✓ 判断任务是否需要同步 VSIX 模板目录。
- ✓ 判断任务是否需要更新 README、snippet、模板清单或边界验证脚本。

## 安装与运行

- ✓ 安装 .NET 10 SDK。
- ✓ 安装 Node.js，用于 JS 边界验证。
- ✓ 可选安装 Visual Studio 2022 17.10+ 或 VS2026，用于 VSIX 模板开发，本轮未依赖。
- ✓ 在仓库根目录验证解决方案。

```bash
dotnet build src/CodeSpirit.slnx
dotnet run --project src/CodeSpirit.LibraryManagement
```

## 执行流程

- ✓ 使用 `Glob`、`Grep`、`Read` 建立上下文。
- ✓ 为复杂任务创建 todo，并保持单个 `in_progress`。
- ✓ 采用最小正确改动。
- ✓ 修改运行时、页面、样式、脚本或 README 时同步 VSIX 模板。
- ✓ 修改内置前端行为时补充 `validate-js-boundary.js`。
- ✓ 新增内置 JS 文件时更新 VSIX `.vstemplate` 清单。
- ✓ 新增 snippet 时更新 VSIX csproj、模板 README 和根 README。
- ✓ 修改 Source Generator 诊断时同步 `AnalyzerReleases.Unshipped.md`，本轮未涉及。
- ✓ 交付前运行验证命令。

## 边界与约束

- ✓ `data-cs-*` 归 MVVM runtime 管理。
- ✓ `data-ui` 归 UI behavior layer 管理。
- ✓ `data-cs-intent` 归语义色调识别管理。
- ✓ `data-cs-scene` 归场景识别和默认视觉系统管理。
- ✓ 页面业务 CSS 只补业务特有布局，优先使用内置样式。
- ✓ `[Command]` 方法保持无参数，通过绑定状态传递输入。
- ✓ 源码示例和 VSIX 模板保持一致。
- ✓ 不输出密钥、token 或凭据真实值。
- ✓ 不执行破坏性命令。
- ✓ 不修改无关用户改动。
- ✓ 只有用户明确要求时才提交或推送。

## 默认样式验收

- ✓ 普通 `h1/h2/h3/p/a` 无需 class 也具备基础排版。
- ✓ 普通 `form/label/input/select/textarea/button` 无需 class 也具备可用外观。
- ✓ 普通 `table` 无需 class 也具备边框、表头、阴影和可读间距。
- ✓ 普通 `section/article` 无需 class 也能形成卡片容器。
- ✓ `input[type='hidden']` 保持隐藏。
- ✓ `cs-scene-*` 场景类能改变页面视觉气质。
- ✓ `data-cs-scene` 能显式覆盖自动识别结果。

## 场景识别验收

- ✓ 大屏识别为 `cs-scene-dashboard`。
- ✓ 图书管理识别为 `cs-scene-library`。
- ✓ 后台管理识别为 `cs-scene-admin`。
- ✓ 电商识别为 `cs-scene-commerce`。
- ✓ 内容管理识别为 `cs-scene-content`。
- ✓ 报表分析识别为 `cs-scene-analytics`。
- ✓ CRM 识别为 `cs-scene-crm`。
- ✓ 财务识别为 `cs-scene-finance`。
- ✓ 教育识别为 `cs-scene-education`。
- ✓ 医疗识别为 `cs-scene-healthcare`。
- ✓ 物流识别为 `cs-scene-logistics`。
- ✓ 开发者工具识别为 `cs-scene-developer`。
- ✓ 人事识别为 `cs-scene-hr`。
- ✓ 制造识别为 `cs-scene-manufacturing`。
- ✓ 酒店识别为 `cs-scene-hospitality`。
- ✓ 房产识别为 `cs-scene-real-estate`。
- ✓ 法务识别为 `cs-scene-legal`。
- ✓ 客服识别为 `cs-scene-support`。
- ✓ 低置信度场景识别写入 `data-cs-scene-confidence` 调试属性。
- ✓ `CodeSpirit.theme.exportTokens(root)` 可导出当前场景主题 token。

## 验证命令

运行时、前端行为、页面标签、模板同步或默认样式变更后执行：

```bash
node src/CodeSpirit.LibraryManagement/scripts/validate-js-boundary.js
dotnet test src/CodeSpirit.slnx
```

整体构建、Source Generator、模板或项目文件变更后追加执行：

```bash
dotnet build src/CodeSpirit.slnx
```

## 预览流程

- ✓ 优先复用已有后台终端中的 Web 服务。
- ✓ 服务未启动时按 README 命令启动。
- ✓ 获取预览地址后访问首页、后台页和关键静态资源。
- ✓ 检查预览连接状态响应体中是否包含平台错误 meta。

```bash
curl -s --max-time 10 "https://<preview-host>/.well-known/mcai-preview-connect-status-detect"
```

## 交付清单

- ✓ 说明完成内容。
- ✓ 列出关键文件路径。
- ✓ 列出验证命令和结果。
- ✓ 提供预览地址和后台终端 ID。
- ✓ 标注 warning 或外部依赖状态。

## 完成定义

- ✓ 需求已实现或文档已更新。
- ✓ 源码与模板同步完成。
- ✓ 对应验证通过。
- ✓ 预览或构建路径可复现。
- ✓ 最终回复包含结果和验证事实。

## 最新执行记录

- Date: 2026-10-17
- JS 验证: `node src/CodeSpirit.LibraryManagement/scripts/validate-js-boundary.js` 通过，61 个测试通过。
- 测试验证: `dotnet test src/CodeSpirit.slnx` 通过，156 个测试通过。
- 构建验证: `dotnet build src/CodeSpirit.slnx` 编译通过。
- 页面标签增强: 完成 `cs:Repeater` 深度配对闭合与独立空态隔离，支持 `cs:Table` / `cs:Crud` / `cs:Tree` 空态与属性绑定。
- 文档结构: `.monkeycode/docs` 保留 `unattended-development.md`，`.monkeycode/specs` 保留 `project-spec.md`。
- 模板同步: 源码与 VSIX 模板目录下的 CSS、README 保持一致。
