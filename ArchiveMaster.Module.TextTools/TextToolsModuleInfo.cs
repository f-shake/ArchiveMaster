using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public class TextToolsModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.TextTools/Assets/";
        public IList<Type> BackgroundServices { get; }

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(TextRewriterConfig)),
            new ConfigMetadata(typeof(TextEncryptionConfig)),
            new ConfigMetadata(typeof(SmartDocSearchConfig)),
            new ConfigMetadata(typeof(TypoCheckerConfig)),
            new ConfigMetadata(typeof(AiProvidersConfig)),
        ];

        public string ModuleName => "文本工具";
        public string ModuleDescription => "对文本或文本文件进行相关处理";

#if DEBUG
        public int Order => -1;
#else
        public int Order => 3;
#endif
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
        [
            typeof(SmartDocSearchService),
            typeof(TextEncryptionService),
            typeof(TypoCheckerService),
            typeof(TextRewriterService),
        ];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(TextRewriterPanel), typeof(TextRewriterViewModel), "文本改写",
                    "使用AI对文本进行改写，实现文本的优化、简化、扩展、翻译、摘要等", baseUrl + "rewrite.svg"),
                new ToolPanelInfo(typeof(TextEncryptionPanel), typeof(TextEncryptionViewModel), "文本混淆",
                    "使用替换式密码的方式混淆文本，实现防君子不防小人的文本加密", baseUrl + "encrypt.svg"),
                new ToolPanelInfo(typeof(SmartDocSearchPanel), typeof(SmartDocSearchViewModel), "文档智能搜索",
                    "从多个文档中搜索关键词，并通过AI进行总结归纳", baseUrl + "docSearch.svg"),
                new ToolPanelInfo(typeof(TypoCheckerPanel), typeof(TypoCheckerViewModel), "错别字检查",
                    "使用AI检查文本是否存在错别字", baseUrl + "typo.svg"),
                new ToolPanelInfo(typeof(AiProvidersPanel), typeof(AiProvidersViewModel), "AI服务提供商",
                    "配置AI服务提供商", baseUrl + "ai.svg"),
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
    }
}