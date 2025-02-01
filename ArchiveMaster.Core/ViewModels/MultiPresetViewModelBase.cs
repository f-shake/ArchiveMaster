using System.Collections.ObjectModel;
using System.Diagnostics;
using ArchiveMaster.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Messages;
using Mapster;

namespace ArchiveMaster.ViewModels;

public abstract partial class MultiPresetViewModelBase<TConfig> : ViewModelBase where TConfig : ConfigBase, new()
{
    /// <summary>
    /// 当前配置项
    /// </summary>
    [ObservableProperty]
    private TConfig config;

    /// <summary>
    /// 当前配置版本的名称
    /// </summary>
    [ObservableProperty]
    private string presetName;

    /// <summary>
    /// 所有配置项版本的名称
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> presetNames;

    private bool processOnPresetNameChanged = true;

    protected MultiPresetViewModelBase(AppConfig appConfig, string configGroupName)
    {
        ConfigGroupName = configGroupName;
        AppConfig = appConfig;
    }


    /// <summary>
    /// 配置管理
    /// </summary>
    public AppConfig AppConfig { get; }

    /// <summary>
    /// 配置版本的组名（见AppConfig）
    /// </summary>
    protected string ConfigGroupName { get; }

    /// <summary>
    /// 进入面板，重置配置和页面
    /// </summary>
    public override void OnEnter()
    {
        base.OnEnter();
        processOnPresetNameChanged = false;
        try
        {
            PresetNames = new ObservableCollection<string>(AppConfig.GetPresets(ConfigGroupName));
            PresetName = AppConfig.GetCurrentPreset(ConfigGroupName);
            processOnPresetNameChanged = true;
            OnPresetNameChanged(PresetName);
        }
        finally
        {
            processOnPresetNameChanged = true;
        }
    }

    protected virtual void OnConfigChanged()
    {
    }

    /// <summary>
    /// 新增配置版本
    /// </summary>
    /// <exception cref="Exception"></exception>
    [RelayCommand]
    private async Task AddPresetAsync()
    {
        if (await this.SendMessage(new InputDialogMessage()
            {
                Type = InputDialogMessage.InputDialogType.Text,
                Title = "新增配置",
                DefaultValue = "新配置",
                Validation = t =>
                {
                    if (string.IsNullOrWhiteSpace(t))
                    {
                        throw new Exception("配置名为空");
                    }

                    if (PresetNames.Contains(t))
                    {
                        throw new Exception("配置名已存在");
                    }
                }
            }).Task is string result)
        {
            PresetNames.Add(result);
            AppConfig.GetOrCreateConfigWithDefaultKey<TConfig>(result);
            PresetName = result;
        }
    }

    /// <summary>
    /// 建立当前配置的副本
    /// </summary>
    [RelayCommand]
    private void ClonePreset()
    {
        string newName = PresetName;
        int i = 1;
        while (PresetNames.Contains(newName))
        {
            i++;
            newName = $"{PresetName} ({i})";
        }

        var newConfig = AppConfig.GetOrCreateConfigWithDefaultKey<TConfig>(newName);
        Config.Adapt(newConfig);
        PresetNames.Add(newName);
        PresetName = newName;
    }

    /// <summary>
    /// 修改当前配置的版本名称
    /// </summary>
    /// <exception cref="Exception"></exception>
    [RelayCommand]
    private async Task ModifyPresetNameAsync()
    {
        if (await this.SendMessage(new InputDialogMessage()
            {
                Type = InputDialogMessage.InputDialogType.Text,
                Title = "修改配置名称",
                DefaultValue = PresetName,
                Validation = t =>
                {
                    if (string.IsNullOrWhiteSpace(t))
                    {
                        throw new Exception("配置名为空");
                    }

                    if (PresetNames.Contains(t))
                    {
                        throw new Exception("配置名已存在");
                    }
                }
            }).Task is string result)
        {
            AppConfig.RenamePreset(ConfigGroupName, PresetName, result);

            var index = PresetNames.IndexOf(PresetName);
            Debug.Assert(index >= 0);
            PresetNames[index] = result;
            PresetName = result;
        }
    }

    /// <summary>
    /// 配置版本改变，重新获取该配置版本的配置对象
    /// </summary>
    /// <param name="value"></param>
    partial void OnPresetNameChanged(string value)
    {
        if (!processOnPresetNameChanged)
        {
            return;
        }

        if (value == null)
        {
            Config = null;
            return;
        }

        AppConfig.SetCurrentPreset(ConfigGroupName, value);
        Config = AppConfig.GetOrCreateConfigWithDefaultKey<TConfig>(value);
        OnConfigChanged();
    }

    /// <summary>
    /// 移除当前配置
    /// </summary>
    [RelayCommand]
    private async Task RemovePresetAsync()
    {
        var name = PresetName;
        var result = await this.SendMessage(new CommonDialogMessage()
        {
            Type = CommonDialogMessage.CommonDialogType.YesNo,
            Title = "删除配置",
            Message = $"是否移除配置：{name}？"
        }).Task;
        if (result.Equals(true))
        {
            PresetNames.Remove(name);
            AppConfig.RemovePreset<TConfig>(typeof(TConfig).Name, name);
            if (PresetNames.Count == 0)
            {
                PresetNames.Add(AppConfig.DEFAULT_PRESET);
                PresetName = AppConfig.DEFAULT_PRESET;
            }
            else
            {
                PresetName = PresetNames[0];
            }
        }
    }
}