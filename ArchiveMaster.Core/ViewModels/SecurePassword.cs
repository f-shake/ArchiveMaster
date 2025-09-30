using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using Serilog;

namespace ArchiveMaster.ViewModels;

public class SecurePassword
{
    public SecurePassword()
    {
    }

    public SecurePassword(string password)
    {
        Password = password;
    }

    public string Password { get; set; }

    public bool Remember { get; set; }

    public static implicit operator string(SecurePassword securePassword) => securePassword.Password;
    public static implicit operator SecurePassword(string password) => new SecurePassword(password);


    public class JsonConverter : JsonConverter<SecurePassword>
    {
        private static readonly Regex HexRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);

        public override SecurePassword Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string storedPassword = reader.GetString();
                var sp = new SecurePassword();
                try
                {
                    if (HexRegex.IsMatch(storedPassword))
                    {
                        sp.Password =
                            SecurePasswordStoreService.LoadPassword(storedPassword,
                                GlobalConfigs.Instance.MajorPassword);
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

            throw new JsonException("Expected string or null for SecurePassword.");
        }

        public override void Write(Utf8JsonWriter writer, SecurePassword value, JsonSerializerOptions options)
        {
            if (value.Remember)
            {
                try
                {
                    string pswd =
                        SecurePasswordStoreService.SavePassword(value.Password, GlobalConfigs.Instance.MajorPassword);
                    writer.WriteStringValue(pswd);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "保存密码失败");
                    writer.WriteNullValue();
                }
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}