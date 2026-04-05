using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public class PhotoTagGeneratorService(AppConfig appConfig)
        : TwoStepServiceBase<PhotoTagGeneratorConfig>(appConfig)
    {
        public override Task ExecuteAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            throw new NotImplementedException();
        }

        public override Task InitializeAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}