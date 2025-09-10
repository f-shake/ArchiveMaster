using System;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public abstract class TwoStepServiceBase<TConfig>(AppConfig appConfig) : ServiceBase<TConfig>(appConfig)
        where TConfig : ConfigBase
    {
        public abstract Task ExecuteAsync(CancellationToken ct = default);

        public abstract IEnumerable<SimpleFileInfo> GetInitializedFiles();

        public virtual IEnumerable<SimpleFileInfo> GetExecutedFiles()
        {
            return GetInitializedFiles();
        }

        public abstract Task InitializeAsync(CancellationToken ct = default);
    }
}