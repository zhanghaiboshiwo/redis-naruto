using System.Buffers;
using System.Text;

namespace RedisNaruto.Utils;

/// <summary>
/// 编码
/// </summary>
internal static class EncodingUtil
{
    /// <summary>
    /// 编码
    /// </summary>
    /// <param name="obj"></param>
    public static byte[] ToEncode(this object obj)
    {
        return obj switch
        {
            byte[] bytes => bytes,
            string str => Encoding.Default.GetBytes(str),
            _ => Encoding.Default.GetBytes(obj + "")
        };
    }

    /// <summary>
    /// 编码 池化
    /// </summary>
    /// <param name="obj"></param>
    public static EncodePool ToEncodePool(this object obj)
    {
        switch (obj)
        {
            case byte[] bytes:
                return new EncodePool(bytes, bytes.Length, false);
            case string str:
            {
                var reusableBuffer = ArrayPool<byte>.Shared.Rent(str.Length * 3);

                var length = Encoding.UTF8.GetBytes(str, 0, str.Length, reusableBuffer, 0);

                return new EncodePool(reusableBuffer, length, true);
            }
        }

        var newStr = obj + "";
        var reusableBuffer2 = ArrayPool<byte>.Shared.Rent(newStr.Length * 3);

        var length2 = Encoding.UTF8.GetBytes(newStr, 0, newStr.Length, reusableBuffer2, 0);

        return new EncodePool(reusableBuffer2, length2, true);
    }
}

public sealed class EncodePool : IDisposable
{
    public byte[] Bytes { get; }

    /// <summary>
    /// 
    /// </summary>
    public int Length { get; }

    private bool IsPool { get; }

    internal EncodePool(byte[] bytes, int length, bool isPool)
    {
        Bytes = bytes;
        Length = length;
        IsPool = isPool;
    }

    public void Dispose()
    {
        if (IsPool)
        {
            ArrayPool<byte>.Shared.Return(Bytes);
        }

        GC.SuppressFinalize(this);
    }
}