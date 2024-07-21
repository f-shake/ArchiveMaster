﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArchiveMaster.Utilities
{
    public abstract class TwoStepUtilityBase: UtilityBase
    {
        public abstract Task ExecuteAsync(CancellationToken token);

        public abstract Task InitializeAsync();
    }
}
