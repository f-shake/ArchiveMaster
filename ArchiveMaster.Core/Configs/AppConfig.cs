using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using FzLib.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ArchiveMaster.Configs
{
    /// <summary>
    /// 配置管理中心
    /// </summary>
    /// <remarks>
    /// 每个模块通过<see cref="RegisterConfig"/>上报自己所需要的配置，然后通过<see cref="GetOrCreateConfig{T}"/>获取配置。
    /// 配置基本信息为<see cref="ConfigMetadata"/>，运行后增加配置对象和预设，使用<see cref="ConfigItem"/>存储。
    /// 支持多预设，即能够保存多个配置项，在程序中命名为Preset。
    /// 通常来说，配置项之间的预设是独立的，一个配置增删改预设，不影响其他配置项。
    /// 但可以通过设置组来实现多个配置之间共享预设，一个典型的例子是OfflineSync，模块中的多个板块是步骤的关系，因此需要共享预设。
    /// 共享预设时，提供相同的组名，此时增删改其中一个配置项的预设，会同步反映到其他配置项上。
    /// 启用共享预设时，需要在IModuleInfo中提供Configs时指定每个Config使用相同的Group，并且在TwoStepViewModel初始化时提供Group。
    /// </remarks>
    public class AppConfig
    {
        public const string DEFAULT_PRESET = "默认";

        private const string CONFIG_FILE = "configs.json";
        private const string JKEY_GLOBALS = "Globals";
        private const string JKEY_GROUPS = "Groups";
        private const string JKEY_MODULES = "Modules";
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true,
            Converters =
            {
                new SecurePassword.JsonConverter(),
                new EditableString.JsonConverter()
            }
        };

        /// <summary>
        /// 模块配置的元数据
        /// </summary>
        private readonly Dictionary<string, ConfigMetadata> configMetadata = new Dictionary<string, ConfigMetadata>();

        /// <summary>
        /// 模块配置
        /// </summary>
        private readonly List<ConfigItem> configs = new List<ConfigItem>();

        /// <summary>
        /// 各配置组的当前预设
        /// </summary>
        private Dictionary<string, string> currentPresets = new Dictionary<string, string>();

        public Exception LoadError { get; private set; }

        public void ClearAllPassword()
        {
            foreach (var (c, p) in EnumerateProperties<SecurePassword>())
            {
                p.SetValue(c.Config, new SecurePassword());
            }
        }

        public string GetCurrentPreset(string groupName)
        {
            ArgumentException.ThrowIfNullOrEmpty(groupName);
            return currentPresets.TryGetValue(groupName, out string value) ? value : GetPresets(groupName)[0];
        }

        public T GetOrCreateConfig<T>(string key, string preset = DEFAULT_PRESET) where T : new()
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ConfigItem configItem = configs.FirstOrDefault(p => p.Key == key && p.Preset == preset);
            if (!configMetadata.TryGetValue(key, out ConfigMetadata metadata))
            {
                throw new Exception($"没有注册名为{key}的配置");
            }

            if (configItem == null)
            {
                configItem = new ConfigItem(key, new T(), preset);
                configs.Add(configItem);
            }
            else
            {
                if (configItem.Config is not T)
                {
                    throw new InvalidCastException(
                        $"配置列表中的配置项{key}（预设：{preset ?? "（无）"}）的配置项对象类型（{configItem.Type?.Name}）与请求的类型（{typeof(T).Name}）不一致");
                }
            }

            return (T)configItem.Config;
        }

        public T GetOrCreateConfigWithDefaultKey<T>(string preset = DEFAULT_PRESET) where T : new()
        {
            return GetOrCreateConfig<T>(typeof(T).Name, preset);
        }

        public IReadOnlyList<string> GetPresets(string groupName)
        {
            ArgumentException.ThrowIfNullOrEmpty(groupName);
            var sameGroupConfigs = configMetadata.Values
                .Where(p => p.Group == groupName)
                .Select(p => p.Key)
                .ToHashSet();
            if (sameGroupConfigs.Count == 0)
            {
                throw new Exception($"没有组名为{groupName}的配置文件");
            }

            var presets = configs
                .Where(p => sameGroupConfigs.Contains(p.Key))
                .Select(p => p.Preset)
                .Distinct()
                .ToList();
            if (presets.Count == 0)
            {
                presets.Add(DEFAULT_PRESET);
            }

            return presets.AsReadOnly();
        }

        public void Initialize()
        {
            try
            {
                if (!File.Exists(CONFIG_FILE))
                {
                    return;
                }

                var json = JsonNode.Parse(File.ReadAllText(CONFIG_FILE));
                if (json is not JsonObject jobj)
                {
                    throw new Exception("配置文件内不是Json Object");
                }

                GlobalConfigs.Instance = jobj.ContainsKey(JKEY_GLOBALS)
                    ? (jobj[JKEY_GLOBALS].Deserialize<GlobalConfigs>(JsonOptions) ?? new GlobalConfigs())
                    : new GlobalConfigs();

                //迁移旧版本配置文件
                if (!jobj.ContainsKey(JKEY_MODULES) && !jobj.ContainsKey(JKEY_GROUPS))
                {
                    try
                    {
                        jobj = MigrateOldVersionConfigJson(jobj);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"迁移旧版本配置文件失败：{ex.Message}", ex);
                    }
                }

                if (!jobj.ContainsKey(JKEY_MODULES))
                {
                    throw new Exception($"配置文件内不包含{JKEY_MODULES}");
                }

                if (!jobj.ContainsKey(JKEY_GROUPS))
                {
                    throw new Exception($"配置文件内不包含{JKEY_GROUPS}");
                }

                if (jobj[JKEY_GROUPS] is not JsonObject jg)
                {
                    throw new Exception($"配置文件内的{JKEY_GROUPS}不是Json Object");
                }

                if (jobj[JKEY_MODULES] is not JsonArray jm)
                {
                    throw new Exception($"配置文件内的{JKEY_MODULES}不是Json Array");
                }

                ParseModules(jm);
                ParseGroups(jg);
                ProcessNullObject();
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "加载配置失败");
                LoadError = new Exception($"加载配置文件失败（JSON解析错误），将使用默认配置", ex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载配置失败");
                LoadError = new Exception($"加载配置文件失败，将使用默认配置", ex);
            }
        }

        public void RegisterConfig(ConfigMetadata config)
        {
            ArgumentNullException.ThrowIfNull(config);
            configMetadata.Add(config.Key, config);
        }

        public bool RemovePreset<T>(string key, string preset) where T : ConfigBase, new()
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentException.ThrowIfNullOrEmpty(preset);
            var configItems = configs.Where(p => p.Key == key && p.Preset == preset).ToList();
            if (configItems.Count == 0)
            {
                return false;
            }

            foreach (var configItem in configItems)
            {
                configs.Remove(configItem);
            }

            //考虑设置当前预设
            return true;
        }

        public void RenamePreset(string group, string oldName, string newName)
        {
            ArgumentException.ThrowIfNullOrEmpty(group);
            ArgumentException.ThrowIfNullOrEmpty(oldName);
            ArgumentException.ThrowIfNullOrEmpty(newName);
            foreach (var config in configs.Where(p => p.Group == group && p.Preset == oldName))
            {
                config.Preset = newName;
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    [JKEY_MODULES] = configs,
                    [JKEY_GROUPS] = currentPresets,
                    [JKEY_GLOBALS] = GlobalConfigs.Instance
                }, JsonOptions);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存配置失败");
            }
        }

        public void SetCurrentPreset(string groupName, string preset)
        {
            ArgumentException.ThrowIfNullOrEmpty(groupName);
            ArgumentException.ThrowIfNullOrEmpty(preset);
            currentPresets[groupName] = preset;
        }

        private IEnumerable<(ConfigItem config, PropertyInfo property)> EnumerateProperties<T>()
        {
            var targetType = typeof(T);
            foreach (var config in configs)
            {
                if (config.Config is null)
                {
                    continue;
                }

                var type = config.Config.GetType();
                var properties = type.GetRuntimeProperties();
                foreach (var p in properties)
                {
                    if (p.PropertyType == targetType)
                    {
                        yield return (config, p);
                    }
                }
            }
        }

        private JsonObject MigrateOldVersionConfigJson(JsonObject jobj)
        {
            var root = new JsonObject();
            var jModules = new JsonArray();
            var jGroups = new JsonObject();
            root.Add(JKEY_MODULES, jModules);
            root.Add(JKEY_GROUPS, jGroups);
            foreach (var oldConfig in jobj)
            {
                var key = oldConfig.Key;
                var content = oldConfig.Value;
                if (oldConfig.Key == "PhotoSlimmingCollectionConfig")
                {
                    JsonArray jSubConfigs = (JsonArray)((JsonObject)content)["List"];
                    foreach (JsonObject jSubConfig in jSubConfigs)
                    {
                        string preset = jSubConfig["Name"].GetValue<string>();
                        MigrateConfigItem("PhotoSlimmingConfig", preset, jSubConfig);
                    }
                }
                else if (oldConfig.Key == "OfflineSyncConfig")
                {
                    JsonObject jSubConfigs = (JsonObject)((JsonObject)content)["ConfigCollection"];
                    foreach (var subConfig in jSubConfigs)
                    {
                        var preset = subConfig.Key;
                        for (int i = 1; i <= 3; i++)
                        {
                            var step = ((JsonObject)subConfig.Value)[$"Step{i}"];
                            MigrateConfigItem($"OfflineSyncStep{i}Config", preset, step);
                        }
                    }
                }
                else
                {
                    MigrateConfigItem(key, DEFAULT_PRESET, content);
                }
            }

            return root;

            void MigrateConfigItem(string key, string preset, JsonNode content)
            {
                JsonObject jNewConfig = new JsonObject
                {
                    { nameof(ConfigMetadata.Key), key },
                    { nameof(ConfigMetadata.Group), key },
                    { nameof(ConfigItem.Preset), preset },
                    { nameof(ConfigItem.Config), content.DeepClone() }
                };
                jModules.Add(jNewConfig);
            }
        }

        private void ParseGroups(JsonObject jobj)
        {
            Debug.Assert(jobj != null);
            currentPresets = jobj.Deserialize<Dictionary<string, string>>(JsonOptions);
        }

        private void ParseModules(JsonArray jarray)
        {
            Debug.Assert(jarray != null);
            foreach (var key in configMetadata.Keys)
            {
                var configItemJsons =
                    jarray.Where(p => key.Equals(p[nameof(ConfigMetadata.Key)]?.GetValue<string>()));
                foreach (var j in configItemJsons)
                {
                    var preset = j[nameof(ConfigItem.Preset)]?.GetValue<string>();
                    var instance = j[nameof(ConfigItem.Config)]?.Deserialize(configMetadata[key].Type, JsonOptions);
                    configs.Add(new ConfigItem(key, instance, preset));
                }
            }
        }

        /// <summary>
        /// 将一些由于配置升级等原因造成的配置属性为空的配置项，恢复为默认值
        /// </summary>
        private void ProcessNullObject()
        {
            foreach (var (c, p) in EnumerateProperties<SecurePassword>())
            {
                if (p.GetValue(c.Config) is null)
                {
                    p.SetValue(c.Config, new SecurePassword());
                }
            }

            foreach (var (c, p) in EnumerateProperties<FileFilterRule>())
            {
                if (p.GetValue(c.Config) is null)
                {
                    p.SetValue(c.Config, new FileFilterRule());
                }
            }
        }
    }
}