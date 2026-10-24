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
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;
using ArchiveMaster.Converters;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using FzLib.IO;
using FzLib.Text;
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
        public const string ConfigBackupExtension = ".bak";

        public const string DefaultPresetName = "默认";

        public const string NextStartLoadConfigExtension = ".load";

        private const string ConfigFileName = "configs.json";

        private const string JkeyGlobals = "Globals";

        private const string JkeyGroups = "Groups";

        private const string JkeyModules = "Modules";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true,
            Converters =
            {
                new SecurePassword.JsonConverter(),
                new EditableString.JsonConverter(),
                new ColorJsonConverter()
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

        public AppConfig() : this(ConfigFileName)
        {
        }

        public AppConfig(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ConfigPath = Path.GetFullPath(path);
        }

        public string ConfigPath { get; }
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
            var presets = GetPresets(groupName);
            if (currentPresets.TryGetValue(groupName, out string value) && presets.Contains(value))
            {
                return value;
            }

            // 预设被删除/改名后 currentPresets 里可能残留旧名字，
            // 若不修正，界面会显示空预设并在下次保存时“复活”一份默认配置
            string fallback = presets[0];
            currentPresets[groupName] = fallback;
            return fallback;
        }

        public T GetOrCreateConfig<T>(string key, string preset = DefaultPresetName) where T : new()
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ConfigItem configItem = configs.FirstOrDefault(p => p.Key == key && p.Preset == preset);
            if (!configMetadata.TryGetValue(key, out ConfigMetadata metadata))
            {
                throw new Exception($"没有注册名为{key}的配置");
            }

            if (configItem == null)
            {
                configItem = new ConfigItem(key, new T(), preset, metadata.Group);
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

        public T GetOrCreateConfigWithDefaultKey<T>(string preset = DefaultPresetName) where T : new()
        {
            return GetOrCreateConfig<T>(typeof(T).Name, preset);
        }

        /// <summary>
        /// 按配置类型取配置，供 CLI 等无法在编译期写出泛型参数的场景使用。
        /// </summary>
        public object GetOrCreateConfig(Type configType, string preset = DefaultPresetName)
        {
            ArgumentNullException.ThrowIfNull(configType);
            string key = configMetadata.Values.FirstOrDefault(p => p.Type == configType)?.Key;
            if (key == null)
            {
                throw new Exception($"没有注册类型为{configType.Name}的配置");
            }

            ConfigItem configItem = configs.FirstOrDefault(p => p.Key == key && p.Preset == preset);
            if (configItem == null)
            {
                configItem = new ConfigItem(key, Activator.CreateInstance(configType), preset, configMetadata[key].Group);
                configs.Add(configItem);
            }
            else if (!configType.IsInstanceOfType(configItem.Config))
            {
                throw new InvalidCastException(
                    $"配置列表中的配置项{key}（预设：{preset ?? "（无）"}）的配置项对象类型（{configItem.Type?.Name}）与请求的类型（{configType.Name}）不一致");
            }

            return configItem.Config;
        }

        /// <summary>
        /// 判断某个配置类型是否存在指定名称的预设。CLI 用它避免把写错的预设名静默当成默认配置。
        /// </summary>
        public bool HasPreset(Type configType, string preset)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ConfigMetadata metadata = configMetadata.Values.FirstOrDefault(p => p.Type == configType);
            if (metadata == null)
            {
                return false;
            }

            // 共享预设组（如 OfflineSync 的多个步骤）里，只要组内任一配置有该预设，
            // 就允许当前步骤使用；缺失的步骤会像 GUI 一样按默认值创建。
            HashSet<string> groupKeys = configMetadata.Values
                .Where(p => p.Group == metadata.Group)
                .Select(p => p.Key)
                .ToHashSet();
            return configs.Any(p => groupKeys.Contains(p.Key) && p.Preset == preset);
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
                presets.Add(DefaultPresetName);
            }

            return presets.AsReadOnly();
        }

        public static void AddJsonConverter(JsonConverter converter)
        {
            JsonOptions.Converters.Add(converter);
        }

        /// <summary>
        /// 创建一份包含当前已注册转换器的序列化选项，供 CLI 等无 GUI 场景复用同一套配置格式。
        /// </summary>
        public static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
            };
            foreach (JsonConverter converter in JsonOptions.Converters)
            {
                options.Converters.Add(converter);
            }

            return options;
        }

        /// <summary>
        /// 加载配置。
        /// </summary>
        /// <param name="applyPendingLoad">
        /// 是否处理 <c>configs.json.load</c>（会覆盖 configs.json）。CLI 传入 false，保证只读。
        /// </param>
        public void Initialize(bool applyPendingLoad = true)
        {
            try
            {
                if (applyPendingLoad && File.Exists(ConfigPath + NextStartLoadConfigExtension))
                {
                    if (File.Exists(ConfigPath))
                    {
                        // 先留一份 .bak，再把待生效文件改名覆盖。不用 File.Replace：
                        // 它在部分文件系统/同步盘上会直接拒绝访问
                        File.Copy(ConfigPath, ConfigPath + ConfigBackupExtension, true);
                    }

                    File.Move(ConfigPath + NextStartLoadConfigExtension, ConfigPath, true);
                }

                if (!File.Exists(ConfigPath))
                {
                    return;
                }

                var json = JsonNode.Parse(File.ReadAllText(ConfigPath));
                if (json is not JsonObject jobj)
                {
                    throw new Exception("配置文件内不是Json Object");
                }

                //反序列化两次。第一次时，MasterPassword还没反序列化，导致GlobalConfigs中的SecurePassword类型的属性无法被反序列化。
                //所以，进行第二次反序列化。
                //虽然不是合适的解决方案，但能用。
                GlobalConfigs.Instance = jobj.ContainsKey(JkeyGlobals)
                    ? (jobj[JkeyGlobals].Deserialize<GlobalConfigs>(JsonOptions) ?? new GlobalConfigs())
                    : GlobalConfigs.GetEmptyInstance();
                GlobalConfigs.Instance = jobj.ContainsKey(JkeyGlobals)
                    ? (jobj[JkeyGlobals].Deserialize<GlobalConfigs>(JsonOptions) ?? new GlobalConfigs())
                    : GlobalConfigs.GetEmptyInstance();

                //迁移旧版本配置文件
                if (!jobj.ContainsKey(JkeyModules) && !jobj.ContainsKey(JkeyGroups))
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

                if (!jobj.ContainsKey(JkeyModules))
                {
                    throw new Exception($"配置文件内不包含{JkeyModules}");
                }

                if (!jobj.ContainsKey(JkeyGroups))
                {
                    throw new Exception($"配置文件内不包含{JkeyGroups}");
                }

                if (jobj[JkeyGroups] is not JsonObject jg)
                {
                    throw new Exception($"配置文件内的{JkeyGroups}不是Json Object");
                }

                if (jobj[JkeyModules] is not JsonArray jm)
                {
                    throw new Exception($"配置文件内的{JkeyModules}不是Json Array");
                }

                ParseModules(jm);
                ParseGroups(jg);
                ProcessAllNullObjects();
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

        public void LoadNextStart(string bakConfigPath)
        {
            var tempConfig = new AppConfig(bakConfigPath);
            var previousGlobals = GlobalConfigs.Instance;
            tempConfig.Initialize(applyPendingLoad: false);
            // Initialize 会顺带把 GlobalConfigs.Instance 换成导入文件里的值。
            // 导入的语义是“下次启动生效”，当前会话必须还原，否则会立刻换掉主密码/删除方式/AI 配置
            GlobalConfigs.Instance = previousGlobals;
            if (tempConfig.LoadError != null)
            {
                // Initialize 内部会把异常吞成 LoadError，这里必须显式检查，
                // 否则损坏的配置文件会被接受并在下次启动顶替现有配置
                throw new Exception($"加载配置文件{bakConfigPath}失败：{tempConfig.LoadError.Message}",
                    tempConfig.LoadError);
            }

            File.Copy(bakConfigPath, ConfigPath + NextStartLoadConfigExtension, true);
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
            if (!configMetadata.TryGetValue(key, out ConfigMetadata metadata))
            {
                return false;
            }

            var configItems = configs
                .Where(p => p.Group == metadata.Group && p.Preset == preset)
                .ToList();
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

        private readonly object saveLock = new object();

        public void Save()
        {
            Save(ConfigPath);
        }

        public void Save(string configFilePath)
        {
            lock (saveLock)
            {
                string tempPath = configFilePath + ".tmp";
                try
                {
                    var json = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        [JkeyModules] = configs,
                        [JkeyGroups] = currentPresets,
                        [JkeyGlobals] = GlobalConfigs.Instance
                    }, JsonOptions);
                    // 先写临时文件再替换：保存中断不会留下半截 configs.json
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, configFilePath, true);
                }
                catch (Exception ex)
                {
                    if (File.Exists(tempPath))
                    {
                        try
                        {
                            File.Delete(tempPath);
                        }
                        catch
                        {
                            // 清理失败不影响原始异常
                        }
                    }

                    Log.Error(ex, "保存配置失败");
                    throw new IOException($"保存配置到{configFilePath}失败：{ex.Message}", ex);
                }
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


        private IEnumerable<(ConfigItem config, PropertyInfo property)> EnumerateProperties()
        {
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
                    yield return (config, p);
                }
            }
        }

        private JsonObject MigrateOldVersionConfigJson(JsonObject jobj)
        {
            var root = new JsonObject();
            var jModules = new JsonArray();
            var jGroups = new JsonObject();
            root.Add(JkeyModules, jModules);
            root.Add(JkeyGroups, jGroups);
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
                    MigrateConfigItem(key, DefaultPresetName, content);
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
                    configs.Add(new ConfigItem(key, instance, preset, configMetadata[key].Group));
                }
            }
        }

        /// <summary>
        /// 将一些由于配置升级等原因造成的配置属性为空的配置项，恢复为默认值
        /// </summary>
        private void ProcessAllNullObjects()
        {
            foreach (var (c, p) in EnumerateProperties())
            {
                if (p.PropertyType.IsClass
                    && p.GetValue(c.Config) is null
                    && !p.PropertyType.Namespace.StartsWith("System")) //如果是Class且为null，就创建一个实例
                {
                    if (p.PropertyType.GetConstructor(Type.EmptyTypes) is ConstructorInfo) //需要有无参构造函数
                    {
                        p.SetValue(c.Config, Activator.CreateInstance(p.PropertyType));
                    }
                }
            }
        }

        private void ProcessNullObjects<T>() where T : new()
        {
            foreach (var (c, p) in EnumerateProperties<T>())
            {
                if (p.GetValue(c.Config) is null)
                {
                    p.SetValue(c.Config, new T());
                }
            }
        }
    }
}