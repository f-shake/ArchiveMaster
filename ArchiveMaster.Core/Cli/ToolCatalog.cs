using System;
using System.Collections.Generic;
using System.Reflection;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Cli;

/// <summary>
/// 从模块清单建立工具目录。GUI 与 CLI 看到的工具来源一致，排除项由调用方给出。
/// </summary>
public static class ToolCatalog
{
    /// <summary>
    /// 已审计出“准备阶段会写文件”的工具。新增工具若在 InitializeAsync 里创建目录或写文件，必须登记到这里。
    /// </summary>
    private static readonly HashSet<string> PrepareUnsafeToolIds = new(StringComparer.Ordinal)
    {
        "writeOnceArchive.packing",
    };

    /// <summary>
    /// 只返回聚合结果、不提供条目级列表的工具。它们的结果通过 ToolRunResult.Notes 说明。
    /// </summary>
    private static readonly HashSet<string> NoItemResultToolIds = new(StringComparer.Ordinal)
    {
        "offlineSync.step1",
        "writeOnceArchive.packing",
        "textTools.typoChecker",
        "textTools.lineByLineProcessor",
        "photoTagging.photoTagManager",
        "photoTagging.photoTagSearcher",
    };

    public static IReadOnlyList<ToolDescriptor> Build(IEnumerable<IModuleInfo> modules,
        ISet<Type> excludedModuleTypes = null)
    {
        ArgumentNullException.ThrowIfNull(modules);
        excludedModuleTypes ??= new HashSet<Type>();

        var result = new List<ToolDescriptor>();
        var ids = new Dictionary<string, string>();

        foreach (IModuleInfo module in modules)
        {
            if (module == null || excludedModuleTypes.Contains(module.GetType()))
            {
                continue;
            }

            if (module.Views?.Panels == null)
            {
                continue;
            }

            string modulePascal = GetModulePascalName(module.GetType());
            string moduleId = LowerFirst(modulePascal);

            foreach (ToolPanelInfo panel in module.Views.Panels)
            {
                if (panel?.ViewModelType == null || !TryResolve(panel.ViewModelType, out Type serviceType,
                        out Type configType, out bool hasPrepare))
                {
                    // 聊天类、非两步式工具没有 TwoStepViewModelBase，直接跳过
                    continue;
                }

                string toolName = GetToolName(configType, modulePascal);
                string id = $"{moduleId}.{toolName}";
                if (ids.TryGetValue(id, out string existing))
                {
                    throw new InvalidOperationException(
                        $"工具 ID 冲突：{id}（{existing} 与 {panel.ViewModelType.FullName}）。请检查配置类名或模块名。");
                }

                ids.Add(id, panel.ViewModelType.FullName);
                result.Add(new ToolDescriptor()
                {
                    Id = id,
                    ModuleId = moduleId,
                    ModuleName = module.ModuleName,
                    Title = panel.Title,
                    Description = panel.Description,
                    ConfigType = configType,
                    ServiceType = serviceType,
                    HasPrepareStage = hasPrepare,
                    PrepareUnsafe = PrepareUnsafeToolIds.Contains(id),
                    HasItemResults = !NoItemResultToolIds.Contains(id),
                });
            }
        }

        return result;
    }

    private static bool TryResolve(Type viewModelType, out Type serviceType, out Type configType,
        out bool hasPrepare)
    {
        serviceType = null;
        configType = null;
        hasPrepare = false;

        for (Type type = viewModelType; type != null; type = type.BaseType)
        {
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(TwoStepViewModelBase<,>))
            {
                continue;
            }

            Type[] arguments = type.GetGenericArguments();
            serviceType = arguments[0];
            configType = arguments[1];

            // EnableInitialize 是 virtual，未覆写时 getter 声明在 TwoStepViewModelBase 上；
            // 现有覆写均为 => false，因此“声明在当前类型链更下方”就视为没有准备阶段。
            MethodInfo getter = viewModelType.GetProperty("EnableInitialize")?.GetGetMethod();
            hasPrepare = getter?.DeclaringType == type;
            return true;
        }

        return false;
    }

    private static string GetModulePascalName(Type moduleType)
    {
        string name = moduleType.Name;
        const string suffix = "ModuleInfo";
        if (name.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = name[..^suffix.Length];
        }

        return name;
    }

    private static string GetToolName(Type configType, string modulePascal)
    {
        string name = configType.Name;
        const string suffix = "Config";
        if (name.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = name[..^suffix.Length];
        }

        // OfflineSyncStep1Config -> step1：模块前缀在 ID 里已经出现过一次
        if (name.StartsWith(modulePascal, StringComparison.Ordinal) && name.Length > modulePascal.Length)
        {
            name = name[modulePascal.Length..];
        }

        return LowerFirst(name);
    }

    private static string LowerFirst(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
