using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public interface IModuleInfo
    {
        public IList<Type> BackgroundServices { get; }
        public IList<ConfigMetadata> Configs { get; }
        public string ModuleName { get; }
        public string ModuleDescription { get; }
        public int Order { get; }
        public IList<Type> SingletonServices { get; }
        public IList<Type> TransientServices { get; }
        public ToolPanelGroupInfo Views { get; }
    }
}
