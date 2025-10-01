using System.Security.Cryptography;
using System.Text;
using ArchiveMaster.Helpers;
using FzLib.Application;
using FzLib.Cryptography;
using Serilog;

namespace ArchiveMaster.Services;

public class SecurePasswordStoreService
{
    public const int AesGcmTagLength = 16;

    public static string SavePassword(string password, string masterPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = DeriveKey(masterPassword, salt);

        var nonce = RandomNumberGenerator.GetBytes(12); // AES-GCM nonce
        var plain = Encoding.UTF8.GetBytes(password);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(key, AesGcmTagLength))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(salt.Length);
        bw.Write(salt);
        bw.Write(nonce.Length);
        bw.Write(nonce);
        bw.Write(tag.Length);
        bw.Write(tag);
        bw.Write(cipher.Length);
        bw.Write(cipher);
        return Convert.ToHexString(ms.ToArray());
    }

    public static string LoadPassword(string hexString, string masterPassword)
    {
        using var fs = new MemoryStream(Convert.FromHexString(hexString));
        using var br = new BinaryReader(fs);

        var salt = br.ReadBytes(br.ReadInt32());
        var nonce = br.ReadBytes(br.ReadInt32());
        var tag = br.ReadBytes(br.ReadInt32());
        var cipher = br.ReadBytes(br.ReadInt32());

        var key = DeriveKey(masterPassword, salt);
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(key, AesGcmTagLength))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(masterPassword, salt, 200_000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit AES key
    }

    public static bool VerifyMasterPassword(string masterPassword, string hexString)
    {
        try
        {
            var decrypted = DecryptMasterPassword(hexString);
            return decrypted == masterPassword;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "验证主密码失败");
            return false;
        }
    }

    public static string DecryptMasterPassword(string hexString)
    {
        string pswd = IdentityProvider.GetCombinedId();
        var aes = AesHelper.GetDefaultAes(pswd);
        var bytes = Convert.FromHexString(hexString);
        var decryptedBytes = aes.Decrypt(bytes);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    public static string EncryptMasterPassword(string masterPassword)
    {
        string pswd = IdentityProvider.GetCombinedId();
        var aes = AesHelper.GetDefaultAes(pswd);
        var bytes = Encoding.UTF8.GetBytes(masterPassword);
        var encryptedBytes = aes.Encrypt(bytes);
        return Convert.ToHexString(encryptedBytes);
    }
}