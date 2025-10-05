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
    public class SmartDocSearchService(AppConfig appConfig)
        : TwoStepServiceBase<SmartDocSearchConfig>(appConfig)
    {

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return null;
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            throw new  NotSupportedException();
        }
        public override async Task InitializeAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}