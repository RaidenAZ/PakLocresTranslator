namespace UEPakTool.Core.Encryption;

public class FAesKey
{
    public byte[] Key { get; }
    public string KeyString { get; }

    public FAesKey(string keyString)
    {
        keyString = FixKey(keyString);
        if (keyString.Length != 66)
            throw new ArgumentException("AES密钥必须为32字节（带0x前缀的64个十六进制字符）");
        KeyString = keyString;
        Key = ParseHexBinary(keyString.AsSpan(2));
    }

    public FAesKey(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("AES密钥必须为32字节");
        Key = key;
        KeyString = "0x" + Convert.ToHexString(key);
    }

    public static string FixKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        var trimmed = key.AsSpan().Trim();
        Span<char> result = stackalloc char[trimmed.Length + 2];
        trimmed.ToUpperInvariant(result[2..]);

        if (result[2..].StartsWith("0X"))
            result = result[2..];
        else
            result[0] = '0';
        result[1] = 'x';

        return new string(result);
    }

    private static byte[] ParseHexBinary(ReadOnlySpan<char> hex)
    {
        if ((hex.Length & 1) != 0)
            throw new ArgumentException("十六进制字符串长度必须为偶数");

        var bytes = new byte[hex.Length >> 1];
        for (var i = 0; i < bytes.Length; i++)
        {
            var hi = hex[i << 1];
            var lo = hex[(i << 1) + 1];
            bytes[i] = (byte)((GetHexVal(hi) << 4) + GetHexVal(lo));
        }
        return bytes;
    }

    private static int GetHexVal(char c)
    {
        var v = (int)c;
        return v - (v < 58 ? 48 : (v < 97 ? 55 : 87));
    }

    public override string ToString() => KeyString;
}
