using ArchiveMaster.Cli;

namespace ArchiveMaster.Tests.Core;

public class ToolCatalogTests
{
    [Fact]
    public void Build_SevenModules_ReturnsTwentyNineTools()
    {
        var tools = BuildCatalog();

        Assert.Equal(29, tools.Count);
        Assert.Equal(tools.Count, tools.Select(p => p.Id).Distinct().Count());
        Assert.Contains(tools, p => p.Id == "fileTools.timeClassify");
        Assert.Contains(tools, p => p.Id == "offlineSync.step1");
        Assert.Contains(tools, p => p.Id == "writeOnceArchive.packing");
        Assert.Contains(tools, p => p.Id == "photoTagging.photoTagSearcher");
    }

    [Fact]
    public void Build_MarksPrepareUnsafeAndNoItemResultTools()
    {
        var tools = BuildCatalog();

        Assert.True(tools.Single(p => p.Id == "writeOnceArchive.packing").PrepareUnsafe);
        Assert.False(tools.Single(p => p.Id == "fileTools.timeClassify").PrepareUnsafe);
        Assert.False(tools.Single(p => p.Id == "offlineSync.step1").HasItemResults);
        Assert.False(tools.Single(p => p.Id == "photoTagging.photoTagManager").HasItemResults);
        Assert.True(tools.Single(p => p.Id == "fileTools.timeClassify").HasItemResults);
    }

    [Fact]
    public void Build_ModuleIdIsLowerCamelCase()
    {
        var tool = BuildCatalog().Single(p => p.Id == "offlineSync.step2");

        Assert.Equal("offlineSync", tool.ModuleId);
        Assert.Equal("本地生成补丁", tool.Title);
    }

    private static IReadOnlyList<ToolDescriptor> BuildCatalog()
    {
        IModuleInfo[] modules =
        [
            new FileToolsModuleInfo(),
            new PhotoToolsModuleInfo(),
            new PhotoTaggingModuleInfo(),
            new VideoToolsModuleInfo(),
            new TextToolsModuleInfo(),
            new OfflineSyncModuleInfo(),
            new WriteOnceArchiveModuleInfo(),
        ];
        return ToolCatalog.Build(modules);
    }
}
