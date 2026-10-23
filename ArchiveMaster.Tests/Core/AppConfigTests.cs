using ArchiveMaster.Configs;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.Core;

public class AppConfigTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();

    public void Dispose()
    {
        globalState.Dispose();
    }

    [Fact]
    public void GetOrCreateConfig_CreatesDefaultAndNamedPresets()
    {
        using var ws = new TempWorkspace();
        var appConfig = TestAppConfig.Create(ws);
        appConfig.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));

        var defaultConfig = appConfig.GetOrCreateConfig<SampleConfig>("SampleConfig");
        var second = appConfig.GetOrCreateConfig<SampleConfig>("SampleConfig", "第二个");

        Assert.NotSame(defaultConfig, second);
        Assert.Equal("", defaultConfig.Value);
        Assert.Equal(["默认", "第二个"], appConfig.GetPresets("SampleConfig"));
    }

    [Fact]
    public void SaveThenInitialize_RestoresConfigValues()
    {
        using var ws = new TempWorkspace();
        var first = TestAppConfig.Create(ws);
        first.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));
        var config = first.GetOrCreateConfig<SampleConfig>("SampleConfig");
        config.Value = "持久化内容";
        first.Save();

        var second = TestAppConfig.Create(ws);
        second.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));
        second.Initialize();

        Assert.Null(second.LoadError);
        Assert.Equal("持久化内容", second.GetOrCreateConfig<SampleConfig>("SampleConfig").Value);
    }

    [Fact]
    public void Initialize_WithoutFile_DoesNotSetLoadError()
    {
        using var ws = new TempWorkspace();
        var appConfig = TestAppConfig.Create(ws);
        appConfig.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));

        appConfig.Initialize();

        Assert.Null(appConfig.LoadError);
        Assert.Equal("", appConfig.GetOrCreateConfig<SampleConfig>("SampleConfig").Value);
    }

    [Fact]
    public void Initialize_InvalidJson_SetsLoadError()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteText("configs.json", "{ 这不是 JSON");
        var appConfig = new AppConfig(path);
        appConfig.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));

        appConfig.Initialize();

        Assert.NotNull(appConfig.LoadError);
    }

    [Fact]
    public void Initialize_WithApplyPendingLoadFalse_DoesNotReplaceExistingConfig()
    {
        using var ws = new TempWorkspace();
        var target = TestAppConfig.Create(ws);
        target.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));
        target.GetOrCreateConfig<SampleConfig>("SampleConfig").Value = "旧值";
        target.Save();

        var next = new AppConfig(target.ConfigPath);
        next.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));
        next.GetOrCreateConfig<SampleConfig>("SampleConfig").Value = "新值";
        next.Save(target.ConfigPath + AppConfig.NextStartLoadConfigExtension);

        target.Initialize(false);
        Assert.Equal("旧值", target.GetOrCreateConfig<SampleConfig>("SampleConfig").Value);

        // AppConfig 预期只在启动时 Initialize 一次，因此用新实例验证 .load 的替换行为。
        var afterPendingLoad = new AppConfig(target.ConfigPath);
        afterPendingLoad.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));
        afterPendingLoad.Initialize(true);

        Assert.Equal("新值", afterPendingLoad.GetOrCreateConfig<SampleConfig>("SampleConfig").Value);
        Assert.True(File.Exists(afterPendingLoad.ConfigPath + AppConfig.ConfigBackupExtension));
    }

    [Fact]
    public void HasPreset_IsGroupAware()
    {
        using var ws = new TempWorkspace();
        var appConfig = TestAppConfig.Create(ws);
        appConfig.RegisterConfig(new ConfigMetadata(typeof(SampleConfig), "共享组"));
        appConfig.RegisterConfig(new ConfigMetadata(typeof(OtherConfig), "共享组"));
        appConfig.GetOrCreateConfig<SampleConfig>("SampleConfig", "组内预设");

        Assert.True(appConfig.HasPreset(typeof(OtherConfig), "组内预设"));
        Assert.False(appConfig.HasPreset(typeof(OtherConfig), "不存在的预设"));
    }

    [Fact]
    public void RenameAndRemovePreset_WorkOnAllConfigsInGroup()
    {
        using var ws = new TempWorkspace();
        var appConfig = TestAppConfig.Create(ws);
        appConfig.RegisterConfig(new ConfigMetadata(typeof(SampleConfig), "共享组"));
        appConfig.RegisterConfig(new ConfigMetadata(typeof(OtherConfig), "共享组"));
        var sample = appConfig.GetOrCreateConfig<SampleConfig>("SampleConfig", "旧预设");
        sample.Value = "用户数据";
        var other = appConfig.GetOrCreateConfig<OtherConfig>("OtherConfig", "旧预设");

        appConfig.RenamePreset("共享组", "旧预设", "新预设");

        Assert.True(appConfig.HasPreset(typeof(SampleConfig), "新预设"));
        Assert.True(appConfig.HasPreset(typeof(OtherConfig), "新预设"));
        Assert.False(appConfig.HasPreset(typeof(SampleConfig), "旧预设"));

        // 模拟 UI 改名后重新取配置：应取回原对象，不能新建重复预设
        var renamedSample = appConfig.GetOrCreateConfig<SampleConfig>("SampleConfig", "新预设");
        var renamedOther = appConfig.GetOrCreateConfig<OtherConfig>("OtherConfig", "新预设");
        Assert.Same(sample, renamedSample);
        Assert.Same(other, renamedOther);
        Assert.Equal("用户数据", renamedSample.Value);
        Assert.Equal(["新预设"], appConfig.GetPresets("共享组"));

        Assert.True(appConfig.RemovePreset<SampleConfig>("SampleConfig", "新预设"));
        Assert.False(appConfig.HasPreset(typeof(SampleConfig), "新预设"));
        Assert.False(appConfig.RemovePreset<SampleConfig>("SampleConfig", "新预设"));
    }

    [Fact]
    public void GetOrCreateConfig_ByType_ResolvesRegisteredConfig()
    {
        using var ws = new TempWorkspace();
        var appConfig = TestAppConfig.Create(ws);
        appConfig.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));
        appConfig.GetOrCreateConfig<SampleConfig>("SampleConfig").Value = "按类型";

        var config = (SampleConfig)appConfig.GetOrCreateConfig(typeof(SampleConfig));

        Assert.Equal("按类型", config.Value);
    }

    [Fact]
    public void GetOrCreateConfig_UnregisteredKey_Throws()
    {
        using var ws = new TempWorkspace();
        var appConfig = TestAppConfig.Create(ws);
        appConfig.RegisterConfig(new ConfigMetadata(typeof(SampleConfig)));

        Assert.Throws<Exception>(() => appConfig.GetOrCreateConfig<OtherConfig>("OtherConfig"));
    }

    public sealed class SampleConfig : ConfigBase
    {
        public string Value { get; set; } = "";

        public override void Check()
        {
        }
    }

    public sealed class OtherConfig : ConfigBase
    {
        public override void Check()
        {
        }
    }
}
