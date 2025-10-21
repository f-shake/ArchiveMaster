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
    public SecurePassword()
    {
    }

    public SecurePassword(string password)
    {
        Password = password;
    }

    [ObservableProperty]
    private string password;

    [ObservableProperty]
    private bool remember = true;

    public static implicit operator SecurePassword(string password) => new SecurePassword(password);

    public static implicit operator string(SecurePassword securePassword) => securePassword.Password;

    public class JsonConverter(bool alwaysRemember) : JsonConverter<SecurePassword>
    {
        public bool AlwaysRemember { get; } = alwaysRemember;

        public JsonConverter() : this(false)
        {
        }

        private static readonly Regex HexRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);

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

                var sp = new SecurePassword();
                try
                {
                    if (HexRegex.IsMatch(storedPassword))
                    {
                        var masterPassword = GlobalConfigs.Instance.MasterPassword;
                        masterPassword = SecurePasswordStoreService.DecryptMasterPassword(masterPassword);
                        sp.Password =
                            SecurePasswordStoreService.LoadPassword(storedPassword, masterPassword);
                    }
                    else
                    {
                        sp.Password = storedPassword;
                    }

                    sp.Remember = true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "读取密码失败");
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
                try
                {
                    if (string.IsNullOrEmpty(value.Password))
                    {
                        writer.WriteStringValue("");
                    }
                    else
                    {
                        var masterPassword = GlobalConfigs.Instance.MasterPassword;
                        masterPassword = SecurePasswordStoreService.DecryptMasterPassword(masterPassword);
                        string pswd =
                            SecurePasswordStoreService.SavePassword(value.Password, masterPassword);
                        writer.WriteStringValue(pswd);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "保存密码失败");
                    writer.WriteStringValue("");
                }
            }
            else
            {
                writer.WriteStringValue("");
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SecurePasswordAlwaysRememberAttribute()
        : JsonConverterAttribute(typeof(AlwaysRememberJsonConverter));

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
}