# CodeSpirit Library Management

这是 CodeSpirit 的企业图书管理示例应用，也是 VSIX 和 `dotnet new` 模板的参考实现。

## 运行

```bash
dotnet run --project src/CodeSpirit.LibraryManagement
```

应用默认监听 `http://0.0.0.0:8000`。

## 目录结构

```text
CodeSpirit.LibraryManagement/
├── Features/
├── Pages/
├── Components/
├── Reports/
├── wwwroot/
├── scripts/
├── Program.cs
└── appsettings.json
```

## 模块约定

- 业务代码放在 `Features/{ModuleName}`。
- 页面 ViewModel 放在对应模块目录，例如 `Features/Admin/AdminViewModel.cs`。
- 领域模型和服务放在能力目录，例如 `Features/Library`。
- 框架约定文件保留在根目录：`Pages`、`Components`、`Reports`、`wwwroot`。
- ASPX 只描述标记结构，路由和标题放在 ViewModel 的 `PageDirective`。
- ASPX 渲染支持嵌套绑定路径、列表索引和 `<cs:Repeater>` 内继续使用 CodeSpirit 组件。
- 默认布局 `Pages/Site.master` 使用 `<cs:Scripts />` 加载内置前端工具链。

## 内置页面标签

| 标签 | 作用 |
|------|------|
| `cs:Content` | 填充布局占位符 |
| `cs:PlaceHolder` | 声明布局插槽 |
| `cs:Repeater` | 渲染集合 |
| `cs:Conditional` | 条件渲染 |
| `cs:Link` | 安全渲染链接 |
| `cs:Form` | 渲染 MVVM 表单 |
| `cs:Button` | 渲染命令按钮 |
| `cs:Field` | 渲染绑定字段 |
| `cs:Table` | 渲染表格 |
| `cs:Column` | 自定义表格单元格模板 |
| `cs:Toolbar` | 渲染工具栏 |
| `cs:Tabs` | 渲染标签页 |
| `cs:Modal` | 渲染弹窗 |
| `cs:Pager` | 渲染分页 |
| `cs:Region` | 渲染局部刷新区域 |
| `cs:Scripts` | 渲染内置脚本资源 |
| `cs:Script` | 渲染页面级脚本 |

## 前端工具链

- `Pages/Site.master` 通过 `<cs:Scripts>` 加载 runtime、UI behaviors、intent analysis、dev panel、site scripts 和页面脚本。
- 使用 `<cs:Scripts Runtime="/js/custom-runtime.js" />` 替换内置脚本。
- 使用 `<cs:Scripts DevPanel="none" />` 禁用指定内置脚本。
- 使用 `<cs:Scripts><cs:Script Src="/js/pages/home.js" /></cs:Scripts>` 追加页面脚本。
- `jquery-lite.js` 提供本地 jQuery 兼容层。
- `jquery.behaviors.js` 管理依赖 jQuery 兼容层的 `data-ui` 行为。
- `ui.behaviors.js` 管理原生渐进增强行为。
- `codespirit.runtime.js` 管理 `data-cs-*` MVVM 绑定、命令、局部刷新、表单控件值、校验和错误回显。
- `codespirit.expression.js` 管理 `data-cs-show`、`data-cs-enable`、`data-cs-refresh`、`data-cs-confirm`、`data-cs-source`、`data-cs-attr`、`data-cs-visible`、`data-cs-hidden`、`data-cs-enabled`、`data-cs-disabled` 表达式和运行时行为。
- `codespirit.intent.js` 管理 `data-cs-intent` 和 `data-cs-scene` 识别。
- Dev Panel 在 `?dev=1` 下支持实时编辑常用 `data-cs-*`、`data-ui`、`class`、`style`、文本和 HTML；`Ctrl/Cmd + Shift + C` 切换页面拾取，`Ctrl/Cmd + S` 同步当前预览到 ASPX。

前端运行时稳定 API：

| API | 用途 |
|-----|------|
| `CodeSpirit.vm(root)` | 获取链式 ViewModel 操作对象 |
| `CodeSpirit.input(element)` | 通知 MVVM 某个控件值已变化 |
| `CodeSpirit.applyState(root, state)` | 将服务端 state 应用到绑定元素 |
| `CodeSpirit.applyRegions(root, regions)` | 替换 `data-cs-region` 局部区域 |
| `CodeSpirit.mount(root)` | 初次初始化 DOM root |
| `CodeSpirit.refresh(root)` | 动态更新后重新初始化 DOM root |
| `CodeSpirit.ui.register(name, initializer)` | 注册可复用 `data-ui` 行为 |
| `CodeSpirit.qs(selector, root)` / `CodeSpirit.qsa(selector, root)` | 查询作用域内单个或多个元素 |
| `CodeSpirit.on(root, eventName, selector, handler)` | 注册事件委托并返回取消订阅函数 |
| `CodeSpirit.debounce(fn, wait)` / `CodeSpirit.throttle(fn, wait)` | 防抖和节流高频前端交互 |
| `CodeSpirit.ready(callback)` | DOM 就绪后执行回调 |
| `CodeSpirit.state(root)` / `CodeSpirit.set(root, name, value)` / `CodeSpirit.batch(root, changes)` | 读取、写入和批量更新 ViewModel 状态 |

## 默认样式与场景

`wwwroot/css/site.css` 是内置默认视觉系统。普通 HTML 和 CodeSpirit 标签无需页面 CSS 即具备现代化外观。

内置场景：`dashboard`、`library`、`admin`、`commerce`、`content`、`analytics`、`crm`、`finance`、`education`、`healthcare`、`logistics`、`developer`、`hr`、`manufacturing`、`hospitality`、`real-estate`、`legal`、`support`、`supply-chain`、`research`、`security`、`retail`、`insurance`、`ngo`。

显式指定场景：

```html
<main data-cs-scene="library">
  <h1>Library Admin</h1>
</main>
```

低置信度识别会写入 `data-cs-scene-confidence="low"`、`data-cs-scene-candidate` 和 `data-cs-scene-score`。使用 `CodeSpirit.theme.exportTokens(root)` 可导出当前场景下的 `--cs-*` 主题 token。

## CSV 导入导出

`/admin` 页面支持馆藏 CSV 导入导出。

```text
ISBN,Title,Author,Category,Location,PublishedYear,CopyCount,Rating
```

- 已存在的 ISBN 会更新馆藏记录。
- 新 ISBN 会新增馆藏记录。
- 缺少 title 或 author 的行会跳过并显示 notice。

## VSIX 代码片段

### C# 快捷方式

| 快捷方式 | 输出 |
|----------|------|
| `csaop` | `[Transactional]` / `[Cacheable]` AOP 拦截器 |
| `csapp` | Program.cs 入口 |
| `csbind` | `[Bind]` 属性（支持单向/双向） |
| `cshttp` | `[Endpoint]` / `[HttpGet]` / `[HttpPost]` 声明式 HTTP |
| `cslife` | `[BeforeLoad]` / `[AfterLoad]` 页面生命周期钩子 |
| `csmod` | CodeSpiritModule 模板 |
| `csrepo` | `[Repository]` 仓储模板 |
| `cssched` | `[Scheduled]` / `[Every]` / `[OnStartup]` 定时任务 |
| `cssvc` | Service 模板（含 [Value] 和 AOP） |
| `csvm` | ViewModel 模板（含生命周期钩子） |
| `csval` | `[Value("key")]` 配置注入 |

### HTML 快捷方式

| 快捷方式 | 输出 |
|----------|------|
| `cscard` | `<cs:Card>` 默认卡片容器 |
| `csconditional` | `<cs:Conditional Visible="{Binding ...}">` 条件渲染 |
| `cscrud` | `<cs:Crud>` 复合 CRUD 表单 |
| `csdash` | `<cs:Dashboard>` 复合仪表盘布局 |
| `csform` | `<cs:Form>` 表单 |
| `csgrid` | `<cs:Grid>` 响应式网格布局 |
| `cslink` | `<cs:Link NavigateTo="{Binding ...}">` 安全链接 |
| `csmetric` | `<cs:MetricCard>` 指标卡片组 |
| `csmodal` | `<cs:Modal>` 弹窗 |
| `cspager` | `<cs:Pager>` 分页 |
| `csqlinks` | `<cs:QuickLinks>` 快捷入口卡片 |
| `csregion` | `<cs:Region>` 局部刷新区域 |
| `csrepeater` | `<cs:Repeater>` 列表迭代 |
| `csscript` | `<cs:Script>` 页面脚本 |
| `csscripts` | `<cs:Scripts>` 脚本资源容器 |
| `csstack` | `<cs:Stack>` Flex 堆叠布局 |
| `csactivity` | `<cs:ActivityFeed>` 活动流 |
| `cschart` | `<cs:Chart>` 图表组件 |
| `cstable` | `<cs:Table Columns="Property:Header[:Format]">` 表格简写 |
| `cstablex` | `<cs:Table>` 含 `<cs:Column>` 子标签显式列定义 |
| `cstabs` | `<cs:Tabs>` 标签页 |
| `cstoolbar` | `<cs:Toolbar>` 工具栏 |
| `cstree` | `<cs:Tree>` 可折叠树形结构组件 |
| `cswizard` | `<cs:Wizard>` 可点击分步向导组件 |

`wwwroot/js/codespirit.d.ts` 提供前端 TypeScript IntelliSense，覆盖运行时 API、UI 行为、主题、意图、表达式引擎、校验规则以及 `codespirit:tree-toggle`、`codespirit:wizard-step` 事件 detail。

### VS Code JavaScript 快捷方式

| 快捷方式 | 输出 |
|----------|------|
| `cs-qs` | `CodeSpirit.qs` / `CodeSpirit.qsa` 作用域查询 |
| `cs-on` | `CodeSpirit.on` 事件委托 |
| `cs-batch` | `CodeSpirit.batch` 批量状态更新 |
| `cs-vm-chain` | `CodeSpirit.vm(root)` 链式调用 |
| `cs-tree-event` | `codespirit:tree-toggle` 事件监听 |
| `cs-wizard-event` | `codespirit:wizard-step` 事件监听 |

## 无人值守开发

执行任务前阅读 `.monkeycode/docs/unattended-development.md`。

关键规则：

- 修改本项目运行时、页面、样式或脚本时，同步 VSIX 模板目录。
- 修改前端行为后更新 `scripts/validate-js-boundary.js`。
- 运行 JS 边界验证和解决方案测试。
- 只有用户明确要求时才提交或推送。

## 验证

```bash
node scripts/validate-js-boundary.js
dotnet build CodeSpirit.LibraryManagement.csproj
```
