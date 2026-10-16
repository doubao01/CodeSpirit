# CodeSpirit 统一项目规格

本文件是 `.monkeycode/specs/` 下唯一保留的规格文档，用于合并当前项目的功能规格、设计边界和任务验收标准。

## 产品定位

CodeSpirit 是一个面向 .NET 10 的约定式应用框架，提供 Spring Boot 式属性驱动开发体验、MVVM 页面模型、内置前端运行时、默认视觉系统、场景识别和 VSIX 模板。

目标是让开发者用少量 C# ViewModel 和 ASPX 标签生成可运行、可交互、可预览、默认好看的企业应用。

## 统一需求

### R1 应用启动与服务注册

- WHEN 应用调用 `builder.AddCodeSpirit()` 和 `app.UseCodeSpirit()`，THE 系统 SHALL 自动注册框架服务、模块、路由、中间件和页面渲染能力。
- WHEN 开发者使用 `[Service]` 标记类，THE 系统 SHALL 通过 Source Generator 生成 DI 注册代码。
- IF `[Service]` 类为抽象类、缺少公开构造或命令签名非法，THE 系统 SHALL 给出编译期诊断。

### R2 MVVM 页面与命令

- WHEN ViewModel 使用 `[PageDirective]` 声明路由，THE 系统 SHALL 将该 ViewModel 暴露为页面入口。
- WHEN 属性使用 `[Bind]`，THE 系统 SHALL 将其状态渲染到页面并支持前端更新。
- WHEN 用户提交 `data-cs-command`，THE 系统 SHALL 调用对应无参数 `[Command]` 方法。
- WHEN 命令返回 state 或 regions，THE 前端 SHALL 局部更新绑定字段和 `cs:Region` 区域。

### R3 内置页面标签

- THE 系统 SHALL 支持 `cs:Content`、`cs:PlaceHolder`、`cs:Repeater`、`cs:Conditional`、`cs:Link`。
- THE 系统 SHALL 支持 `cs:Form`、`cs:Button`、`cs:Field`、`cs:Table`、`cs:Column` 和 `cs:Region`，减少 MVVM 样板代码。
- THE 系统 SHALL 支持 `cs:Toolbar`、`cs:Tabs`、`cs:Modal`、`cs:Pager`，提供常见后台和企业应用组件。
- THE 系统 SHALL 支持 `cs:Scripts` 和 `cs:Script`，统一加载、替换或禁用内置前端资源。
- THE 前端运行时 SHALL 提供 `qs/qsa/on/debounce/throttle/ready/state/set/batch` 快捷 API，减少页面脚本样板代码。

### R4 默认视觉系统

- WHEN 页面没有编写业务 CSS，THE 系统 SHALL 通过 `site.css` 为普通 HTML 和 CodeSpirit 标签提供现代化默认外观。
- THE 默认样式 SHALL 覆盖标题、段落、链接、表单、字段、按钮、表格、section、article 和常用 `.cs-*` 组件。
- THE 默认样式 SHALL 保持隐藏输入、可访问焦点、响应式布局和可读层级。

### R5 场景识别与意图识别

- WHEN 页面加载，THE 系统 SHALL 通过 `codespirit.intent.js` 读取标题、表头、标签、按钮、命令、placeholder、class、页面标题和路由。
- THE 系统 SHALL 识别 dashboard、library、admin、commerce、content、analytics、crm、finance、education、healthcare、logistics、developer、hr、manufacturing、hospitality、real-estate、legal、support、supply-chain、research、security、retail、insurance、ngo 场景。
- WHEN 页面声明 `data-cs-scene`，THE 系统 SHALL 使用显式场景覆盖自动识别结果。
- WHEN 元素声明 `data-cs-intent`，THE 系统 SHALL 根据 numeric、status、due、trend 等意图应用语义色调。
- WHEN 场景识别低于阈值但存在候选场景，THE 系统 SHALL 写入低置信度调试属性。
- WHEN 开发者调用 `CodeSpirit.theme.exportTokens(root)`，THE 系统 SHALL 返回当前场景的 `--cs-*` 主题 token。

### R6 图书管理示例

- THE 示例应用 SHALL 提供首页大屏、后台管理、馆藏、读者、借阅、预约、库存审计和健康检查能力。
- WHEN 管理员导出馆藏，THE 系统 SHALL 输出包含 `ISBN,Title,Author,Category,Location,PublishedYear,CopyCount,Rating` 的 CSV。
- WHEN 管理员导入 CSV，THE 系统 SHALL 按 ISBN 新增或更新馆藏，并跳过缺少标题或作者的行。

### R7 模板同步

- WHEN 修改 `src/CodeSpirit.LibraryManagement` 中的页面、运行时、样式、脚本或 README，THE 系统 SHALL 同步检查 VSIX 模板目录。
- WHEN 新增内置脚本，THE 系统 SHALL 同步 VSIX `.vstemplate` 清单。
- WHEN 新增 snippet，THE 系统 SHALL 同步 VSIX 项目文件和 README 快捷方式表。

### R8 表达式驱动交互 (P0)

- WHEN 元素声明 `data-cs-show="Expr"`，THE 系统 SHALL 根据微型表达式求值结果显示/隐藏元素。支持比较 (`> < >= <= == !=`)、逻辑 (`&& || !`)、存在判断 (`contains`, `empty`)、三元 (`? :`)。
- WHEN 元素声明 `data-cs-enable="Expr"`，THE 系统 SHALL 根据表达式求值结果启用/禁用控件。
- WHEN 元素声明 `data-cs-refresh="Region"`，THE 系统 SHALL 在值变更后自动触发指定 `cs:Region` 的命令刷新。
- THE 表达式引擎 SHALL 在 `codespirit.runtime.js` 中内置，体积不超过 3KB，无需额外依赖。

### R9 语义化布局标签 (P0)

- THE 系统 SHALL 支持 `<cs:Grid>` 标签，通过 `Columns`、`Gap`、`BreakAt` 属性控制 CSS Grid 布局，内置响应式断点 `sm|md|lg|xl`，小屏自动切换单列。
- THE 系统 SHALL 支持 `<cs:Card>` 标签，自动应用框架默认圆角、阴影、内边距，替代手动 `.cs-card` class。
- THE 系统 SHALL 支持 `<cs:Stack>` 标签，通过 `Direction`、`Align`、`Gap` 属性控制 Flexbox 排列。
- WHEN 页面使用 `<cs:Grid>`、`<cs:Card>`、`<cs:Stack>` 组合，THE 系统 SHALL 无需额外页面 CSS 即呈现完整布局。

### R10 复合组件 (P1)

- THE 系统 SHALL 支持 `<cs:Crud>` 复合组件，通过 `Entity`、`Fields`、`Commands` 属性自动生成完整 CRUD 表单、按钮和表格联动刷新。
- THE 系统 SHALL 支持 `<cs:Chart>` 组件，通过 `Type`、`Width`、`Height`、`Data`、`Labels`、`Title` 属性生成 canvas 图表容器。
- THE 系统 SHALL 支持 `<cs:Tree>` 组件，通过 `Items`、`ChildrenField`、`LabelField`、`ValueField` 属性渲染递归树形结构。
- WHEN `<cs:Tree>` 节点存在子节点，THE 系统 SHALL 内置折叠展开行为，并触发 `codespirit:tree-toggle` 事件。
- THE 系统 SHALL 支持 `<cs:Wizard>` 组件，通过 `<cs:Step>` 子元素创建多步向导，`ActiveStep` 控制当前激活步骤。
- WHEN 用户点击 `<cs:Wizard>` 的步骤标题，THE 系统 SHALL 内置切换当前步骤和面板，并触发 `codespirit:wizard-step` 事件。
- WHEN `<cs:Dashboard>` 包裹 `<cs:MetricCard>`、`<cs:ActivityFeed>`、`<cs:QuickLinks>`，THE 系统 SHALL 自动应用仪表盘默认布局。
- THE 仪表盘子组件 SHALL 支持字段映射属性，使开发者可以复用现有数据模型字段而无需额外投影。
- WHEN 按钮声明 `data-cs-confirm="消息"`，THE 系统 SHALL 内置确认对话框，无需 jQuery 行为扩展。
- WHEN 下拉框声明 `data-cs-source="Command"`，THE 系统 SHALL 自动通过命令获取选项数据。

## 设计边界

| 边界 | 归属 | 约束 |
|------|------|------|
| `data-cs-*` | MVVM runtime | 只处理绑定、命令、状态、局部刷新、错误回显和表达式求值 |
| `data-cs-show/enable/refresh/confirm/source` | Expression engine | 内置微型表达式引擎，无额外依赖 |
| `data-ui` | UI behavior layer | 只处理渐进增强、组件行为和第三方控件初始化 |
| `data-cs-intent` | Intent analyzer | 只处理元素级语义色调 |
| `data-cs-scene` | Scene analyzer | 只处理页面级场景视觉 |
| `site.css` | 默认视觉系统 | 提供默认好看的基线，业务 CSS 只补特定布局 |
| `<cs:Grid/Card/Stack/Chart/Tree/Wizard>` | 语义布局与复合组件层 | 属性驱动，消灭手写 JS/CSS |
| PageRenderer cache | 渲染性能 | `_renderCache` 基于 (template, stateJson) 键缓存并限制容量，`ClearRenderCache()` 和测试指标供回归验证使用 |
| VSIX template | 新项目生成 | 与示例项目保持功能一致 |

## 验收清单

- ✓ `cs:Form`、`cs:Button`、`cs:Field`、`cs:Table`、`cs:Region` 已可用。
- ✓ `cs:Toolbar`、`cs:Tabs`、`cs:Modal`、`cs:Pager` 已可用。
- ✓ `cs:Scripts` 和 `cs:Script` 已支持内置脚本加载、替换和禁用。
- ✓ 图书 CSV 导入导出已通过 MVVM 命令流实现。
- ✓ 默认视觉系统已覆盖普通 HTML 和常用 `.cs-*` 组件，新增 ul、ol、blockquote、code、pre、hr、fieldset、legend、figure、img、details、summary、nav、aside、header、footer、small、progress、meter 等无类名默认样式。
- ✓ 场景识别已覆盖 24 类通用业务场景（含 supply-chain、research、security、retail、insurance、ngo）。
- ✓ JS 边界验证脚本覆盖 runtime、UI behavior、intent 和 scene 关键路径。
- ✓ 示例项目 README、模板 README 和根 README 已同步更新。
- ✓ VS Code 23 个前端 IntelliSense snippets、VSIX snippets 和 `codespirit.d.ts` 类型声明已验证与实际代码模式一致。
- ✓ 表达式引擎 `data-cs-show/enable/refresh` 已内置到前端运行时链路（P0）。
- ✓ `<cs:Grid>`、`<cs:Card>`、`<cs:Stack>` 布局标签已可用（P0）。
- [✓] `data-cs-confirm` 和 `data-cs-source` 已内置，替代 jQuery 行为依赖（P1）。
- ✓ `<cs:Crud>` 复合组件已可用（P1）。
- ✓ `<cs:Dashboard>` 自动仪表盘布局和子组件字段映射已可用（P2）。
- ✓ `<cs:Chart>` 图表容器组件已可用（P1）。
- ✓ `<cs:Tree>` 递归树形组件和内置折叠展开已可用（P1）。
- ✓ `<cs:Wizard>` 多步向导组件和内置步骤切换已可用（P1）。
- ✓ `data-cs-source` 选项数据源协议已可用（P1）。
- ✓ 渲染缓存、路径缓存和指标机制已实现，137 个 C# 测试 + JS 边界验证全通过。
- ✓ JS 测试套件已扩展至 61 个表达式、VM 链、意图分析、主题 token、运行时快捷 API 及拓展校验全覆盖用例。

## 验证标准

每次涉及框架、示例、模板、前端运行时或默认样式的改动，至少执行：

```bash
node src/CodeSpirit.LibraryManagement/scripts/validate-js-boundary.js
dotnet test src/CodeSpirit.slnx
```

涉及项目文件、Source Generator 或整体结构时追加执行：

```bash
dotnet build src/CodeSpirit.slnx
```

## 已完成演进

- ✓ 为更多企业场景补充场景关键词和主题变量。
- ✓ 为默认视觉系统增加可配置 token 导出能力。
- ✓ 为意图识别增加低置信度调试提示。
- ✓ 为 VSIX 模板增加更多可复用页面片段。
- ✓ 将项目文档继续保持为 docs 单文件、specs 单文件结构。
- ✓ 扩展源码生成器静态诊断 CSP014（命令返回值类型校验）与 Roslyn 内存编译测试。
- ✓ 开放 `ITagRenderer` 自定义标签扩展接口并支持 `<cs:Form>` / `<cs:Button>` 原生 Loading 与 Debounce 防抖。
- ✓ 扩展前端运行时校验体系（url/numeric/integer/phone/matches 与 `CodeSpirit.registerValidator` 全局校验注册）。
- ✓ `ViewModelExecutor` 支持并 await `ValueTask` 异步命令。
