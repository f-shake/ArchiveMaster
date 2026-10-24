using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace ArchiveMaster.Configs;

public partial class SecurePassword : ObservableObject
{
    [ObservableProperty]
    private string password;

    [ObservableProperty]
    private bool remember = true;

    /// <summary>
    /// 从配置文件读到的原始密文。解密失败时保留它，保存时原样写回，
    /// 避免一次失败的读取在下次保存时把用户密码清空。
    /// </summary>
    internal string OriginalCipherText { get; set; }

    /// <summary>
    /// 读取时是否解密失败。只有失败时才回写原密文；用户主动清空密码仍应真正清空。
    /// </summary>
    internal bool DecryptionFailed { get; set; }

    public SecurePassword()
    {
    }

    public SecurePassword(string password)
    {
        Password = password;
    }
    public static implicit operator SecurePassword(string password) => new SecurePassword(password);

    public static implicit operator string(SecurePassword securePassword) => securePassword.Password;

    public class AlwaysRememberJsonConverter : JsonConverter<SecurePassword>
    {
        /*SecurePasswordAlwaysRememberAttribute 继承自 JsonConverterAttribute，
         System.Text.Json 在解析类型元数据的时候，会自动去扫描属性是否有 JsonConverterAttribute，
         如果有，就直接实例化里面指定的 converter（也就是你 base(typeof(SecurePasswordAlwaysRememberJsonConverter)) 传进去的那个类型）。*/

        private readonly JsonConverter inner =
            new JsonConverter(alwaysRemember: true);

        public override SecurePassword Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
            => inner.Read(ref reader, typeToConvert, options);

        public override void Write(Utf8JsonWriter writer, SecurePassword value, JsonSerializerOptions options)
            => inner.Write(writer, value, options);
    }

    public partial class JsonConverter(bool alwaysRemember) : JsonConverter<SecurePassword>
    {
        private static readonly Regex HexRegex = GenerateHexRegex();
        public JsonConverter() : this(false)
        {
        }

        public bool AlwaysRemember { get; } = alwaysRemember;
        public override SecurePassword Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string storedPassword = reader.GetString();
                if (string.IsNullOrWhiteSpace(storedPassword))
                {
                    return new SecurePassword { Remember = false };
                }

                var sp = new SecurePassword { OriginalCipherText = storedPassword };
                try
                {
                    //检测是不是新版本加密后的密码（不一定准确）
                    if (HexRegex.IsMatch(storedPassword))
                    {
                        var masterPassword = GlobalConfigs.Instance.MasterPassword;
                        masterPassword = SecurePasswordStoreService.DecryptMasterPassword(masterPassword);
                        sp.Password =
                            SecurePasswordStoreService.LoadPassword(storedPassword, masterPassword);
                    }
                    else
                    {
                        //明文存储的密码
                        sp.Password = storedPassword;
                    }

                    sp.Remember = true;
                }
                catch (Exception ex)
                {
                    // 解密失败时保留原文，保存时写回，避免覆盖掉仍可能有效的密文；
                    // 用独立标志区分“解密失败”与“用户主动清空密码”
                    sp.DecryptionFailed = true;
                    Log.Error(ex, "读取密码失败，保留原始密文");
                }

                return sp;
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return new SecurePassword { Remember = false };
            }

            throw new JsonException($"期望之外的类型{reader.TokenType}，无法转换为SecurePassword");
        }

        public override void Write(Utf8JsonWriter writer, SecurePassword value, JsonSerializerOptions options)
        {
            if (value.Remember || AlwaysRemember)
            {
                if (string.IsNullOrEmpty(value.Password))
                {
                    // 只有“读取时解密失败”才回写原密文；用户主动清空密码时应当真正清空
                    writer.WriteStringValue(value.DecryptionFailed ? value.OriginalCipherText ?? "" : "");
                    return;
                }

                try
                {
                    var masterPassword = GlobalConfigs.Instance.MasterPassword;
                    masterPassword = SecurePasswordStoreService.DecryptMasterPassword(masterPassword);
                    string pswd =
                        SecurePasswordStoreService.SavePassword(value.Password, masterPassword);
                    writer.WriteStringValue(pswd);
                }
                catch (Exception ex)
                {
                    // 保存失败必须让整个配置保存失败，否则会静默丢掉密码
                    Log.Error(ex, "保存密码失败");
                    throw new InvalidOperationException($"保存密码失败：{ex.Message}", ex);
                }
            }
            else
            {
                writer.WriteStringValue("");
            }
        }

        [GeneratedRegex("^[0-9a-fA-F]{64,}$", RegexOptions.Compiled)]
        private static partial Regex GenerateHexRegex();
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SecurePasswordAlwaysRememberAttribute()
        : JsonConverterAttribute(typeof(AlwaysRememberJsonConverter));
}