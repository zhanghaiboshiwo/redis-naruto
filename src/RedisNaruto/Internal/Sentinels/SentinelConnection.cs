using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.ObjectPool;
using RedisNaruto.EventDatas;
using RedisNaruto.Exceptions;
using RedisNaruto.Internal.DiagnosticListeners;
using RedisNaruto.Internal.Message;
using RedisNaruto.Internal.Models;
using RedisNaruto.Utils;

namespace RedisNaruto.Internal.Sentinels;

/// <summary>
/// 哨兵
/// </summary>
internal static class SentinelConnection
{
    /// <summary>
    /// 消息传输
    /// </summary>
    private static readonly IMessageTransport MessageTransport = new MessageTransport();


    private static readonly ObjectPool<TcpClient> ObjectPool;

    static SentinelConnection()
    {
        //初始化
        ObjectPool = new DefaultObjectPool<TcpClient>(new DefaultPooledObjectPolicy<TcpClient>()
        {
        });
    }

    /// <summary>
    /// 获取主节点地址
    /// </summary>
    /// <param name="masterName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RedisSentinelException"></exception>
    public static async Task<HostPort> GetMaserAddressAsync(string masterName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hostInfo = ConnectionStateManage.Get();
        //获取ip地址
        var ips = await Dns.GetHostAddressesAsync(hostInfo.hostPort.Host, cancellationToken);
        TcpClient sentinelTcpClient = null;
        try
        {
            sentinelTcpClient = ObjectPool.Get();
            if (sentinelTcpClient.Connected && !await sentinelTcpClient.IsVaildAsync())
            {
                var localSentinelTcpClient = sentinelTcpClient;
                localSentinelTcpClient.Dispose();
                sentinelTcpClient = new TcpClient();
                await sentinelTcpClient.ConnectAsync(ips, hostInfo.hostPort.Port,
                    cancellationToken);
            }

            if (!sentinelTcpClient.Connected)
            {
                await sentinelTcpClient.ConnectAsync(ips, hostInfo.hostPort.Port,
                    cancellationToken);
            }

            var argv = new object[]
            {
                "get-master-addr-by-name",
                masterName
            };
            //执行获取主节点地址
            await MessageTransport.SendAsync(sentinelTcpClient.GetStream(),
                new Command(RedisCommandName.Sentinel, argv));
            new SendSentinelMessageEventData(hostInfo.hostPort.Host, hostInfo.hostPort.Port, RedisCommandName.Sentinel,
                argv).SendSentinel();
            var result = await MessageTransport.ReceiveMessageAsync(sentinelTcpClient.GetStream());
            new ReceiveSentinelMessageEventData(hostInfo.hostPort.Host, hostInfo.hostPort.Port,
                RedisCommandName.Sentinel, argv, result).ReceiveSentinel();
            if (result is List<object> list)
            {
                return new HostPort(list[0].ToString(), list[1].ToString().ToInt());
            }
        }
        catch (Exception e)
        {
            new SentinelMessageErrorEventData(hostInfo.hostPort.Host,hostInfo.hostPort.Port,e).SentinelMessageError();
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            ObjectPool.Return(sentinelTcpClient!);
        }

        throw new RedisSentinelException("获取主节点异常");
    }

    /// <summary>
    /// 是否有效
    /// </summary>
    /// <param name="tcpClient"></param>
    /// <returns></returns>
    private static async Task<bool> IsVaildAsync(this TcpClient tcpClient)
    {
        //ping
        await MessageTransport.SendAsync(tcpClient.GetStream(), new Command(RedisCommandName.Ping, null));
        var result = await MessageTransport.ReceiveSimpleMessageAsync(tcpClient.GetStream());
        if (!result.IsEmpty() && !string.Equals(result.ToString(), "PONG", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}