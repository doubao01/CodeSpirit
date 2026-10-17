# CodeSpirit

CodeSpirit 是一个面向 .NET 10 的约定式应用框架，目标是把 Spring Boot 式开发体验、WPF 式 MVVM 页面绑定、内置前端运行时和默认视觉系统组合成一套可无人值守生成企业应用的框架。

核心原则：约定优先、属性驱动、默认可用、边界清晰、生成后可验证。

## 快速开始

### 从源码运行

```bash
dotnet build src/CodeSpirit.slnx
dotnet run --project src/CodeSpirit.LibraryManagement
```

默认示例应用监听 `http://0.0.0.0:8000`。

### 使用项目模板

```bash
dotnet new install CodeSpirit.Templates
dotnet new codespirit-library -n CodeSpirit.LibraryManagement
cd CodeSpirit.LibraryManagement
dotnet run
```

### 使用 Visual Studio VSIX

1. 构建 `src/Templates/CodeSpiritVsixTemplate`。
2. 安装生成的 `.vsix`。
3. 在 Visual Studio 新建项目中搜索 `CodeSpirit`。

## 框架能力

| 能力 | CodeSpirit 入口 | 说明 |
|------|-----------------|------|
| 应用启动 | `[CodeSpiritApplication]` | 一行接入自动配置、模块扫描、路由和中间件 |
| 服务注册 | `[Service]` | Source Generator 生成 DI 注册代码 |
| 依赖注入 | `[Autowired]` | 支持属性和字段注入 |
| 配置绑定 | `[Value("key")]` | 从配置读取强类型值 |
| 页面模型 | `ViewModel` + `[PageDirective]` | 以 ViewModel 驱动页面路由、状态和渲染 |
| 双向绑定 | `[Bind]` | 前端 `data-cs-bind` 与后端状态同步 |
| 命令事件 | `[Command]` | 前端提交 `data-cs-command` 调用后端命令 |
| 局部刷新 | `cs:Region` | 命令响应返回区域 HTML patch |
| 定时任务 | `[Scheduled]`、`[Every]`、`[OnStartup]` | 原生 async/await 后台任务 |
| AOP | `[Transactional]`、`[Cacheable]` | Castle DynamicProxy 拦截器 |
| 监控端点 | `/actuator/*` | Health、metrics、info |
| 默认前端 | `site.css`、`codespirit.runtime.js`、`codespirit.intent.js` | 页面无需写 CSS 即具备基础视觉和交互 |
| 表达式引擎 | `data-cs-show`、`data-cs-enable`、`data-cs-refresh` | 内置微型表达式引擎，无额外依赖 |
| 语义布局 | `<cs:Grid>`、`<cs:Card>`、`<cs:Stack>` | 消灭页面级布局 CSS |
| 复合组件 | `<cs:Crud>`、`<cs:Dashboard>`、`<cs:MetricCard>`、`<cs:ActivityFeed>`、`<cs:QuickLinks>` | 自动生成 CRUD 和大屏 |
| 场景识别 | `data-cs-scene`、`data-cs-intent` | 24 类通用业务场景自动识别与主题切换 |
| 内置交互 | `data-cs-confirm`、`data-cs-source`、`cs:Tabs`、`cs:Wizard` | 确认对话框、选项数据源协议、ARIA 标签页/向导与方向键导航 |

## 最小应用

```csharp
[CodeSpiritApplication]
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddCodeSpirit();
        var app = builder.Build();
        app.UseCodeSpirit();
        app.Run();
    }
}
```

## MVVM 页面模型

```csharp
[PageDirective(Route = "/customers", Title = "Customers")]
[Service]
public class CustomerViewModel : ViewModel
{
    [FromQuery]
    [Bind(BindDirection.TwoWay)]
    public string? Search { get; set; }

    [Bind]
    public List<Customer> Customers { get; set; } = [];

    public override Task LoadAsync()
    {
        Customers = Repository.Search(Search);
        return Task.CompletedTask;
    }

    [Command]
    public void Refresh()
    {
        Customers = Repository.Search(Search);
    }
}
```

```html
<cs:Form>
  <cs:Field Name="Search" Label="Search" Placeholder="Customer name" />
  <cs:Button Command="Refresh">Search</cs:Button>
</cs:Form>

<cs:Table Items="{Binding Customers}" Columns="Id:Id,Name:Name,Status:Status" />
```

ASPX 渲染支持嵌套绑定路径和列表索引，例如 `{Binding User.Profile.Name}`、`{Binding Books[0].Title}`。`<cs:Repeater>` 内可以继续使用 `<cs:Card>`、`<cs:Show>` 等 CodeSpirit 组件，子模板会使用当前 item 状态渲染。

## 内置前端边界

| 边界 | 所属层 | 规则 |
|------|--------|------|
| `data-cs-*` | CodeSpirit MVVM runtime | 只处理状态绑定、命令提交、局部刷新、错误回显 |
| `data-ui` | UI behavior layer | 只处理视觉组件增强、事件交互、第三方控件初始化 |
| `data-cs-intent` | Intent analyzer | 只处理值语义和状态色调 |
| `data-cs-scene` | Scene analyzer | 只处理页面场景和默认视觉主题 |
| 页面 CSS | 应用层 | 只补充业务特有布局，优先复用内置样式 |

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

`VmChain.get()` 和 `VmChain.state()` 会正确处理 checkbox、radio 和 multiple select；`VmChain.validate()` 支持 `required`、`minLength`、`maxLength`、`pattern`、`email`、`min`、`max` 和 `custom`。

## 表达式引擎

内置微型表达式引擎在 `codespirit.runtime.js` 中，体积不超过 3KB，无需额外依赖。

| 属性 | 作用 |
|------|------|
| `data-cs-show="Expr"` | 根据表达式求值结果显示/隐藏元素 |
| `data-cs-enable="Expr"` | 根据表达式求值结果启用/禁用控件 |
| `data-cs-refresh="Region"` | 值变更后自动触发 `cs:Region` 命令刷新 |
| `data-cs-confirm="消息"` | 内置确认对话框，无需 jQuery 行为扩展 |
| `data-cs-source="Command"` | 自动通过命令获取选项数据 |
| `data-cs-attr="Field:attr"` | 将字段值同步到安全 HTML 属性 |
| `data-cs-visible` / `data-cs-hidden` | 根据字段值显示或隐藏元素 |
| `data-cs-enabled` / `data-cs-disabled` | 根据字段值启用或禁用控件 |

支持运算符：`> < >= <= == != && \|\| ! contains empty ? :`

## 默认视觉系统

`wwwroot/css/site.css` 是框架内置默认样式。普通 HTML 和 CodeSpirit 标签在没有页面 CSS 的情况下也会获得现代化外观。

默认覆盖元素：`h1`、`h2`、`h3`、`p`、`a`、`form`、`label`、`input`、`select`、`textarea`、`button`、`section`、`article`、`table`、`ul`、`ol`、`blockquote`、`code`、`kbd`、`pre`、`hr`、`fieldset`、`legend`、`figure`、`figcaption`、`img`、`details`、`summary`、`nav`、`aside`、`header`、`footer`、`small`、`strong`、`em`、`address`、`progress`、`meter`。

内置 UI 原语：`.cs-card`、`.cs-panel`、`.cs-grid`、`.cs-stack`、`.cs-btn`、`.cs-badge`、`.cs-alert`、`.cs-toolbar`、`.cs-tabs`、`.cs-modal`、`.cs-pager`。

## 场景识别

`wwwroot/js/ui/codespirit.intent.js` 会读取标题、表头、标签、按钮、命令名、placeholder、class、页面标题和路径，为根节点添加场景类。

| 场景类 | 识别场景 |
|--------|----------|
| `cs-scene-dashboard` | 大屏、看板、驾驶舱、KPI、实时监控 |
| `cs-scene-library` | 图书、馆藏、读者、借阅、预约 |
| `cs-scene-admin` | 后台、管理、CRUD、筛选、状态表 |
| `cs-scene-commerce` | 电商、商品、订单、购物车、支付 |
| `cs-scene-content` | CMS、文章、博客、发布流 |
| `cs-scene-analytics` | 报表、统计、分析、增长 |
| `cs-scene-crm` | 客户、线索、商机、销售管道 |
| `cs-scene-finance` | 财务、账单、发票、预算、收支 |
| `cs-scene-education` | 教育、课程、学生、考试 |
| `cs-scene-healthcare` | 医疗、患者、医生、预约、诊断 |
| `cs-scene-logistics` | 物流、仓储、配送、运单、车队 |
| `cs-scene-developer` | API、Webhook、SDK、日志、部署 |
| `cs-scene-hr` | 人事、招聘、员工、考勤、薪酬 |
| `cs-scene-manufacturing` | 制造、生产、工单、产线、质检 |
| `cs-scene-hospitality` | 酒店、房间、入住、预订、宾客 |
| `cs-scene-real-estate` | 房产、楼盘、租赁、物业、租户 |
| `cs-scene-legal` | 法务、合同、案件、合规、风险 |
| `cs-scene-support` | 客服、工单、服务台、SLA、队列 |
| `cs-scene-supply-chain` | 供应链、采购、供应商、库存、补货 |
| `cs-scene-research` | 研发、实验、论文、专利、项目管理 |
| `cs-scene-security` | 安全、审计、日志、告警、合规 |
| `cs-scene-retail` | 零售、门店、POS、收银、会员 |
| `cs-scene-insurance` | 保险、保单、理赔、核保、精算 |
| `cs-scene-ngo` | 公益、项目、捐赠、志愿者、受益人 |

显式指定场景：

```html
<main data-cs-scene="dashboard">
  <h1>Realtime Operations Screen</h1>
</main>
```

调试与主题能力：

- 低置信度识别会写入 `data-cs-scene-confidence="low"`、`data-cs-scene-candidate` 和 `data-cs-scene-score`。
- `CodeSpirit.theme.exportTokens(root)` 可导出当前场景下的 `--cs-*` 主题 token。
- `CodeSpirit.theme.tokens(root)` 是同一能力的简写。

## 无人值守开发

无人值守开发的执行文档位于 `.monkeycode/docs/unattended-development.md`。

执行原则：

- 先读文档和项目记忆，再动代码。
- 修改 LibraryManagement 运行时、页面、样式或脚本时，同步 VSIX 模板目录。
- 每次实现后运行 JS 边界验证和解决方案测试。
- 只在用户明确要求时提交或推送。
- 不输出密钥，不执行破坏性命令，不绕过框架边界。

## 项目结构

```text
src/
├── CodeSpirit.Core/
├── CodeSpirit.Infrastructure/
├── CodeSpirit.SourceGenerator/
├── CodeSpirit.Modules/
├── CodeSpirit.Host/
├── CodeSpirit.LibraryManagement/
├── CodeSpirit.Tests/
└── Templates/
    └── CodeSpiritVsixTemplate/
```

示例项目结构：

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

## 验证命令

```bash
node src/CodeSpirit.LibraryManagement/scripts/validate-js-boundary.js
dotnet test src/CodeSpirit.slnx
dotnet build src/CodeSpirit.slnx
```

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
| `cstable` | `<cs:Table Columns="Property:Header[:Format]">` 表格简写 |
| `cstablex` | `<cs:Table>` 含 `<cs:Column>` 子标签显式列定义 |
| `cstabs` | `<cs:Tabs>` 标签页 |
| `cstoolbar` | `<cs:Toolbar>` 工具栏 |
| `cschart` | `<cs:Chart>` 图表组件（支持 bar/line/pie 等类型） |
| `cstree` | `<cs:Tree>` 树形结构组件 |
| `cswizard` | `<cs:Wizard>` 分步向导组件 |

### VS Code 支持

在项目根目录的 `.vscode/codespirit.code-snippets` 中提供 23 个 JavaScript 和 HTML 的 VS Code snippets，覆盖运行时 API、VmChain、UI 行为、主题、意图、事件名、ASPX 标签、data-cs-* 属性、data-ui 属性、表达式片段、作用域查询、事件委托和批量状态更新。`wwwroot/js/codespirit.d.ts` 提供 TypeScript IntelliSense，包含快捷 runtime API 以及 `codespirit:tree-toggle`、`codespirit:wizard-step` 事件 detail 类型。

Dev Panel 在 `?dev=1` 下支持编辑常用 `data-cs-*`、`data-ui`、`class`、`style`、文本和 HTML。`Ctrl/Cmd + Shift + C` 切换页面拾取，`Ctrl/Cmd + S` 将当前预览同步回 ASPX 文件。

## 运行要求

- .NET 10 SDK
- Visual Studio 2022 17.10+ 或 VS2026，用于 VSIX 模板开发
- Node.js，用于 `validate-js-boundary.js` 边界验证

## License

MIT
