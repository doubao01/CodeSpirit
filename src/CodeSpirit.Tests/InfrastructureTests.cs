using CodeSpirit.Core.Attributes;
using CodeSpirit.Core.Abstractions;
using CodeSpirit.Core.Interfaces;
using CodeSpirit.Infrastructure.AutoConfiguration;
using CodeSpirit.Infrastructure.Aop;
using CodeSpirit.Infrastructure.Page;
using CodeSpirit.Infrastructure;
using CodeSpirit.Infrastructure.Mvvm;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Json;
using System.Reflection;

namespace CodeSpirit.Tests;

public class AopExtensionsTests
{
    [Fact]
    public void NeedsAopProxy_WithTransactionalMethod_ReturnsTrue()
    {
        Assert.True(AopExtensions.NeedsAopProxy(typeof(TransactionalService)));
    }

    [Fact]
    public void NeedsAopProxy_WithCacheableMethod_ReturnsTrue()
    {
        Assert.True(AopExtensions.NeedsAopProxy(typeof(CacheableService)));
    }

    [Fact]
    public void NeedsAopProxy_WithoutAopAttributes_ReturnsFalse()
    {
        Assert.False(AopExtensions.NeedsAopProxy(typeof(PlainService)));
    }

    [Fact]
    public void NeedsAopProxy_WithBothAttributes_ReturnsTrue()
    {
        Assert.True(AopExtensions.NeedsAopProxy(typeof(MixedAopService)));
    }

    [Service]
    class TransactionalService
    {
        [Transactional]
        public virtual void DoWork() { }
    }

    [Service]
    class CacheableService
    {
        [Cacheable(ExpirationSeconds = 60, CacheKey = "test")]
        public virtual int GetValue() => 42;
    }

    [Service]
    class PlainService
    {
        public void DoWork() { }
    }

    [Service]
    class MixedAopService
    {
        [Transactional]
        public virtual void Save() { }

        [Cacheable]
        public virtual string Get() => "cached";
    }
}

public class ModuleLoaderTests
{
    private readonly ModuleLoader _loader = new();

    [Fact]
    public void ResolveModuleTopology_SingleModule_ReturnsIt()
    {
        var types = _loader.ResolveModuleTopology(typeof(SingleModule).Assembly);
        Assert.Contains(typeof(SingleModule), types);
    }

    [Fact]
    public void ResolveModuleTopology_DependencyOrderedCorrectly()
    {
        var types = _loader.ResolveModuleTopology(typeof(ParentModule).Assembly);

        var parentIdx = types.IndexOf(typeof(ParentModule));
        var childIdx = types.IndexOf(typeof(ChildModule));

        Assert.True(childIdx < parentIdx, "ChildModule should be resolved before ParentModule because ParentModule depends on it");
    }

    [Fact]
    public void ResolveModuleTopology_NoDependencies_ReturnsStandalone()
    {
        var types = _loader.ResolveModuleTopology(typeof(StandaloneModule).Assembly);
        Assert.Contains(typeof(StandaloneModule), types);
    }

    [Fact]
    public void ResolveModuleTopology_ChainDependency_PreservesOrder()
    {
        var types = _loader.ResolveModuleTopology(typeof(GrandparentModule).Assembly);

        var grandparentIdx = types.IndexOf(typeof(GrandparentModule));
        var parentIdx = types.IndexOf(typeof(ParentChainModule));
        var childIdx = types.IndexOf(typeof(ChildChainModule));

        Assert.True(childIdx < parentIdx);
        Assert.True(parentIdx < grandparentIdx);
    }
}

public class AutoServiceRegistrarTests
{
    private readonly AutoServiceRegistrar _registrar = new();

    [Fact]
    public void RegisterServices_WithServiceAttribute_RegistersService()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        _registrar.RegisterServices(services, typeof(SampleService).Assembly);

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(ISampleService) || s.ServiceType == typeof(SampleService));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void RegisterServices_WithRepositoryAttribute_RegistersRepository()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        _registrar.RegisterServices(services, typeof(SampleRepository).Assembly);

        var descriptor = services.FirstOrDefault(s =>
            s.ImplementationType == typeof(SampleRepository));
        Assert.NotNull(descriptor);
    }
}

// Test module classes for ModuleLoader tests
public class SingleModule : CodeSpiritModule { }

[DependsOn(typeof(ChildModule))]
public class ParentModule : CodeSpiritModule { }

public class ChildModule : CodeSpiritModule { }

public class StandaloneModule : CodeSpiritModule { }

[DependsOn(typeof(ParentChainModule))]
public class GrandparentModule : CodeSpiritModule { }

[DependsOn(typeof(ChildChainModule))]
public class ParentChainModule : CodeSpiritModule { }

public class ChildChainModule : CodeSpiritModule { }

// Test service classes for AutoServiceRegistrar tests
public interface ISampleService { }

[Service(ServiceType = typeof(ISampleService))]
public class SampleService : ISampleService { }

[Repository]
public class SampleRepository { }

public class ShowTagTests
{
    [Fact]
    public void Show_Visible_GeneratesDataCsVisible()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding IsActive}\">Hello</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-visible=\"IsActive\"", html);
        Assert.Contains(">Hello<", html);
    }

    [Fact]
    public void Show_HideWhen_GeneratesDataCsHidden()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show HideWhen=\"{Binding IsChecked}\">Content</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-hidden=\"IsChecked\"", html);
    }

    [Fact]
    public void Show_Class_GeneratesDataCsClass()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Class=\"{Binding IsActive}\" ClassName=\"active\">Item</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-class=\"IsActive:active\"", html);
    }

    [Fact]
    public void Show_ClassWithoutClassName_UsesFieldName()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Class=\"{Binding IsActive}\">Item</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-class=\"IsActive\"", html);
    }

    [Fact]
    public void Show_TagAttribute_RespectsCustomTag()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Ready}\" Tag=\"span\">Label</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("<span", html);
        Assert.Contains("</span>", html);
    }

    [Fact]
    public void Show_InvalidTagAttribute_FallsBackToDiv()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Ready}\" Tag=\"1script\">Label</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("<div", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Show_UnsafeTagAttribute_FallsBackToDiv()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Ready}\" Tag=\"script\">Label</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("<div", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Show_DefaultTag_IsDiv()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Ready}\">Label</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("<div", html);
    }

    [Fact]
    public void Show_MultipleBindings_GeneratesAllAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Active}\" Class=\"{Binding Theme}\" ClassName=\"dark\">Box</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-visible=\"Active\"", html);
        Assert.Contains("data-cs-class=\"Theme:dark\"", html);
    }

    [Fact]
    public void Show_PassesThroughExtraAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Ready}\" Id=\"panel\" Style=\"display:block\">Body</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains("Id=\"panel\"", html);
        Assert.Contains("Style=\"display:block\"", html);
    }

    [Fact]
    public void Show_DropsUnsafeEventAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Ready}\" onclick=\"alert(1)\" srcdoc=\"<p>x</p>\">Body</cs:Show>",
            new Dictionary<string, object?>());

        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("srcdoc", html, StringComparison.OrdinalIgnoreCase);
    }
}

public class PageParserTests
{
    [Fact]
    public void ParseFromFile_ReadsStandardAspxPageDirective()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"codespirit-page-{Guid.NewGuid():N}.aspx");
        File.WriteAllText(filePath, "<%@ Page Route=\"/customers\" Title=\"Customer Portal\" Layout=\"Pages/Customer.master\" %>");

        var descriptor = new PageParser().ParseFromFile(filePath);

        Assert.NotNull(descriptor);
        Assert.Equal("/customers", descriptor.Route);
        Assert.Equal("Customer Portal", descriptor.Title);
        Assert.Equal("Pages/Customer.master", descriptor.Layout);
    }

    [Fact]
    public void ParseFromFile_IgnoresFilesWithoutPageDirective()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"codespirit-page-{Guid.NewGuid():N}.aspx");
        File.WriteAllText(filePath, "<div>No directive</div>");

        var descriptor = new PageParser().ParseFromFile(filePath);

        Assert.Null(descriptor);
    }

    [Fact]
    public void ParseFromFile_WithPrecedingCommentOrDirective_ParsesPageDirective()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"codespirit-page-{Guid.NewGuid():N}.aspx");
        File.WriteAllText(filePath, "<!-- Comment --><%@ Import Namespace=\"System\" %><%@ Page Route=\"/profile\" Title=\"User Profile\" %>");

        var descriptor = new PageParser().ParseFromFile(filePath);

        Assert.NotNull(descriptor);
        Assert.Equal("/profile", descriptor.Route);
        Assert.Equal("User Profile", descriptor.Title);
    }
}

public class PageRendererTagTests
{
    [Fact]
    public void Region_RendersDataRegionAndKeepsAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Region Name=\"orders\" Tag=\"section\" class=\"panel\">Body</cs:Region>",
            new Dictionary<string, object?>());

        Assert.Contains("<section data-cs-region=\"orders\" class=\"panel\">Body</section>", html);
    }

    [Fact]
    public void Region_InvalidTagAttribute_FallsBackToDiv()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Region Name=\"orders\" Tag=\"1script\">Body</cs:Region>",
            new Dictionary<string, object?>());

        Assert.Contains("<div data-cs-region=\"orders\">Body</div>", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void FormFieldButton_RenderRuntimeConnectionAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Form class=\"search\"><cs:Field Name=\"Query\" Label=\"Search\" Placeholder=\"Keyword\" /><cs:Button Command=\"Search\">Go</cs:Button></cs:Form>",
            new Dictionary<string, object?> { ["Query"] = "clean" });

        Assert.Contains("<form method=\"post\" data-cs-vm class=\"search\">", html);
        Assert.Contains("name=\"Query\"", html);
        Assert.Contains("value=\"clean\"", html);
        Assert.Contains("data-cs-bind=\"Query\"", html);
        Assert.Contains("data-cs-command=\"Search\"", html);
    }

    [Fact]
    public void Binding_NestedDictionaryPath_RendersValue()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<p>{Binding User.Profile.Name}</p>",
            new Dictionary<string, object?>
            {
                ["User"] = new Dictionary<string, object?>
                {
                    ["Profile"] = new Dictionary<string, object?> { ["Name"] = "Ada" }
                }
            });

        Assert.Contains("<p>Ada</p>", html);
    }

    [Fact]
    public void Binding_ListIndexPath_RendersValue()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<p>{Binding Books[1].Title}</p>",
            new Dictionary<string, object?>
            {
                ["Books"] = new[]
                {
                    new { Title = "First" },
                    new { Title = "Second" }
                }
            });

        Assert.Contains("<p>Second</p>", html);
    }

    [Fact]
    public void Repeater_RendersNestedComponentsWithItemState()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Books}\"><cs:Card Tone=\"{Binding Tone}\"><span>{Binding Title}</span></cs:Card></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["Books"] = new[]
                {
                    new Dictionary<string, object?> { ["Title"] = "Domain Design", ["Tone"] = "success" }
                }
            });

        Assert.Contains("cs-card cs-card-success", html);
        Assert.Contains("<span>Domain Design</span>", html);
        Assert.DoesNotContain("<cs:Card", html);
    }

    [Fact]
    public void RenderTemplate_RepeatedSameState_TracksCacheHit()
    {
        PageRenderer.ClearRenderCache();
        var state = new Dictionary<string, object?> { ["Title"] = "Fast" };

        PageRenderer.RenderTemplateForTests("<p>{Binding Title}</p>", state);
        PageRenderer.RenderTemplateForTests("<p>{Binding Title}</p>", state);

        var metrics = PageRenderer.GetRenderMetricsForTests();
        Assert.True(metrics.RenderCacheEntries >= 1);
        Assert.True(metrics.RenderCacheHits >= 1);
        Assert.True(metrics.RenderCacheMisses >= 1);
    }

    [Fact]
    public void Binding_RepeatedNestedPath_UsesPathCache()
    {
        PageRenderer.ClearRenderCache();

        PageRenderer.RenderTemplateForTests(
            "<p>{Binding User.Profile.Name}</p>",
            new Dictionary<string, object?>
            {
                ["User"] = new Dictionary<string, object?>
                {
                    ["Profile"] = new Dictionary<string, object?> { ["Name"] = "Ada" }
                }
            });

        var metrics = PageRenderer.GetRenderMetricsForTests();
        Assert.True(metrics.PathCacheEntries >= 1);
    }

    [Fact]
    public void Link_DropsUnsafeJavascriptUrl()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Link NavigateTo=\"javascript:alert(1)\">Bad</cs:Link>",
            new Dictionary<string, object?>());

        Assert.Contains("href=\"#\"", html);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Form_InvalidMethod_FallsBackToPost()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Form Method=\"dialog\">Body</cs:Form>",
            new Dictionary<string, object?>());

        Assert.Contains("<form method=\"post\" data-cs-vm>", html);
    }

    [Fact]
    public void Button_WithSingleQuotesAndCustomClass_RendersCorrectly()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Button Command='Save' class='btn-success'>Save Item</cs:Button>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-command=\"Save\"", html);
        Assert.Contains("class=\"btn-success\"", html);
        Assert.Contains("Save Item", html);
    }

    [Fact]
    public void Conditional_WithSingleQuotesAndVisibleBinding_RendersCorrectly()
    {
        var htmlTrue = PageRenderer.RenderTemplateForTests(
            "<cs:Conditional Visible='{Binding HasData}'><span>Has Content</span></cs:Conditional>",
            new Dictionary<string, object?> { ["HasData"] = true });

        Assert.Contains("<span>Has Content</span>", htmlTrue);

        var htmlFalse = PageRenderer.RenderTemplateForTests(
            "<cs:Conditional Visible='{Binding HasData}'><span>Has Content</span></cs:Conditional>",
            new Dictionary<string, object?> { ["HasData"] = false });

        Assert.DoesNotContain("<span>Has Content</span>", htmlFalse);
    }

    [Fact]
    public void Repeater_WithSingleQuotes_RendersCorrectly()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items='{Binding Items}'><span>{Binding Name}</span></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["Items"] = new[] { new { Name = "Alpha" }, new { Name = "Beta" } }
            });

        Assert.Contains("<span>Alpha</span>", html);
        Assert.Contains("<span>Beta</span>", html);
    }

    [Fact]
    public void Field_SelectWithOptions_RendersSelectOptions()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Field Name='Role' Type='select' Options='admin:Administrator,user:Standard User' Placeholder='Choose a role' />",
            new Dictionary<string, object?> { ["Role"] = "admin" });

        Assert.Contains("<select id=\"Role\" name=\"Role\" data-cs-bind=\"Role\">", html);
        Assert.Contains("<option value=\"\">Choose a role</option>", html);
        Assert.Contains("<option value=\"admin\" selected>Administrator</option>", html);
        Assert.Contains("<option value=\"user\">Standard User</option>", html);
    }

    [Fact]
    public void Field_CheckboxType_RendersCheckbox()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Field Name='IsActive' Type='checkbox' Label='Active Status' />",
            new Dictionary<string, object?> { ["IsActive"] = true });

        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("checked", html);
        Assert.Contains("Active Status", html);
    }

    [Fact]
    public void Scripts_RendersDefaultBuiltInAssets()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts />",
            new Dictionary<string, object?>());

        Assert.Contains("/js/codespirit.runtime.js", html);
        Assert.Contains("/js/codespirit.expression.js", html);
        Assert.Contains("/js/vendor/jquery-lite.js", html);
        Assert.Contains("/js/ui/ui.behaviors.js", html);
        Assert.Contains("/js/ui/codespirit.intent.js", html);
        Assert.Contains("/js/ui/codespirit.devpanel.js", html);
        Assert.Contains("/js/site.js", html);
    }

    [Fact]
    public void Scripts_AllowsAssetReplacementAndDisable()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts Runtime=\"/custom/runtime.js\" DevPanel=\"none\" />",
            new Dictionary<string, object?>());

        Assert.Contains("/custom/runtime.js", html);
        Assert.DoesNotContain("/js/codespirit.runtime.js", html);
        Assert.DoesNotContain("/js/ui/codespirit.devpanel.js", html);
    }

    [Fact]
    public void Scripts_AppendsExtraScriptsAfterBuiltInsAndAppliesAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts Defer=\"true\" Type=\"module\" Extra=\"/js/pages/home.js,/js/custom.js\" />",
            new Dictionary<string, object?>());

        Assert.Contains("<script src=\"/js/codespirit.runtime.js\" type=\"module\" defer></script>", html);
        Assert.Contains("<script src=\"/js/pages/home.js\" type=\"module\" defer></script>", html);
        Assert.Contains("<script src=\"/js/custom.js\" type=\"module\" defer></script>", html);
        Assert.True(html.IndexOf("/js/site.js", StringComparison.Ordinal) < html.IndexOf("/js/pages/home.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Scripts_BlockAppendsNestedPageScriptsAfterBuiltIns()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts Defer=\"true\"><cs:Script Src=\"/js/pages/{Binding Page}.js\" /></cs:Scripts>",
            new Dictionary<string, object?> { ["Page"] = "home" });

        Assert.Contains("<script src=\"/js/codespirit.runtime.js\" defer></script>", html);
        Assert.Contains("<script src=\"/js/pages/home.js\" defer></script>", html);
        Assert.True(html.IndexOf("/js/site.js", StringComparison.Ordinal) < html.IndexOf("/js/pages/home.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Scripts_NestedScriptCanOverrideInheritedAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts Type=\"module\" Defer=\"true\"><cs:Script Src=\"/js/pages/home.js\" Type=\"text/javascript\" Defer=\"false\" /></cs:Scripts>",
            new Dictionary<string, object?>());

        Assert.Contains("<script src=\"/js/codespirit.runtime.js\" type=\"module\" defer></script>", html);
        Assert.Contains("<script src=\"/js/pages/home.js\" type=\"text/javascript\"></script>", html);
    }

    [Fact]
    public void Scripts_BlockPreservesRenderedRawPageScriptsAfterBuiltIns()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts><script src=\"/js/pages/{Binding Page}.js\"></script></cs:Scripts>",
            new Dictionary<string, object?> { ["Page"] = "home" });

        Assert.Contains("<script src=\"/js/pages/home.js\"></script>", html);
        Assert.True(html.IndexOf("/js/site.js", StringComparison.Ordinal) < html.IndexOf("/js/pages/home.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Scripts_DropsUnsafeExtraScriptUrls()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts Extra=\"javascript:alert(1),/js/safe.js\" />",
            new Dictionary<string, object?>());

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/safe.js", html);
    }

    [Fact]
    public void Script_RendersSafePageScriptWithAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Script Src=\"/js/pages/home.js\" Defer=\"true\" />",
            new Dictionary<string, object?>());

        Assert.Equal("<script src=\"/js/pages/home.js\" defer></script>", html);
    }

    [Fact]
    public void Script_DropsUnsafeSrc()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Script Src=\"javascript:alert(1)\" />",
            new Dictionary<string, object?>());

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Table_ColumnListInfersIntentForRuntimeStyling()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Table Items=\"{Binding Rows}\" Columns=\"Name:Name,Status:Status,FineBalance:Fine\" />",
            new Dictionary<string, object?>
            {
                ["Rows"] = new[] { new TableRow("Ada", "Active", 12.5m) }
            });

        Assert.Contains("<th>Name</th>", html);
        Assert.Contains("<span data-cs-intent=\"status\">Active</span>", html);
        Assert.Contains("<span data-cs-intent=\"numeric\">12.5</span>", html);
    }

    [Fact]
    public void Table_BlockColumnsRenderTemplates()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Table Items=\"{Binding Rows}\"><cs:Column Header=\"Status\"><span class=\"status-{Binding Status}\">{Binding Status}</span></cs:Column></cs:Table>",
            new Dictionary<string, object?>
            {
                ["Rows"] = new[] { new TableRow("Ada", "Active", 12.5m) }
            });

        Assert.Contains("<th>Status</th>", html);
        Assert.Contains("<span class=\"status-Active\">Active</span>", html);
    }

    [Fact]
    public void Table_EmptyBlockRendersInsteadOfRows()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Table Items=\"{Binding Rows}\"><cs:Column Name=\"Name\" Header=\"Name\" /><cs:Empty><strong>{Binding Hint}</strong></cs:Empty></cs:Table>",
            new Dictionary<string, object?>
            {
                ["Rows"] = Array.Empty<TableRow>(),
                ["Hint"] = "Nothing here yet"
            });

        Assert.Contains("class=\"cs-table-empty-row\"", html);
        Assert.Contains("<td colspan=\"1\"><strong>Nothing here yet</strong></td>", html);
        Assert.DoesNotContain("<td>Ada</td>", html);
    }

    [Fact]
    public void Table_EmptyTextSupportsStateBinding()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Table Items=\"{Binding Rows}\" Columns=\"Name:Name\" EmptyText=\"{Binding Hint}\" />",
            new Dictionary<string, object?>
            {
                ["Rows"] = Array.Empty<TableRow>(),
                ["Hint"] = "No books matched"
            });

        Assert.Contains("<div class=\"cs-empty\">No books matched</div>", html);
    }

    [Fact]
    public void Table_EmptyBlockDoesNotLeakIntoRenderedRows()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Table Items=\"{Binding Rows}\"><cs:Column Name=\"Name\" Header=\"Name\" /><cs:Empty>No data</cs:Empty></cs:Table>",
            new Dictionary<string, object?>
            {
                ["Rows"] = new[] { new TableRow("Ada", "Active", 12.5m) }
            });

        Assert.Contains("<td>Ada</td>", html);
        Assert.DoesNotContain("No data", html);
    }

    [Fact]
    public void LayoutTags_RenderSemanticClassesAndStyles()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Grid Columns=\"2\" Gap=\"lg\"><cs:Card Tone=\"blue\"><cs:Stack Direction=\"Row\" Align=\"Center\" Gap=\"sm\">Body</cs:Stack></cs:Card></cs:Grid>",
            new Dictionary<string, object?>());

        Assert.Contains("class=\"cs-grid cs-grid-2\"", html);
        Assert.Contains("style=\"gap: 1.5rem;\"", html);
        Assert.Contains("class=\"cs-card cs-card-blue\"", html);
        Assert.Contains("class=\"cs-stack cs-row\"", html);
        Assert.Contains("gap: 0.5rem; align-items: center;", html);
    }

    [Fact]
    public void Card_RendersHeaderWhenTitleOrSubtitleProvided()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Card Title=\"Quick Overview\" Subtitle=\"Key metrics summary\"><span>Content</span></cs:Card>",
            new Dictionary<string, object?>());

        Assert.Contains("class=\"cs-card\"", html);
        Assert.Contains("<header class=\"cs-card-header\">", html);
        Assert.Contains("<h3>Quick Overview</h3>", html);
        Assert.Contains("<p>Key metrics summary</p>", html);
        Assert.Contains("<span>Content</span>", html);
    }

    [Fact]
    public void Crud_RendersFormFieldsAndCommands()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Crud Entity=\"Book\" Title=\"Book Catalog\" Subtitle=\"Manage books\" Fields=\"BookId:Book Id:number,BookTitle:Title,ImportExportCsv:CSV:textarea:9\" Commands=\"AddBook:Add,ArchiveBook:Archive:Archive this book?\" />",
            new Dictionary<string, object?>
            {
                ["BookId"] = 7,
                ["BookTitle"] = "Clean Code",
                ["ImportExportCsv"] = "ISBN,Title"
            });

        Assert.Contains("class=\"cs-card cs-crud\"", html);
        Assert.Contains("<h2>Book Catalog</h2>", html);
        Assert.Contains("<p>Manage books</p>", html);
        Assert.Contains("name=\"BookId\" value=\"7\"", html);
        Assert.Contains("name=\"BookTitle\" value=\"Clean Code\"", html);
        Assert.Contains("<textarea id=\"ImportExportCsv\" name=\"ImportExportCsv\" data-cs-bind=\"ImportExportCsv\" rows=\"9\">ISBN,Title</textarea>", html);
        Assert.Contains("data-cs-command=\"AddBook\"", html);
        Assert.Contains("data-cs-command=\"ArchiveBook\"", html);
        Assert.Contains("data-cs-confirm=\"Archive this book?\"", html);
        Assert.Contains("cs-btn-danger", html);
    }

    [Fact]
    public void Crud_WithItemsAndColumns_RendersTableAlongsideForm()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Crud Entity=\"Book\" Title=\"Book Catalog\" Fields=\"BookTitle:Title\" Commands=\"AddBook:Add\" Items=\"{Binding Books}\" Columns=\"Id:ID,Title:Book Title\" />",
            new Dictionary<string, object?>
            {
                ["BookTitle"] = "Clean Architecture",
                ["Books"] = new[]
                {
                    new { Id = 1, Title = "Clean Code" },
                    new { Id = 2, Title = "Refactoring" }
                }
            });

        Assert.Contains("class=\"cs-card cs-crud\"", html);
        Assert.Contains("<h2>Book Catalog</h2>", html);
        Assert.Contains("name=\"BookTitle\" value=\"Clean Architecture\"", html);
        Assert.Contains("data-cs-command=\"AddBook\"", html);
        Assert.Contains("<table", html);
        Assert.Contains("<th>ID</th>", html);
        Assert.Contains("<th>Book Title</th>", html);
        Assert.Contains("<td>Clean Code</td>", html);
        Assert.Contains("<td>Refactoring</td>", html);
    }

    [Fact]
    public void Crud_BlockSyntaxWithInnerColumns_RendersTableFromChildColumns()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Crud Entity=\"Book\" Title=\"Book Catalog\" Fields=\"BookTitle:Title\" Commands=\"AddBook:Add\" Items=\"{Binding Books}\"><cs:Column Name=\"Title\" Header=\"Name\" /></cs:Crud>",
            new Dictionary<string, object?>
            {
                ["BookTitle"] = "Domain-Driven Design",
                ["Books"] = new[]
                {
                    new { Title = "Building Microservices" }
                }
            });

        Assert.Contains("class=\"cs-card cs-crud\"", html);
        Assert.Contains("<h2>Book Catalog</h2>", html);
        Assert.Contains("<th>Name</th>", html);
        Assert.Contains("<td>Building Microservices</td>", html);
    }

    [Fact]
    public void Crud_EmptyBlockRendersWhenItemsAreEmpty()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Crud Entity=\"Book\" Fields=\"BookTitle:Title\" Commands=\"AddBook:Add\" Items=\"{Binding Books}\"><cs:Column Name=\"BookTitle\" Header=\"Title\" /><cs:Empty><span>No books</span></cs:Empty></cs:Crud>",
            new Dictionary<string, object?>
            {
                ["BookTitle"] = string.Empty,
                ["Books"] = Array.Empty<object>()
            });

        Assert.Contains("class=\"cs-table-empty-row\"", html);
        Assert.Contains("<span>No books</span>", html);
        Assert.Contains("<th>Title</th>", html);
    }

    [Fact]
    public void DashboardComponents_RenderCompositeSections()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Dashboard Title=\"Library Command Center\" Subtitle=\"Live insights\"><cs:MetricCard Items=\"{Binding Metrics}\" /><cs:ActivityFeed Items=\"{Binding Activities}\" Title=\"Live Activity\" /><cs:QuickLinks Items=\"{Binding Cards}\" /></cs:Dashboard>",
            new Dictionary<string, object?>
            {
                ["Metrics"] = new[] { new { Label = "Books", Value = 42, Hint = "Available", Tone = "green" } },
                ["Activities"] = new[] { new { Time = "09:00", Text = "Borrowed Clean Code", Tone = "blue" } },
                ["Cards"] = new[] { new { Title = "Admin", Description = "Manage books", Url = "/admin" } }
            });

        Assert.Contains("class=\"cs-dashboard\"", html);
        Assert.Contains("<h1>Library Command Center</h1>", html);
        Assert.Contains("class=\"cs-grid cs-grid-3 cs-metrics\"", html);
        Assert.Contains("<strong data-cs-intent=\"numeric\">42</strong>", html);
        Assert.Contains("class=\"cs-card cs-activity-feed\"", html);
        Assert.Contains("Borrowed Clean Code", html);
        Assert.Contains("class=\"cs-card cs-quick-link\" href=\"/admin\"", html);
    }

    [Fact]
    public void DashboardComponents_SupportCustomFieldMappings()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Dashboard><cs:MetricCard Items=\"{Binding Metrics}\" Title=\"KPIs\" LabelField=\"Name\" ValueField=\"Count\" HintField=\"Caption\" ToneField=\"Status\" /><cs:ActivityFeed Items=\"{Binding Events}\" TimeField=\"When\" TextField=\"Message\" ToneField=\"Level\" /><cs:QuickLinks Items=\"{Binding Links}\" Title=\"Actions\" TitleField=\"Name\" DescriptionField=\"Caption\" UrlField=\"Href\" /></cs:Dashboard>",
            new Dictionary<string, object?>
            {
                ["Metrics"] = new[] { new { Name = "Loans", Count = 18, Caption = "Today", Status = "Success" } },
                ["Events"] = new[] { new { When = "10:30", Message = "Reader checked in", Level = "Info" } },
                ["Links"] = new[] { new { Name = "Unsafe", Caption = "Blocked", Href = "javascript:alert(1)" } }
            });

        Assert.Contains("<h2>KPIs</h2>", html);
        Assert.Contains("<span>Loans</span>", html);
        Assert.Contains("<strong data-cs-intent=\"numeric\">18</strong>", html);
        Assert.Contains("cs-metric-success", html);
        Assert.Contains("<time>10:30</time>", html);
        Assert.Contains("Reader checked in", html);
        Assert.Contains("cs-activity-info", html);
        Assert.Contains("<h2>Actions</h2>", html);
        Assert.Contains("<h3>Unsafe</h3>", html);
        Assert.Contains("href=\"#\"", html);
    }

    [Fact]
    public void Toolbar_RendersTitleSubtitleAndActions()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Toolbar Title=\"{Binding Title}\" Subtitle=\"Manage records\"><button>Add</button></cs:Toolbar>",
            new Dictionary<string, object?> { ["Title"] = "Books" });

        Assert.Contains("class=\"cs-toolbar\"", html);
        Assert.Contains("<h2>Books</h2>", html);
        Assert.Contains("<p>Manage records</p>", html);
        Assert.Contains("<div class=\"cs-toolbar-actions\"><button>Add</button></div>", html);
    }

    [Fact]
    public void Tabs_RendersActiveTabAndPanels()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Tabs Active=\"loans\"><cs:Tab Key=\"overview\" Title=\"Overview\">One</cs:Tab><cs:Tab Key=\"loans\" Title=\"Loans\">Two</cs:Tab></cs:Tabs>",
            new Dictionary<string, object?>());

        Assert.Contains("role=\"tablist\"", html);
        Assert.Contains("href=\"#loans\" class=\"cs-tab active\"", html);
        Assert.Contains("id=\"loans\" class=\"cs-tab-panel active\"", html);
        Assert.Contains("id=\"overview\" class=\"cs-tab-panel\"", html);
        Assert.Contains("id=\"cs-tab-2\" href=\"#loans\"", html);
        Assert.Contains("role=\"tab\" aria-controls=\"loans\"", html);
        Assert.Contains("aria-selected=\"true\" tabindex=\"0\"", html);
        Assert.Contains("role=\"tabpanel\" aria-labelledby=\"cs-tab-2\"", html);
        Assert.Contains("id=\"overview\" class=\"cs-tab-panel\" role=\"tabpanel\" aria-labelledby=\"cs-tab-1\" hidden", html);
    }

    [Fact]
    public void Modal_RendersDialogAndHiddenState()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Modal Id=\"edit-book\" Title=\"Edit Book\">Body</cs:Modal>",
            new Dictionary<string, object?>());

        Assert.Contains("id=\"edit-book\"", html);
        Assert.Contains("role=\"dialog\"", html);
        Assert.Contains(" hidden", html);
        Assert.Contains("<h2>Edit Book</h2>", html);
    }

    [Fact]
    public void Modal_Closable_RendersCloseButtonWithDataModalClose()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Modal Id=\"detail-modal\" Title=\"Book Details\" Closable=\"true\">Details</cs:Modal>",
            new Dictionary<string, object?>());

        Assert.Contains("<header class=\"cs-modal-header\">", html);
        Assert.Contains("<h2>Book Details</h2>", html);
        Assert.Contains("class=\"cs-modal-close\" data-modal-close aria-label=\"Close\"", html);
    }

    [Fact]
    public void Pager_RendersLinksAndCurrentPage()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Pager Page=\"2\" TotalPages=\"3\" Url=\"/admin?page={page}\" />",
            new Dictionary<string, object?>());

        Assert.Contains("aria-label=\"Pagination\"", html);
        Assert.Contains("href=\"/admin?page=1\"", html);
        Assert.Contains("href=\"/admin?page=3\"", html);
        Assert.Contains("class=\"cs-pager-link active\" href=\"/admin?page=2\" aria-current=\"page\"", html);
    }

    [Fact]
    public void Chart_RendersCanvasWithDataBindings()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Chart Type=\"bar\" Width=\"600\" Height=\"400\" Data=\"Values\" Labels=\"Labels\" Title=\"Sales\" />",
            new Dictionary<string, object?>());

        Assert.Contains("class=\"cs-card cs-chart\"", html);
        Assert.Contains("<h3>Sales</h3>", html);
        Assert.Contains("data-cs-chart=\"bar\"", html);
        Assert.Contains("data-cs-chart-width=\"600\"", html);
        Assert.Contains("data-cs-chart-height=\"400\"", html);
        Assert.Contains("data-cs-chart-data=\"Values\"", html);
        Assert.Contains("data-cs-chart-labels=\"Labels\"", html);
    }

    [Fact]
    public void Tree_RendersHierarchicalNodes()
    {
        var items = new[]
        {
            new { Label = "Root", Value = "1", Children = new[] { new { Label = "Child", Value = "1-1" } } }
        };

        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Tree Items=\"{Binding Nodes}\" ChildrenField=\"Children\" LabelField=\"Label\" ValueField=\"Value\" />",
            new Dictionary<string, object?> { ["Nodes"] = items });

        Assert.Contains("class=\"cs-card cs-tree\"", html);
        Assert.Contains("cs-tree-root", html);
        Assert.Contains("data-cs-tree-children=\"Children\"", html);
        Assert.Contains("data-cs-tree-label=\"Label\"", html);
        Assert.Contains("data-cs-tree-value=\"Value\"", html);
        Assert.Contains(">Root<", html);
        Assert.Contains("cs-tree-node-has-children", html);
    }

    [Fact]
    public void Wizard_RendersStepsAndActivePanel()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Wizard ActiveStep=\"step-2\"><cs:Step Key=\"step-1\" Title=\"First\">One</cs:Step><cs:Step Key=\"step-2\" Title=\"Second\">Two</cs:Step></cs:Wizard>",
            new Dictionary<string, object?>());

        Assert.Contains("class=\"cs-wizard\"", html);
        Assert.Contains("cs-wizard-step active", html);
        Assert.Contains(">Second<", html);
        Assert.Contains("cs-wizard-panel active", html);
        Assert.Contains(">Two<", html);
        Assert.Contains("role=\"tablist\"", html);
        Assert.Contains("id=\"cs-wizard-tab-2\" class=\"cs-wizard-step active\"", html);
        Assert.Contains("data-cs-wizard-step=\"step-2\" role=\"tab\" aria-controls=\"cs-wizard-panel-2\"", html);
        Assert.Contains("aria-selected=\"true\" tabindex=\"0\"", html);
        Assert.Contains("id=\"cs-wizard-panel-1\" class=\"cs-wizard-panel\" data-cs-wizard-panel=\"step-1\" role=\"tabpanel\" aria-labelledby=\"cs-wizard-tab-1\" hidden", html);
    }

    [Fact]
    public void Show_PreservesOriginalContentWhenVisibleBindingIsEmpty()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show>content</cs:Show>",
            new Dictionary<string, object?>());

        Assert.Contains(">content<", html);
    }

    [Fact]
    public void Show_DropsUnsafeEventAttributesAndSrcdoc()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Show Visible=\"{Binding Ready}\" onclick=\"alert(1)\" srcdoc=\"<p>x</p>\">Body</cs:Show>",
            new Dictionary<string, object?>());

        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("srcdoc", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Region_KeepsCustomAttributesAndSafeTag()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Region Name=\"orders\" Tag=\"section\" class=\"panel\" data-id=\"42\">Body</cs:Region>",
            new Dictionary<string, object?>());

        Assert.Contains("<section data-cs-region=\"orders\" class=\"panel\" data-id=\"42\">Body</section>", html);
    }

    [Fact]
    public void Scripts_DropsUnsafeExtraScriptUrlsAndPreservesOrder()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Scripts Extra=\"javascript:alert(1),/js/safe.js\" />",
            new Dictionary<string, object?>());

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/safe.js", html);
        Assert.True(html.IndexOf("/js/site.js", StringComparison.Ordinal) < html.IndexOf("/js/safe.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Chart_WithDefaultValues_RendersDefaults()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Chart />",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-chart=\"bar\"", html);
        Assert.Contains("data-cs-chart-width=\"400\"", html);
        Assert.Contains("data-cs-chart-height=\"300\"", html);
        Assert.DoesNotContain("<h3>", html);
    }

    [Fact]
    public void Chart_WithoutDataAndLabels_OmitsDataAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Chart Type=\"line\" Width=\"800\" Height=\"200\" Title=\"Trend\" />",
            new Dictionary<string, object?>());

        Assert.DoesNotContain("data-cs-chart-data", html);
        Assert.DoesNotContain("data-cs-chart-labels", html);
        Assert.Contains("data-cs-chart=\"line\"", html);
    }

    [Fact]
    public void Tree_EmptyItems_ReturnsEmpty()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Tree Items=\"{Binding Nodes}\" />",
            new Dictionary<string, object?> { ["Nodes"] = Array.Empty<object>() });

        Assert.Contains("cs-tree-root", html);
        Assert.DoesNotContain("cs-tree-node", html);
    }

    [Fact]
    public void Tree_EmptyTextRendersWhenItemsAreEmpty()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Tree Items=\"{Binding Nodes}\" EmptyText=\"{Binding Hint}\" />",
            new Dictionary<string, object?>
            {
                ["Nodes"] = Array.Empty<object>(),
                ["Hint"] = "No categories"
            });

        Assert.Contains("<li class=\"cs-empty\">No categories</li>", html);
    }

    [Fact]
    public void Tree_SingleNode_NoChildren()
    {
        var items = new[] { new { Label = "Only", Value = "1" } };
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Tree Items=\"{Binding Nodes}\" LabelField=\"Label\" ValueField=\"Value\" />",
            new Dictionary<string, object?> { ["Nodes"] = items });

        Assert.Contains("data-ui=\"tree\"", html);
        Assert.Contains("cs-tree-node", html);
        Assert.DoesNotContain("cs-tree-node-has-children", html);
        Assert.DoesNotContain("data-cs-tree-toggle", html);
    }

    [Fact]
    public void Tree_CollapsedWithChildren_RendersToggleAndHiddenChildren()
    {
        var items = new[] {
            new { Label = "Root", Value = "root", Children = new[] { new { Label = "Child", Value = "child" } } }
        };
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Tree Items=\"{Binding Nodes}\" ChildrenField=\"Children\" LabelField=\"Label\" ValueField=\"Value\" Collapsed=\"true\" data-ui=\"custom\" />",
            new Dictionary<string, object?> { ["Nodes"] = items });

        Assert.Contains("data-ui=\"tree custom\"", html);
        Assert.Contains("class=\"cs-tree-node cs-tree-node-has-children collapsed\"", html);
        Assert.Contains("data-cs-tree-toggle aria-expanded=\"false\"", html);
        Assert.Contains("<ul class=\"cs-tree-children\" style=\"display:none\">", html);
    }

    [Fact]
    public void Tree_DeepNesting_RendersRecursively()
    {
        var items = new[] {
            new { Label = "L1", Value = "1", Children = new[] {
                new { Label = "L2", Value = "1-1", Children = new[] {
                    new { Label = "L3", Value = "1-1-1" }
                }}
            }}
        };
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Tree Items=\"{Binding Nodes}\" ChildrenField=\"Children\" LabelField=\"Label\" ValueField=\"Value\" />",
            new Dictionary<string, object?> { ["Nodes"] = items });

        Assert.Contains(">L1<", html);
        Assert.Contains(">L2<", html);
        Assert.Contains(">L3<", html);
    }

    [Fact]
    public void Wizard_SingleStep_RendersOneStep()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Wizard ActiveStep=\"step-1\"><cs:Step Key=\"step-1\" Title=\"Only Step\">Content</cs:Step></cs:Wizard>",
            new Dictionary<string, object?>());

        Assert.Contains("data-ui=\"wizard\"", html);
        Assert.Contains("<button type=\"button\" id=\"cs-wizard-tab-1\" class=\"cs-wizard-step active\"", html);
        Assert.Contains("aria-selected=\"true\"", html);
        Assert.Contains("cs-wizard-panel", html);
        Assert.DoesNotContain("cs-wizard-step cs-wizard-step", html);
    }

    [Fact]
    public void Wizard_MultipleSteps_RendersClickableInactiveSteps()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Wizard ActiveStep=\"details\"><cs:Step Key=\"intro\" Title=\"Intro\">One</cs:Step><cs:Step Key=\"details\" Title=\"Details\">Two</cs:Step></cs:Wizard>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-wizard-step=\"intro\" role=\"tab\" aria-controls=\"cs-wizard-panel-1\"", html);
        Assert.Contains("data-cs-wizard-step=\"details\" role=\"tab\" aria-controls=\"cs-wizard-panel-2\" aria-selected=\"true\"", html);
        Assert.Contains("data-cs-wizard-panel=\"intro\" role=\"tabpanel\" aria-labelledby=\"cs-wizard-tab-1\" hidden style=\"display:none\"", html);
        Assert.Contains("data-cs-wizard-panel=\"details\"", html);
    }

    [Fact]
    public void Wizard_NoSteps_RendersEmptyDiv()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Wizard ActiveStep=\"0\"></cs:Wizard>",
            new Dictionary<string, object?>());

        Assert.Contains("class=\"cs-wizard\"", html);
        Assert.DoesNotContain("cs-wizard-step ", html);
    }

    [Fact]
    public void Repeater_EmptyArray_ReturnsEmptyString()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Items}\"><li>{Binding Name}</li></cs:Repeater>",
            new Dictionary<string, object?> { ["Items"] = Array.Empty<object>() });

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Repeater_NullBinding_ReturnsEmpty()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Missing}\"><li>{Binding Name}</li></cs:Repeater>",
            new Dictionary<string, object?>());

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Repeater_WithEnumeration_RendersItems()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding People}\"><li>{Binding Name}:{Binding Role}</li></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["People"] = new[] { new { Name = "Alice", Role = "Admin" }, new { Name = "Bob", Role = "User" } }
            });

        Assert.Contains("<li>Alice:Admin</li>", html);
        Assert.Contains("<li>Bob:User</li>", html);
    }

    [Fact]
    public void Repeater_EmptyBlock_RendersEmptyStateWithoutConditional()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Items}\"><li>{Binding Name}</li><cs:Empty><p class=\"cs-empty\">No records</p></cs:Empty></cs:Repeater>",
            new Dictionary<string, object?> { ["Items"] = Array.Empty<object>() });

        Assert.Contains("<p class=\"cs-empty\">No records</p>", html);
        Assert.DoesNotContain("<li>", html);
    }

    [Fact]
    public void Repeater_EmptyText_UsesParentStateBinding()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Items}\" EmptyText=\"{Binding EmptyHint}\"><li>{Binding Name}</li></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["Items"] = Array.Empty<object>(),
                ["EmptyHint"] = "No data available"
            });

        Assert.Contains("<p class=\"cs-empty\">No data available</p>", html);
    }

    [Fact]
    public void Repeater_WithItems_IgnoresEmptyBlock()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Items}\"><li>{Binding Name}</li><cs:Empty>No records</cs:Empty></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["Items"] = new[] { new { Name = "Alice" } }
            });

        Assert.Contains("<li>Alice</li>", html);
        Assert.DoesNotContain("No records", html);
    }

    [Fact]
    public void Repeater_NestedEmptyBlock_DoesNotLeakToOuterRepeater()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Groups}\"><div><cs:Repeater Items=\"{Binding Items}\"><span>{Binding Name}</span><cs:Empty>Nested empty</cs:Empty></cs:Repeater></div><cs:Empty>Outer empty</cs:Empty></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["Groups"] = Array.Empty<object>()
            });

        Assert.Contains("Outer empty", html);
        Assert.DoesNotContain("Nested empty", html);
    }

    [Fact]
    public void Repeater_NestedRepeater_RendersItsOwnEmptyState()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding Groups}\"><div>{Binding Title}<cs:Repeater Items=\"{Binding Items}\"><span>{Binding Name}</span><cs:Empty>Nested empty</cs:Empty></cs:Repeater></div><cs:Empty>Outer empty</cs:Empty></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["Groups"] = new[] { new { Title = "Group A", Items = Array.Empty<object>() } }
            });

        Assert.Contains("Group A", html);
        Assert.Contains("Nested empty", html);
        Assert.DoesNotContain("Outer empty", html);
    }

    [Fact]
    public void Repeater_IndexAndCount_AvailableInItemTemplate()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Repeater Items=\"{Binding People}\"><li data-i=\"{Binding index}\" data-n=\"{Binding count}\">{Binding Name}</li></cs:Repeater>",
            new Dictionary<string, object?>
            {
                ["People"] = new[] { new { Name = "Alice" }, new { Name = "Bob" } }
            });

        Assert.Contains("<li data-i=\"0\" data-n=\"2\">Alice</li>", html);
        Assert.Contains("<li data-i=\"1\" data-n=\"2\">Bob</li>", html);
    }

    [Fact]
    public void Conditional_WhenBindingIsFalse_HidesContent()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Conditional Visible=\"{Binding Show}\"><p>Hidden</p></cs:Conditional>",
            new Dictionary<string, object?> { ["Show"] = false });

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Conditional_WhenBindingIsTrue_ShowsContent()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Conditional Visible=\"{Binding Show}\"><p>Visible</p></cs:Conditional>",
            new Dictionary<string, object?> { ["Show"] = true });

        Assert.Contains("<p>Visible</p>", html);
    }

    [Fact]
    public void Conditional_WhenBindingIsFalsy_Zero_ReturnsEmpty()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Conditional Visible=\"{Binding Show}\"><p>Zero</p></cs:Conditional>",
            new Dictionary<string, object?> { ["Show"] = 0 });

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Conditional_WhenBindingIsTruthy_NonZero_RendersContent()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Conditional Visible=\"{Binding Show}\"><p>One</p></cs:Conditional>",
            new Dictionary<string, object?> { ["Show"] = 1 });

        Assert.Contains("<p>One</p>", html);
    }

    [Fact]
    public void Pager_SinglePage_ShowsOnlyOne()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Pager Page=\"1\" TotalPages=\"1\" Url=\"/list?page={page}\" />",
            new Dictionary<string, object?>());

        Assert.Contains("aria-current=\"page\"", html);
        Assert.DoesNotContain("href=\"/list?page=0\"", html);
    }

    [Fact]
    public void Pager_UsesInferredPageUrl()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Pager Page=\"3\" TotalPages=\"5\" />",
            new Dictionary<string, object?>());

        Assert.Contains("class=\"cs-pager\"", html);
        Assert.Contains("cs-pager-link", html);
        Assert.Contains("active", html);
    }

    [Fact]
    public void Pager_LargeTotalPages_RendersWindowedPaginationWithEllipsis()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Pager Page=\"50\" TotalPages=\"100\" Url=\"/books?page={page}\" />",
            new Dictionary<string, object?>());

        Assert.Contains("class=\"cs-pager\"", html);
        Assert.Contains("href=\"/books?page=1\"", html);
        Assert.Contains("class=\"cs-pager-ellipsis\">&hellip;</span>", html);
        Assert.Contains("href=\"/books?page=49\"", html);
        Assert.Contains("href=\"/books?page=50\" aria-current=\"page\"", html);
        Assert.Contains("href=\"/books?page=51\"", html);
        Assert.Contains("href=\"/books?page=100\"", html);
        Assert.DoesNotContain("href=\"/books?page=20\"", html);
    }

    [Fact]
    public void Script_RendersInlineWhenNoSrc()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Script>console.log('test');</cs:Script>",
            new Dictionary<string, object?>());

        Assert.Contains("console.log('test')", html);
        Assert.Contains("<script", html);
    }

    [Fact]
    public void Grid_CustomColumnsClass_IncludesGridClass()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Grid Columns=\"3\">Three columns</cs:Grid>",
            new Dictionary<string, object?>());

        Assert.Contains("cs-grid-3", html);
        Assert.Contains("Three columns</div>", html);
    }

    [Fact]
    public void Stack_VerticalByDefault_Renders()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Stack>Stacked content</cs:Stack>",
            new Dictionary<string, object?>());

        Assert.Contains("cs-stack", html);
        Assert.Contains("Stacked content</div>", html);
    }

    private sealed record TableRow(string Name, string Status, decimal FineBalance);
}

public class ValueConverterTests
{
    [Fact]
    public void ConvertValue_NullToNonNullableValueType_ReturnsDefault()
    {
        var value = ValueConverter.ConvertValue(null, typeof(int));

        Assert.Equal(0, value);
    }

    [Fact]
    public void ConvertValue_EmptyStringToNullableValueType_ReturnsNull()
    {
        var value = ValueConverter.ConvertValue(string.Empty, typeof(int?));

        Assert.Null(value);
    }

    [Fact]
    public void ConvertValue_StringToEnum_IgnoresCase()
    {
        var value = ValueConverter.ConvertValue("green", typeof(TestTone));

        Assert.Equal(TestTone.Green, value);
    }

    private enum TestTone
    {
        Green,
        Red
    }
}

public class ViewModelExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_PostJson_BindsTwoWayPropertyAndRunsCommand()
    {
        var ctx = CreateContext("POST", "{\"Count\":3,\"__command\":\"Increment\"}");
        var executor = CreateExecutor(ctx.RequestServices);

        await executor.ExecuteAsync(ctx, typeof(ExecutorTestViewModel));

        var response = await ReadJsonResponse(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.Equal(4, response.RootElement.GetProperty("state").GetProperty("Count").GetInt32());
        Assert.Contains("Increment", response.RootElement.GetProperty("commands").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task ExecuteAsync_PostJson_ExecutesAndAwaitsValueTaskCommand()
    {
        var ctx = CreateContext("POST", "{\"Value\":5,\"__command\":\"IncrementValueTask\"}");
        var executor = CreateExecutor(ctx.RequestServices);

        await executor.ExecuteAsync(ctx, typeof(ExecutorAsyncCommandViewModel));

        var response = await ReadJsonResponse(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.Equal(15, response.RootElement.GetProperty("state").GetProperty("Value").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_PostJson_ExecutesAndAwaitsTaskCommand()
    {
        var ctx = CreateContext("POST", "{\"Value\":5,\"__command\":\"IncrementTask\"}");
        var executor = CreateExecutor(ctx.RequestServices);

        await executor.ExecuteAsync(ctx, typeof(ExecutorAsyncCommandViewModel));

        var response = await ReadJsonResponse(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.Equal(10, response.RootElement.GetProperty("state").GetProperty("Value").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_PostJson_UnknownCommand_ReturnsJsonError()
    {
        var ctx = CreateContext("POST", "{\"__command\":\"Missing\"}");
        var executor = CreateExecutor(ctx.RequestServices);

        await executor.ExecuteAsync(ctx, typeof(ExecutorTestViewModel));

        var response = await ReadJsonResponse(ctx);

        Assert.Equal(500, ctx.Response.StatusCode);
        Assert.Contains("Command 'Missing' was not found", response.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_Get_RendersHtmlFallbackWhenPageFileMissing()
    {
        var ctx = CreateContext("GET", string.Empty);
        var executor = CreateExecutor(ctx.RequestServices);

        await executor.ExecuteAsync(ctx, typeof(ExecutorTestViewModel));

        ctx.Response.Body.Position = 0;
        var html = await new StreamReader(ctx.Response.Body).ReadToEndAsync();

        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", ctx.Response.ContentType);
        Assert.Contains("Executor Test", html);
        Assert.Contains("Count", html);
    }

    [Fact]
    public async Task ExecuteAsync_Get_EncodesFallbackTitle()
    {
        var ctx = CreateContext("GET", string.Empty);
        var executor = CreateExecutor(ctx.RequestServices);

        await executor.ExecuteAsync(ctx, typeof(UnsafeTitleViewModel));

        ctx.Response.Body.Position = 0;
        var html = await new StreamReader(ctx.Response.Body).ReadToEndAsync();

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }

    [Fact]
    public async Task ExecuteAsync_Get_BindsQueryAndRouteValuesBeforeRendering()
    {
        EnsureTestPage("ExecutorQueryRoute.aspx", """
<%@ Page Title="Query Route Test" %>
<p>Query: {Binding Query}</p>
<p>RouteId: {Binding RouteId}</p>
""");
        var ctx = CreateContext("GET", string.Empty);
        ctx.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["q"] = "books"
        });
        ctx.GetRouteData().Values["id"] = "42";
        var executor = CreateExecutor(ctx.RequestServices);

        await executor.ExecuteAsync(ctx, typeof(ExecutorQueryRouteViewModel));

        ctx.Response.Body.Position = 0;
        var html = await new StreamReader(ctx.Response.Body).ReadToEndAsync();

        Assert.Contains("Query: books", html);
        Assert.Contains("RouteId: 42", html);
    }

    [Fact]
    public void Button_SelfClosingWithTextAttribute_RendersCorrectly()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Button Command=\"Save\" Text=\"Save Book\" />",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-command=\"Save\"", html);
        Assert.Contains(">Save Book</button>", html);
    }

    [Fact]
    public void Link_SelfClosingWithTextAttribute_RendersCorrectly()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Link NavigateTo=\"/books\" Text=\"All Books\" />",
            new Dictionary<string, object?>());

        Assert.Contains("href=\"/books\"", html);
        Assert.Contains(">All Books</a>", html);
    }

    [Fact]
    public void Form_WithDebounceAndLoading_RendersDataAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Form Debounce=\"500\" Loading=\"true\"><input type=\"text\" /></cs:Form>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-debounce=\"500\"", html);
        Assert.Contains("data-cs-loading=\"true\"", html);
        Assert.Contains("data-cs-vm", html);
    }

    [Fact]
    public void Button_WithDebounceAndLoading_RendersDataAttributes()
    {
        var html = PageRenderer.RenderTemplateForTests(
            "<cs:Button Command=\"Save\" Loading=\"true\" Debounce=\"300\">Save</cs:Button>",
            new Dictionary<string, object?>());

        Assert.Contains("data-cs-command=\"Save\"", html);
        Assert.Contains("data-cs-loading=\"true\"", html);
        Assert.Contains("data-cs-debounce=\"300\"", html);
    }

    [Fact]
    public void CustomTagRenderer_BlockAndSelfClosing_RendersOutput()
    {
        try
        {
            PageRenderer.RegisterTagRenderer(new TestBadgeTagRenderer());
            PageRenderer.RegisterTagRenderer(new TestAlertTagRenderer());

            var selfClosingHtml = PageRenderer.RenderTemplateForTests(
                "<cs:Badge Text=\"New\" Type=\"success\" />",
                new Dictionary<string, object?>());

            Assert.Contains("<span class=\"badge badge-success\">New</span>", selfClosingHtml);

            var blockHtml = PageRenderer.RenderTemplateForTests(
                "<cs:Alert Type=\"warning\"><strong>Warning:</strong> Low stock</cs:Alert>",
                new Dictionary<string, object?>());

            Assert.Contains("<div class=\"alert alert-warning\"><strong>Warning:</strong> Low stock</div>", blockHtml);
        }
        finally
        {
            PageRenderer.UnregisterTagRenderer("Badge");
            PageRenderer.UnregisterTagRenderer("Alert");
        }
    }

    private sealed class TestBadgeTagRenderer : ITagRenderer
    {
        public string TagName => "Badge";
        public bool IsSelfClosing => true;

        public string Render(string rawAttributes, string innerContent, IReadOnlyDictionary<string, object?> state)
        {
            var text = PageRenderer.GetAttribute(rawAttributes, "Text") ?? string.Empty;
            var type = PageRenderer.GetAttribute(rawAttributes, "Type") ?? "default";
            return $"<span class=\"badge badge-{type}\">{text}</span>";
        }
    }

    private sealed class TestAlertTagRenderer : ITagRenderer
    {
        public string TagName => "Alert";
        public bool IsSelfClosing => false;

        public string Render(string rawAttributes, string innerContent, IReadOnlyDictionary<string, object?> state)
        {
            var type = PageRenderer.GetAttribute(rawAttributes, "Type") ?? "info";
            return $"<div class=\"alert alert-{type}\">{innerContent}</div>";
        }
    }

    private static void EnsureTestPage(string fileName, string content)
    {
        var pagesDir = Path.Combine(AppContext.BaseDirectory, "Pages");
        Directory.CreateDirectory(pagesDir);
        File.WriteAllText(Path.Combine(pagesDir, fileName), content, Encoding.UTF8);
    }

    private static DefaultHttpContext CreateContext(string method, string body)
    {
        var services = new ServiceCollection();
        services.AddScoped<ExecutorTestViewModel>();
        services.AddScoped<ExecutorQueryRouteViewModel>();
        services.AddScoped<UnsafeTitleViewModel>();
        services.AddScoped<ExecutorAsyncCommandViewModel>();
        var provider = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = provider };
        ctx.Request.Method = method;
        ctx.Response.Body = new MemoryStream();

        if (!string.IsNullOrEmpty(body))
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
            ctx.Request.ContentType = "application/json";
        }

        return ctx;
    }

    private static ViewModelExecutor CreateExecutor(IServiceProvider services)
    {
        var renderer = new PageRenderer(new PageParser(), NullLogger<PageRenderer>.Instance);
        return new ViewModelExecutor(NullLogger<ViewModelExecutor>.Instance, renderer);
    }

    private static async Task<JsonDocument> ReadJsonResponse(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(ctx.Response.Body);
    }

    [CodeSpirit.Core.Page.PageDirective(Title = "Executor Test")]
    private sealed class ExecutorTestViewModel : CodeSpirit.Core.ViewModel
    {
        [CodeSpirit.Core.Mvvm.Bind(CodeSpirit.Core.Mvvm.BindDirection.TwoWay)]
        public int Count { get; set; }

        [CodeSpirit.Core.Mvvm.Command]
        public void Increment()
        {
            Count++;
        }
    }

    [CodeSpirit.Core.Page.PageDirective(Title = "Query Route Test")]
    private sealed class ExecutorQueryRouteViewModel : CodeSpirit.Core.ViewModel
    {
        [CodeSpirit.Core.Mvvm.FromQuery("q")]
        [CodeSpirit.Core.Mvvm.Bind]
        public string Query { get; set; } = string.Empty;

        [CodeSpirit.Core.Mvvm.FromRoute("id")]
        [CodeSpirit.Core.Mvvm.Bind]
        public int RouteId { get; set; }
    }

    [CodeSpirit.Core.Page.PageDirective(Title = "<script>alert(1)</script>")]
    private sealed class UnsafeTitleViewModel : CodeSpirit.Core.ViewModel
    {
        [CodeSpirit.Core.Mvvm.Bind]
        public string Message { get; set; } = "safe";
    }

    [CodeSpirit.Core.Page.PageDirective(Title = "Async Command Test")]
    private sealed class ExecutorAsyncCommandViewModel : CodeSpirit.Core.ViewModel
    {
        [CodeSpirit.Core.Mvvm.Bind(CodeSpirit.Core.Mvvm.BindDirection.TwoWay)]
        public int Value { get; set; }

        [CodeSpirit.Core.Mvvm.Command]
        public async ValueTask IncrementValueTask()
        {
            await Task.Yield();
            Value += 10;
        }

        [CodeSpirit.Core.Mvvm.Command]
        public async Task IncrementTask()
        {
            await Task.Yield();
            Value += 5;
        }
    }
}
