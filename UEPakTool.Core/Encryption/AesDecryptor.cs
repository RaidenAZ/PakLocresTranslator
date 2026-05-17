using System.Security.Cryptography;

namespace UEPakTool.Core.Encryption;

public static class AesDecryptor
{
    public const int ALIGN = 16;
    private const int BLOCK_SIZE = 128;

    private static readonly System.Security.Cryptography.Aes Provider;

    static AesDecryptor()
    {
        Provider = System.Security.Cryptography.Aes.Create();
        Provider.Mode = CipherMode.ECB;
        Provider.Padding = PaddingMode.None;
        Provider.BlockSize = BLOCK_SIZE;
    }

    public static byte[] Decrypt(byte[] data, FAesKey key)
    {
        return Provider.CreateDecryptor(key.Key, null)
            .TransformFinalBlock(data, 0, data.Length);
    }

    public static byte[] DecryptSafe(byte[] data, FAesKey key)
    {
        var aligned = AlignHelper.Align(data.Length, ALIGN);
        if (aligned == data.Length)
            return Decrypt(data, key);

        var padded = new byte[aligned];
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        return Decrypt(padded, key);
    }

    public static bool IsValidIndex(byte[] data)
    {
        if (data.Length < 5) return false;

        var mountPointLength = BitConverter.ToInt32(data, 0);
        const int maxLength = 128;

        if (mountPointLength > maxLength || mountPointLength < -maxLength)
            return false;

        if (mountPointLength == 0)
            return data[4] == 0;

        if (mountPointLength < 0)
        {
            var offset = 4 + (-mountPointLength - 1) * 2;
            if (offset + 2 > data.Length) return false;
            return BitConverter.ToInt16(data, offset) == 0;
        }

        var utf8Offset = 4 + mountPointLength - 1;
        if (utf8Offset + 1 > data.Length) return false;
        return data[utf8Offset] == 0;
    }
}
