using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Microsoft.Extensions.ObjectPool;
using RedisNaruto.Internal.Interfaces;
using RedisNaruto.Internal.Models;
using RedisNaruto.Internal.Sentinels;

namespace RedisNaruto.Internal;

internal sealed class RedisClientPool : IRedisClientPool
{
    /// <summary>
    ///空闲的客户端
    /// </summary>
    private readonly ConcurrentQueue<IRedisClient> _freeClients = new();

    /// <summary>
    /// 空闲数
    /// </summary>
    private int _freeCount = 0;

    /// <summary>
    /// 最大创建数
    /// </summary>
    private readonly int _maxCount;

    // /// <summary>
    // /// 等待创建事件 300 ms
    // /// </summary>
    // private readonly int WaitTime = 300;

    private readonly IRedisClientFactory _redisClientFactory;

    /// <summary>
    /// 
    /// </summary>
    public RedisClientPool(ConnectionModel connectionModel)
    {
        _maxCount = connectionModel.ConnectionPoolCount;
        _redisClientFactory = new RedisClientFactory(connectionModel);
        //todo 思考如何实现将空闲的客户端释放
    }

    public async Task<IRedisClient> RentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        //从队列中获取
        if (_freeClients.TryDequeue(out var redisClient))
        {
            //池内 空闲数减少
            Interlocked.Decrement(ref _freeCount);
            //重置连接
            await redisClient.ResetAsync(cancellationToken);
        }
        else
        {
            redisClient = await _redisClientFactory.GetAsync(async (client) => { await this.ReturnAsync(client); },
                cancellationToken);
        }

        return redisClient;
    }

    /// <summary>
    /// 归还
    /// </summary>
    /// <param name="redisClient"></param>
    /// <returns></returns>
    public ValueTask ReturnAsync([NotNull] IRedisClient redisClient)
    {
        //递增当前池中的数量 验证 是否小于等于 最大的数量
        if (Interlocked.Increment(ref _freeCount) <= _maxCount)
        {
            //入队
            _freeClients.Enqueue(redisClient);
            return new ValueTask();
        }

        //多余的就释放资源
        redisClient.Close();
        Interlocked.Decrement(ref _freeCount);
        return new ValueTask();
    }


    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool isDispose)
    {
        if (isDispose)
        {
        }
    }

    #endregion
}