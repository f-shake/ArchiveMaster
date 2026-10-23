using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Tests.Core;

public class TreeDirInfoTests : IDisposable
{
    private readonly TempWorkspace workspace = new();

    public void Dispose()
    {
        workspace.Dispose();
    }

    [Fact]
    public void AddFile_CreatesIntermediateDirectoriesAndCounts()
    {
        var tree = TreeDirInfo.CreateEmptyTree();

        tree.AddFile(CreateFile("a/b/c.txt"));
        tree.AddFile(CreateFile("a/d.txt"));
        tree.AddFile(CreateFile("e.txt"));

        Assert.Equal(3, tree.SubFileCount);
        Assert.Equal(2, tree.SubFolderCount);
        Assert.Single(tree.SubDirs);
        Assert.Single(tree.SubFiles);
        Assert.Equal(["a"], tree.SubDirs.Select(p => p.Name));
        Assert.Equal(["e.txt"], tree.SubFiles.Select(p => p.Name));
    }

    [Fact]
    public void Flatten_ReturnsAllFilesAndReorderWorks()
    {
        var tree = TreeDirInfo.CreateEmptyTree();
        tree.AddFile(CreateFile("b/2.txt"));
        tree.AddFile(CreateFile("a/1.txt"));

        tree.Reorder();

        Assert.Equal(["a", "b"], tree.SubDirs.Select(p => p.Name));
        Assert.Equal(["1.txt", "2.txt"], tree.Flatten().Select(p => p.Name));
    }

    [Fact]
    public void Search_FindsDirectoriesAndFiles()
    {
        var tree = TreeDirInfo.CreateEmptyTree();
        tree.AddFile(CreateFile("match/photo.txt"));
        tree.AddFile(CreateFile("other/match-note.txt"));
        tree.AddFile(CreateFile("other/photo.jpg"));

        var results = tree.Search("match");

        Assert.Contains(results, p => p.Name == "match" && p.IsDir);
        Assert.Contains(results, p => p.Name == "match-note.txt");
        Assert.DoesNotContain(results, p => p.Name == "photo.jpg");
    }

    [Fact]
    public void ToJsonAndFromJson_RoundTripsAndRepairsParents()
    {
        var tree = TreeDirInfo.CreateEmptyTree();
        tree.AddFile(CreateFile("a/b.txt"));
        tree.AddFile(CreateFile("a/c/d.txt"));

        string json = tree.ToJson();
        var restored = TreeDirInfo.FromJson(json);

        Assert.Equal(["a"], restored.SubDirs.Select(p => p.Name));
        Assert.Equal(2, restored.SubFileCount);
        var a = restored.SubDirs[0];
        Assert.Same(restored, a.Parent);
        Assert.Same(a, a.SubDirs[0].Parent);
        Assert.Equal("b.txt", a.SubFiles[0].Name);
    }

    [Fact]
    public void CheckedState_PropagatesUpAndDown()
    {
        var tree = TreeDirInfo.CreateEmptyTree();
        var dir = tree.AddSubDir("dir");
        var first = dir.AddSubFile(CreateFile("dir/a.txt"));
        var second = dir.AddSubFile(CreateFile("dir/b.txt"));

        dir.IsTreeItemChecked = true;
        Assert.True(first.IsTreeItemChecked);
        Assert.True(second.IsTreeItemChecked);

        first.IsTreeItemChecked = false;
        Assert.Null(dir.IsTreeItemChecked);
        Assert.True(second.IsTreeItemChecked);

        second.IsTreeItemChecked = false;
        Assert.False(dir.IsTreeItemChecked);
    }

    [Fact]
    public void SimpleFileInfo_RelativePathAndHashAreStable()
    {
        var file = CreateFile("dir\\a.txt");
        var same = CreateFile("dir\\a.txt");
        var other = CreateFile("dir\\b.txt");

        Assert.Equal(Path.Combine("dir", "a.txt"), file.RelativePath);
        Assert.Equal(file.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(file.GetHashCode(), other.GetHashCode());
    }

    [Fact]
    public void SimpleFileInfo_RelativeConstructor_ShouldKeepRelativePath()
    {
        // 构造参数叫 relativePath/topDir，调用方期待 RelativePath 返回该相对路径。
        var file = new SimpleFileInfo(Path.Combine("dir", "a.txt"), workspace.Root);

        Assert.Equal(Path.Combine("dir", "a.txt"), file.RelativePath);
    }

    private SimpleFileInfo CreateFile(string relativePath)
    {
        var file = new SimpleFileInfo
        {
            Name = Path.GetFileName(relativePath),
            Path = workspace.PathOf(relativePath),
            TopDirectory = workspace.Root,
        };
        file.SetRelativePath(relativePath);
        return file;
    }
}
