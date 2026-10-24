using ArchiveMaster.Cli;
using ArchiveMaster.Configs;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.Cli;

public class CliArgumentsTests
{
    [Fact]
    public void Parse_SetOptionIsRepeatableAndSupportsEqualsForm()
    {
        CliArguments args = CliArguments.Parse([
            "plan", "fileTools.timeClassify",
            "--set", "Dir=D:\\input",
            "--set=MinTimeInterval=01:00:00",
        ]);

        Assert.Equal("plan", args.Command);
        Assert.Equal(["fileTools.timeClassify"], args.Positionals);
        Assert.Equal(["Dir=D:\\input", "MinTimeInterval=01:00:00"], args.GetAll("set"));
    }

    [Fact]
    public void Parse_SetWithoutValue_Throws()
    {
        CliException ex = Assert.Throws<CliException>(() =>
            CliArguments.Parse(["plan", "fileTools.timeClassify", "--set"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("缺少值", ex.Message);
    }

    [Fact]
    public void PathGuard_DoesNotTreatSecurePasswordAsPath()
    {
        using var workspace = new TempWorkspace();
        var guard = PathGuard.Create([workspace.Root]);
        var config = new EncryptorConfig
        {
            RawDir = workspace.Dir("raw"),
            EncryptedDir = workspace.Dir("encrypted"),
            // 密码里出现分隔符时不能被当成相对路径拒绝
            Password = "abc/123",
        };

        guard.ValidateConfig(config);
    }

    [Fact]
    public void PathGuard_StillRejectsPathOutsideRoot()
    {
        using var workspace = new TempWorkspace();
        var guard = PathGuard.Create([workspace.Root]);
        var config = new EncryptorConfig
        {
            RawDir = Path.GetDirectoryName(workspace.Root),
            EncryptedDir = workspace.Dir("encrypted"),
        };

        Assert.Throws<CliException>(() => guard.ValidateConfig(config));
    }
}
