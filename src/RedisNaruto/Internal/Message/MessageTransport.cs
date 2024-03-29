using System.Buffers;
using Microsoft.IO;
using RedisNaruto.Exceptions;
using RedisNaruto.Internal.Models;
using RedisNaruto.Internal.Serialization;
using RedisNaruto.Models;
using RedisNaruto.Utils;
using RedisNaruto.Internal.Enums;

namespace RedisNaruto.Internal.Message;

/// <summary>
/// 消息传输
/// </summary>
internal class MessageTransport : IMessageTransport
{
    /// <summary>
    /// 池
    /// </summary>
    protected static readonly RecyclableMemoryStreamManager MemoryStreamManager = new(new RecyclableMemoryStreamManager.Options()
    {
        BlockSize = 1024*10
    });

    /// <summary>
    /// 换行
    /// </summary>
    protected static readonly byte[] NewLine = "\r\n".ToEncode();

    protected static readonly byte CR = (byte) '\r';


    protected static readonly byte LF = (byte) '\n';

    /// <summary>
    /// 空
    /// </summary>
    protected static readonly byte[] Nil = $"{RespMessage.BulkStrings}0".ToEncode();

    /// <summary>
    /// 序列化
    /// </summary>
    protected static readonly ISerializer Serializer = new Serializer();

    #region 发送消息

    /// <summary>
    /// 发送消息
    ///使用MemoryStream 进行消息的缓冲再发送优点，一 是为了当数据过大进行分块处理，二 利于扩展，如果进行二次修改的话
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="command"></param>
    public virtual async Task SendWithMemoryAsync(Stream stream, Command command)
    {
        await using var ms = MemoryStreamManager.GetStream();
        ms.Position = 0;
        using (var encode1 = $"{RespMessage.ArrayString}{command.Length}".ToEncodePool())
        {
            await ms.WriteAsync(encode1.Bytes.AsMemory(0, encode1.Length));
        }

        await ms.WriteAsync(NewLine);
        //写入命令
        using (var encode1 = command.Cmd.ToEncodePool())
        {
            await ms.WriteAsync($"{RespMessage.BulkStrings}{encode1.Length}".ToEncode());
            await ms.WriteAsync(NewLine);
            await ms.WriteAsync(encode1.Bytes.AsMemory(0, encode1.Length));
        }

        await ms.WriteAsync(NewLine);
        if (command.Length > 1)
        {
            //判断参数长度
            foreach (var item in command.Args)
            {
                //处理null
                if (item is null)
                {
                    await ms.WriteAsync(Nil);
                    await ms.WriteAsync(NewLine);
                    await ms.WriteAsync(NewLine);
                    continue;
                }

                if (item is not byte[] argBytes)
                {
                    using (var encode = await Serializer.SerializeAsync(item))
                    {
                        await ms.WriteAsync($"{RespMessage.BulkStrings}{encode.Length}".ToEncode());
                        await ms.WriteAsync(NewLine);
                        await ms.WriteAsync(encode.Bytes.AsMemory(0, encode.Length));
                    }

                    await ms.WriteAsync(NewLine);
                    continue;
                }

                using (var encode1 = $"{RespMessage.BulkStrings}{argBytes.Length}".ToEncodePool())
                {
                    await ms.WriteAsync(encode1.Bytes.AsMemory(0, encode1.Length));
                }

                await ms.WriteAsync(NewLine);
                await ms.WriteAsync(argBytes);
                await ms.WriteAsync(NewLine);
            }
        }

        ms.Position = 0;
        await ms.CopyToAsync(stream);
    }


    /// <summary>
    ///发送消息 直接往网络中写入
    ///优点 减少额外的内存分配
    ///直接使用NetworkStream 进行发送，原因一 如果消息过大，IP层会自动进行分块处理， 二 不需要二次进行消息的修改 。 综上所述 目前不太需要 使用  MemoryStream增加额外的一层 缓冲
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="command"></param>
    public virtual async Task SendAsync(Stream stream, Command command)
    {
        using (var encode1 = $"{RespMessage.ArrayString}{command.Length}".ToEncodePool())
        {
            await stream.WriteAsync(encode1.Bytes.AsMemory(0, encode1.Length));
        }

        await stream.WriteAsync(NewLine);
        //写入命令
        using (var encode1 = command.Cmd.ToEncodePool())
        {
            await stream.WriteAsync($"{RespMessage.BulkStrings}{encode1.Length}".ToEncode());
            await stream.WriteAsync(NewLine);
            await stream.WriteAsync(encode1.Bytes.AsMemory(0, encode1.Length));
        }

        await stream.WriteAsync(NewLine);
        if (command.Length > 1)
        {
            //判断参数长度
            foreach (var item in command.Args)
            {
                //处理null
                if (item is null)
                {
                    await stream.WriteAsync(Nil);
                    await stream.WriteAsync(NewLine);
                    await stream.WriteAsync(NewLine);
                    continue;
                }

                if (item is not byte[] argBytes)
                {
                    using (var encode = await Serializer.SerializeAsync(item))
                    {
                        await stream.WriteAsync($"{RespMessage.BulkStrings}{encode.Length}".ToEncode());
                        await stream.WriteAsync(NewLine);
                        await stream.WriteAsync(encode.Bytes.AsMemory(0, encode.Length));
                    }

                    await stream.WriteAsync(NewLine);
                    continue;
                }

                using (var encode1 = $"{RespMessage.BulkStrings}{argBytes.Length}".ToEncodePool())
                {
                    await stream.WriteAsync(encode1.Bytes.AsMemory(0, encode1.Length));
                }

                await stream.WriteAsync(NewLine);
                await stream.WriteAsync(argBytes);
                await stream.WriteAsync(NewLine);
            }
        }
    }

    #endregion

    /// <summary>
    /// 转换消息
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public async Task<object> ReceiveMessageAsync(Stream stream)
    {
        return await ReceiveMessageCoreAsync(stream, true);
    }

    /// <summary>
    /// 转换消息
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="isThrowException">是否抛出错误</param>
    /// <returns></returns>
    private async Task<object> ReceiveMessageCoreAsync(Stream stream, bool isThrowException)
    {
        //获取首位的 符号 判断消息回复类型
        var head = ReadFirstChar(stream);
        return head switch
        {
            RespMessage.SimpleString => ReadLine(stream, RespMessageTypeEnum.SimpleString),
            RespMessage.Number => ReadLine(stream, RespMessageTypeEnum.Number),
            //数组
            RespMessage.ArrayString => await ReadMLineAsync(stream, ReadLine(stream, RespMessageTypeEnum.ArrayString)),
            RespMessage.BulkStrings => await ReadBulkStringsAsync(stream),
            RespMessage.Maps => await ReadMapsAsync(stream),
            RespMessage.Nulls => ReadNulls(stream),
            RespMessage.Double => ReadDouble(stream),
            RespMessage.BigNumber => ReadBigNumber(stream),
            RespMessage.Bool => ReadBool(stream),
            RespMessage.Sets => await ReadMLineAsync(stream, ReadLine(stream, RespMessageTypeEnum.Sets)),
            RespMessage.Pushs => await ReadMLineAsync(stream, ReadLine(stream, RespMessageTypeEnum.Pushs)),
            RespMessage.BuckError => isThrowException
                ? throw new RedisExecException(await ReadBulkErrorAsync(stream))
                : await ReadBulkErrorAsync(stream),
            //错误
            _ => isThrowException
                ? throw new RedisExecException(ReadLine(stream, RespMessageTypeEnum.Error).ToString())
                : ReadLine(stream, RespMessageTypeEnum.Error)
        };
    }

    /// <summary>
    /// 读取简易消息
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="RedisExecException"></exception>
    public async Task<RedisValue> ReceiveSimpleMessageAsync(Stream stream)
    {
        return (RedisValue) await ReceiveMessageAsync(stream);
    }

    /// <summary>
    /// 多行读取
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    private async Task<List<object>> ReadMLineAsync(Stream stream, int length)
    {
        //读取数组的长度
        if (length == -1)
        {
            return default;
        }

        List<object> resultList = new();
        for (var i = 0; i < length; i++)
        {
            resultList.Add(await ReceiveMessageCoreAsync(stream, false));
        }

        return resultList;
    }

    #region RESP2

    private async Task<RedisValue> ReadBulkStringsAsync(Stream stream)
    {
        int offset = ReadLine(stream, RespMessageTypeEnum.Number);
        //如果为null
        if (offset == -1)
        {
            return RedisValue.Null();
        }

        var result = await ReadBlobLineAsync(stream, offset);

        return new RedisValue(result, RespMessageTypeEnum.BulkStrings);
    }

    #endregion

    #region RESP3

    /// <summary>
    /// 读取key value 键值对结构
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private async Task<Dictionary<string, object>> ReadMapsAsync(Stream stream)
    {
        //读取长度
        var length = ReadLine(stream, RespMessageTypeEnum.Number);
        var maps = new Dictionary<string, object>(length);
        for (var i = 0; i < length; i++)
        {
            var key = await ReceiveSimpleMessageAsync(stream);
            var value = await ReceiveMessageAsync(stream);
            maps.Add(key, value);
        }

        return maps;
    }

    /// <summary>
    /// 读取双精度
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private RedisValue ReadDouble(Stream stream)
    {
        return ReadLine(stream, RespMessageTypeEnum.Double);
    }

    /// <summary>
    /// 读取大数
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private RedisValue ReadBigNumber(Stream stream)
    {
        return ReadLine(stream, RespMessageTypeEnum.BigNumber);
    }

    /// <summary>
    /// 读取bool
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private RedisValue ReadBool(Stream stream)
    {
        return ReadLine(stream, RespMessageTypeEnum.Bool);
    }

    /// <summary>
    /// 读取空
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private RedisValue ReadNulls(Stream stream)
    {
        _ = ReadLine(stream, RespMessageTypeEnum.Nulls);
        return new RedisValue(null, RespMessageTypeEnum.Nulls);
    }

    /// <summary>
    /// 读取错误
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private async Task<RedisValue> ReadBulkErrorAsync(Stream stream)
    {
        int dd = ReadLine(stream, RespMessageTypeEnum.Number);
        var error = await ReadBlobLineAsync(stream, dd);
        return new RedisValue(error, RespMessageTypeEnum.BuckError);
    }

    #endregion

    /// <summary>
    /// 读取指定长度数据
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="length">长度</param>
    /// <returns></returns>
    private static async Task<ReadOnlyMemory<byte>> ReadBlobLineAsync(Stream stream, int length)
    {
        //判断是否为空字符串
        if (length == 0)
        {
            //读取换行信息
            await ReadCrlfAsync(stream);
            return ReadOnlyMemory<byte>.Empty;
        }

        //从内存池中租借
        await using var ms = MemoryStreamManager.GetStream();
        ms.Position = 0;
        var totalLength = 0;
        while (true)
        {
            using (var memoryOwner = MemoryPool<byte>.Shared.Rent(length))
            {
                var mem = await stream.ReadAsync(memoryOwner.Memory[..(length - totalLength)]);
                await ms.WriteAsync(memoryOwner.Memory[..mem]);
                totalLength += mem;
                //读取 
                //这里用大于等于 是因为访问其它情况导致 陷入死循环
                if (totalLength >= length)
                {
                    break;
                }
            }
        }

        //读取换行信息
        await ReadCrlfAsync(stream);
        //获取真实的数据
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }

    /// <summary>
    /// 读取换行
    /// </summary>
    /// <param name="stream"></param>
    private static async Task ReadCrlfAsync(Stream stream)
    {
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(2);
        _ = await stream.ReadAsync(memoryOwner.Memory[..2]);
    }

    /// <summary>
    /// 读取第一行的字节信息
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private static char ReadFirstChar(Stream stream)
    {
        var es = stream.ReadByte();
        //如果返回的-1 网络存在问题
        if (es == -1)
        {
            throw new IOException();
        }

        return (char) es;
    }

    /// <summary>
    /// 读取行数据
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private static RedisValue ReadLine(Stream stream, RespMessageTypeEnum respMessageType)
    {
        //从内存池中租借
        using var ms = MemoryStreamManager.GetStream();
        ms.Position = 0;
        while (true)
        {
            var msg = stream.ReadByte();
            if (msg < 0) break;
            //判断是否为换行 \r\n
            if (msg == CR)
            {
                var msg2 = stream.ReadByte();
                if (msg2 < 0) break;
                if (msg2 == LF) break;
                ms.WriteByte((byte) msg);
                ms.WriteByte((byte) msg2);
            }
            else
                ms.WriteByte((byte) msg);
        }

        return new RedisValue(ms.ToArray(), respMessageType);
    }
}