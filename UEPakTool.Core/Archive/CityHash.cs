// Copyright (c) 2011 Google, Inc.
// Copyright (c) 2014 Atvaark
// CityHash C# Port, by Atvaark — adapted for UEPakTool

using System;
using System.Text;

namespace UEPakTool.Core.Archive;

public static class CityHash
{
    private const ulong K0 = 0xc3a5c85c97cb3127;
    private const ulong K1 = 0xb492b66fbe98f273;
    private const ulong K2 = 0x9ae16a3b2f90404f;

    public static ulong CityHash64(byte[] s)
    {
        int len = s.Length;
        if (len <= 16)
            return HashLen0To16(s, 0);
        if (len <= 32)
            return HashLen17To32(s);
        if (len <= 64)
            return HashLen33To64(s);

        ulong x = Fetch64(s, len - 40);
        ulong y = Fetch64(s, len - 16) + Fetch64(s, len - 56);
        ulong z = HashLen16(Fetch64(s, len - 48) + (ulong)len, Fetch64(s, len - 24));
        UInt128 v = WeakHashLen32WithSeeds(s, len - 64, (ulong)len, z);
        UInt128 w = WeakHashLen32WithSeeds(s, len - 32, y + K1, x);
        x = x * K1 + Fetch64(s, 0);

        len = (s.Length - 1) & ~63;
        int offset = 0;
        do
        {
            x = Rotate(x + y + v.Low + Fetch64(s, offset + 8), 37) * K1;
            y = Rotate(y + v.High + Fetch64(s, offset + 48), 42) * K1;
            x ^= w.High;
            y += v.Low + Fetch64(s, offset + 40);
            z = Rotate(z + w.Low, 33) * K1;
            v = WeakHashLen32WithSeeds(s, offset, v.High * K1, x + w.Low);
            w = WeakHashLen32WithSeeds(s, offset + 32, z + w.High, y + Fetch64(s, offset + 16));
            (x, z) = (z, x);
            offset += 64;
            len -= 64;
        } while (len != 0);
        return HashLen16(HashLen16(v.Low, w.Low) + ShiftMix(y) * K1 + z,
                         HashLen16(v.High, w.High) + x);
    }

    /// <summary>
    /// 计算 UE4 风格的 StrHash：CityHash64(UTF-16) 的 32 位归约。
    /// 算法来自 UE4 TypeHash.h — GetTypeHash(const FString&amp;)
    /// </summary>
    public static uint Ue4StrHash(string str)
    {
        if (str.Length == 0)
            return 0;

        byte[] utf16 = Encoding.Unicode.GetBytes(str);
        ulong h = CityHash64(utf16);
        return (uint)h + (uint)(h >> 32) * 23;
    }

    private static ulong Fetch64(byte[] p, int index)
    {
        return BitConverter.ToUInt64(p, index);
    }

    private static ulong Fetch64(byte[] p, uint index)
    {
        return BitConverter.ToUInt64(p, (int)index);
    }

    private static uint Fetch32(byte[] p, int index)
    {
        return BitConverter.ToUInt32(p, index);
    }

    private static uint Fetch32(byte[] p, uint index)
    {
        return BitConverter.ToUInt32(p, (int)index);
    }

    private static ulong Rotate(ulong val, int shift)
    {
        return shift == 0 ? val : ((val >> shift) | (val << (64 - shift)));
    }

    private static ulong ShiftMix(ulong val)
    {
        return val ^ (val >> 47);
    }

    private static ulong Hash128To64(UInt128 x)
    {
        const ulong kMul = 0x9ddfea08eb382d69;
        ulong a = (x.Low ^ x.High) * kMul;
        a ^= (a >> 47);
        ulong b = (x.High ^ a) * kMul;
        b ^= (b >> 47);
        b *= kMul;
        return b;
    }

    private static ulong HashLen16(ulong u, ulong v)
    {
        return Hash128To64(new UInt128(u, v));
    }

    private static ulong HashLen16(ulong u, ulong v, ulong mul)
    {
        ulong a = (u ^ v) * mul;
        a ^= (a >> 47);
        ulong b = (v ^ a) * mul;
        b ^= (b >> 47);
        b *= mul;
        return b;
    }

    private static ulong HashLen0To16(byte[] s, int offset)
    {
        int len = s.Length - offset;
        if (len >= 8)
        {
            ulong mul = K2 + (uint)len * 2;
            ulong a = Fetch64(s, offset) + K2;
            ulong b = Fetch64(s, offset + len - 8);
            ulong c = Rotate(b, 37) * mul + a;
            ulong d = (Rotate(a, 25) + b) * mul;
            return HashLen16(c, d, mul);
        }
        if (len >= 4)
        {
            ulong mul = K2 + (uint)len * 2;
            ulong a = Fetch32(s, offset);
            return HashLen16((uint)len + (a << 3), Fetch32(s, offset + len - 4), mul);
        }
        if (len > 0)
        {
            byte a = s[offset];
            byte b = s[offset + (len >> 1)];
            byte c = s[offset + (len - 1)];
            uint y = a + ((uint)b << 8);
            uint z = (uint)len + ((uint)c << 2);
            return ShiftMix(y * K2 ^ z * K0) * K2;
        }
        return K2;
    }

    private static ulong HashLen17To32(byte[] s)
    {
        uint len = (uint)s.Length;
        ulong mul = K2 + len * 2;
        ulong a = Fetch64(s, 0) * K1;
        ulong b = Fetch64(s, 8);
        ulong c = Fetch64(s, len - 8) * mul;
        ulong d = Fetch64(s, len - 16) * K2;
        return HashLen16(Rotate(a + b, 43) + Rotate(c, 30) + d,
                         a + Rotate(b + K2, 18) + c, mul);
    }

    private static UInt128 WeakHashLen32WithSeeds(
        ulong w, ulong x, ulong y, ulong z, ulong a, ulong b)
    {
        a += w;
        b = Rotate(b + a + z, 21);
        ulong c = a;
        a += x;
        a += y;
        b += Rotate(a, 44);
        return new UInt128(a + z, b + c);
    }

    private static UInt128 WeakHashLen32WithSeeds(byte[] s, int offset, ulong a, ulong b)
    {
        return WeakHashLen32WithSeeds(Fetch64(s, offset),
            Fetch64(s, offset + 8),
            Fetch64(s, offset + 16),
            Fetch64(s, offset + 24),
            a, b);
    }

    private static ulong HashLen33To64(byte[] s)
    {
        uint len = (uint)s.Length;
        ulong mul = K2 + len * 2;
        ulong a = Fetch64(s, 0) * K2;
        ulong b = Fetch64(s, 8);
        ulong c = Fetch64(s, len - 24);
        ulong d = Fetch64(s, len - 32);
        ulong e = Fetch64(s, 16) * K2;
        ulong f = Fetch64(s, 24) * 9;
        ulong g = Fetch64(s, len - 8);
        ulong h = Fetch64(s, len - 16) * mul;
        ulong u = Rotate(a + g, 43) + (Rotate(b, 30) + c) * 9;
        ulong v = ((a + g) ^ d) + f + 1;
        ulong w = ByteSwapUInt64((u + v) * mul) + h;
        ulong x = Rotate(e + f, 42) + c;
        ulong y = (ByteSwapUInt64((v + w) * mul) + g) * mul;
        ulong z = e + f + c;
        a = ByteSwapUInt64((x + z) * mul + y) + b;
        b = ShiftMix((z + a) * mul + d + h) * mul;
        return b + x;
    }

    private static ulong ByteSwapUInt64(ulong x)
    {
        return (x >> 56) |
               ((x & 0x00ff000000000000UL) >> 40) |
               ((x & 0x0000ff0000000000UL) >> 24) |
               ((x & 0x000000ff00000000UL) >> 8) |
               ((x & 0x00000000ff000000UL) << 8) |
               ((x & 0x0000000000ff0000UL) << 24) |
               ((x & 0x000000000000ff00UL) << 40) |
               (x << 56);
    }
}

public class UInt128
{
    public UInt128(ulong low, ulong high)
    {
        Low = low;
        High = high;
    }

    public ulong Low { get; set; }
    public ulong High { get; set; }
}
