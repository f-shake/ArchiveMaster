using ArchiveMaster.Configs;

namespace ArchiveMaster.Tests.Infrastructure;

public static class TestAppConfig
{
    public static AppConfig Create(TempWorkspace workspace, string fileName = "configs.json")
    {
        return new AppConfig(workspace.PathOf(fileName));
    }
}
