using ArchiveMaster.Cli;

namespace ArchiveMaster.Tests.Core;

public class CliCommandPolicyTests
{
    [Theory]
    [InlineData("list")]
    [InlineData("describe")]
    [InlineData("docs")]
    [InlineData("version")]
    [InlineData("--version")]
    [InlineData("help")]
    [InlineData("--help")]
    public void EnsureSetAllowed_NonToolCommand_Throws(string command)
    {
        CliException ex = Assert.Throws<CliException>(() => CliCommandPolicy.EnsureSetAllowed(true, command));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("--set 只用于 plan 和 run", ex.Message);
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("run")]
    [InlineData("PLAN")]
    [InlineData("Run")]
    public void EnsureSetAllowed_ToolCommand_DoesNotThrow(string command)
    {
        CliCommandPolicy.EnsureSetAllowed(true, command);
    }

    [Fact]
    public void EnsureSetAllowed_WithoutSet_DoesNotThrow()
    {
        CliCommandPolicy.EnsureSetAllowed(false, "version");
    }
}
