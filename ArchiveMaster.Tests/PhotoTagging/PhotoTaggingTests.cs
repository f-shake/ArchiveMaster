using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Text;

namespace ArchiveMaster.Tests.PhotoTagging;

public class PhotoTaggingTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public PhotoTaggingTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public void PhotoTags_AllTagsDistinctButCountCountsDuplicates()
    {
        var tags = new PhotoTags(
            ["猫", "猫"],
            ["室内"],
            [],
            ["暖色"],
            ["特写"],
            "OCR",
            "描述");

        Assert.Equal(4, tags.GetAllTags().Count);
        Assert.Equal(5, tags.Count);
    }

    [Fact]
    public void PhotoTags_ContainsIsCaseSensitiveAndMatchesIsCaseInsensitive()
    {
        var tags = new PhotoTags(["Cat"], ["Room"], [], [], [], "Text", "Description");

        Assert.False(tags.ContainsTag("cat", TagType.Object));
        Assert.True(tags.Matches("cat", TagType.Object));
        Assert.True(tags.ContainsTag("Text", TagType.Text));
        Assert.True(tags.Matches("desc", TagType.Description));
        Assert.False(tags.ContainsTag("", TagType.Object));
    }

    [Fact]
    public void PhotoTags_FlagCombinationMatchesOnlyRequestedKinds()
    {
        var tags = new PhotoTags(["cat"], ["room"], [], [], [], null, null);

        Assert.True(tags.ContainsTag("cat", TagType.Object | TagType.Scene));
        Assert.True(tags.ContainsTag("room", TagType.Object | TagType.Scene));
        Assert.False(tags.ContainsTag("cat", TagType.Scene));
        Assert.True(tags.Matches("cat", TagType.Scene | TagType.Object));
    }

    [Fact]
    public void ObservablePhotoTags_DistinctAndCount()
    {
        var source = new PhotoTags(["cat", "cat", ""], [], [], [], [], null, null);
        var observable = new ObservablePhotoTags(source);

        var result = observable.ToPhotoTags();
        Assert.Equal(["cat"], result.ObjectTags);
        Assert.True(observable.Count > 0);
    }

    [Fact]
    public async Task TagFileHelper_RoundTripsChineseTags()
    {
        string path = workspace.PathOf("tags.json");
        var collection = new TaggedPhotoCollection(
        [
            new TaggedPhoto("图片/猫 1.jpg", new PhotoTags(["猫"], ["室内"], [], ["暖色"], ["特写"], "文字", "描述")),
        ]);

        await TagFileHelper.WritePhotoTagCollectionAsync(path, collection, TestContext.Current.CancellationToken);
        var restored = await TagFileHelper.ReadPhotoTagCollectionAsync(path, TestContext.Current.CancellationToken);

        Assert.Single(restored.Photos);
        Assert.Equal("图片/猫 1.jpg", restored.Photos[0].RelativePath);
        Assert.Equal(["猫"], restored.Photos[0].Tags.ObjectTags);
    }

    [Fact]
    public async Task TagSearcher_CountsAndSearchesByKeyword()
    {
        var tags = new PhotoTags(["猫"], ["室内"], [], [], [], "文本", "描述");
        string root = workspace.Dir("photos");
        workspace.WriteText("photos/a.jpg", "x");
        workspace.WriteText("photos/b.jpg", "x");
        string tagFile = DataFactory.WriteTagJson(workspace.PathOf("tags.json"),
            new TaggedPhoto("a.jpg", tags),
            new TaggedPhoto("b.jpg", tags));

        var service = new PhotoTagSearcherService(appConfig)
        {
            Config = new PhotoTagSearcherConfig { RootDir = root, TagFile = tagFile },
        };
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Single(service.AllTags, p => p.Tag == "猫");
        Assert.Equal(2, service.AllTags.Single(p => p.Tag == "猫").Count);
        Assert.Equal(2, (await service.SearchAsync(TagType.Object, "猫", false)).Count);
        Assert.Empty(await service.SearchAsync(TagType.Object, "", false));
        Assert.Empty(await service.SearchAsync(TagType.Scene, "猫", false));
    }

    [Fact]
    public async Task TagManager_LoadsTagsAndSavesNonEmptyOnly()
    {
        string root = workspace.Dir("photos");
        workspace.WriteText("photos/a.jpg", "x");
        workspace.WriteText("photos/b.jpg", "x");
        string tagFile = DataFactory.WriteTagJson(workspace.PathOf("tags.json"),
            new TaggedPhoto("a.jpg", new PhotoTags(["猫"], [], [], [], [], null, null)));
        var service = new PhotoTagManagerService(appConfig)
        {
            Config = new PhotoTagManagerConfig { RootDir = root, TagFile = tagFile },
        };
        await service.ExecuteAsync(CancellationToken.None);
        await service.SaveTagsAsync(workspace.PathOf("saved.json"), TestContext.Current.CancellationToken);

        var saved = await TagFileHelper.ReadPhotoTagCollectionAsync(workspace.PathOf("saved.json"),
            TestContext.Current.CancellationToken);
        Assert.Single(saved.Photos);
        Assert.Equal("a.jpg", saved.Photos[0].RelativePath);
    }

    [Fact]
    public void TagType_NoneIsNotPartOfAnyTag()
    {
        var tags = new PhotoTags(["cat"], [], [], [], [], null, null);

        Assert.False(tags.ContainsTag("cat", TagType.None));
        Assert.False(tags.Matches("cat", TagType.None));
    }
}
