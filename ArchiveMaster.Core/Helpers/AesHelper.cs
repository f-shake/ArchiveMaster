using System.Security.Cryptography;
using System.Text;

namespace ArchiveMaster.Helpers;

public static class AesHelper
{
    public static Aes GetDefaultAes(string password)
    {
        Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.SetStringKey(password);
        return aes;
    }

    public static Aes SetStringKey(this Aes manager, string key)
    {
        //using var deriveBytes = new Rfc2898DeriveBytes(key, Encoding.UTF8.GetBytes(nameof(ArchiveMaster)), 100000,
        //    HashAlgorithmName.SHA256);
        //manager.Key = deriveBytes.GetBytes(manager.KeySize / 8);
        //return manager;


        // 获取盐值的字节数组
        byte[] salt = Encoding.UTF8.GetBytes(nameof(ArchiveMaster));
        // 计算需要的密钥字节长度
        int keySizeInBytes = manager.KeySize / 8;

        // 使用官方推荐的静态 Pbkdf2 方法，消除 SYSLIB0060 警告
        manager.Key = Rfc2898DeriveBytes.Pbkdf2(
            key,
            salt,
            100000,
            HashAlgorithmName.SHA256,
            keySizeInBytes
        );

        return manager;
    }
}