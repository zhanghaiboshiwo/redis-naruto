using System.Runtime.CompilerServices;
using RedisNaruto.Internal;
using RedisNaruto.Internal.Models;
using RedisNaruto.Utils;

namespace RedisNaruto.RedisCommands;

/// <summary>
/// hash操作
/// </summary>
public partial class RedisCommand : IRedisCommand
{
    /// <summary>
    /// hash删除
    /// </summary>
    /// <param name="key"></param>
    /// <param name="fields"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<long> HDelAsync(string key, string[] fields, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);
        return
            await client.ExecuteAsync<long>(new Command(RedisCommandName.HDel,
                new object[] {key}.Union(fields).ToArray()));
    }

    /// <summary>
    /// hash存储
    /// </summary>
    /// <param name="key"></param>
    /// <param name="fields"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<long> HSetAsync(string key, Dictionary<string, object> fields,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);
        var argv = new object[fields.Count * 2 + 1];
        argv[0] = key;

        var idx = 1;
        foreach (var (itemKey, value) in fields)
        {
            argv[idx * 2 - 1] = itemKey;
            argv[idx * 2] = value;
            idx++;
        }

        return
            await client.ExecuteAsync<long>(new Command(RedisCommandName.HSet,
                argv));
    }

    /// <summary>
    /// hash存在
    /// </summary>
    /// <param name="key"></param>
    /// <param name="field"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> HExistsAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);
        return
            await client.ExecuteAsync<int>(new Command(RedisCommandName.HExists,
                new object[]
                {
                    key, field
                })) == 1;
    }

    /// <summary>
    /// hash获取
    /// </summary>
    /// <param name="key"></param>
    /// <param name="field"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> HGetAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);
        return
            await client.ExecuteAsync<string>(new Command(RedisCommandName.HGet,
                new object[]
                {
                    key, field
                }));
    }

    /// <summary>
    /// 获取所有的hash数据
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Dictionary<string, string>> HGetAllAsync(string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);
        return await
            client.ExecuteMoreResultAsync<string>(new Command(RedisCommandName.HGetAll,
                new object[]
                {
                    key
                })).ToDicAsync();
    }

    /// <summary>
    /// hash 递增
    /// </summary>
    /// <param name="key"></param>
    /// <param name="field"></param>
    /// <param name="increment">递增的值</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<double> HIncrByAsync(string key, string field, double increment = 1,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);
        return
            await client.ExecuteAsync<double>(new Command(RedisCommandName.HIncrBy,
                new object[]
                {
                    key, field, increment
                }));
    }

    /// <summary>
    /// hash 的field 信息
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<string>> HKeysAsync(string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteMoreResultAsync<string>(new Command(RedisCommandName.HKeys,
            new object[]
            {
                key
            })).ToListAsync();
    }

    /// <summary>
    /// hash 长度
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<long> HLenAsync(string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteAsync<long>(new Command(RedisCommandName.HLen,
            new object[]
            {
                key
            }));
    }

    /// <summary>
    /// 批量获取
    /// </summary>
    /// <param name="key"></param>
    /// <param name="fields"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<string>> HMGetAsync(string key, string[] fields,
        CancellationToken cancellationToken = default)
    {
        if (key.IsNullOrWhiteSpace() || fields is not {Length: > 0})
        {
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteMoreResultAsync<string>(new Command(RedisCommandName.HMGet,
            new object[] {key}.Union(fields).ToArray())).ToListAsync();
    }

    /// <summary>
    /// 随机获取hash数据
    /// </summary>
    /// <param name="key"></param>
    /// <param name="count">数量</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Dictionary<string, string>> HRandFieldWithValueAsync(string key, int count = 1,
        CancellationToken cancellationToken = default)
    {
        if (key.IsNullOrWhiteSpace())
        {
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteMoreResultAsync<string>(new Command(RedisCommandName.HRandField,
            new object[]
            {
                key,
                count,
                "WITHVALUES"
            })).ToDicAsync();
    }

    /// <summary>
    /// 随机获取hash数据
    /// </summary>
    /// <param name="key"></param>
    /// <param name="count">数量</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<string>> HRandFieldAsync(string key, int count = 1,
        CancellationToken cancellationToken = default)
    {
        if (key.IsNullOrWhiteSpace())
        {
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteMoreResultAsync<string>(new Command(RedisCommandName.HRandField,
            new object[]
            {
                key,
                count
            })).ToListAsync();
    }

    /// <summary>
    /// 获取hash的值信息
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<string>> HValsAsync(string key,
        CancellationToken cancellationToken = default)
    {
        if (key.IsNullOrWhiteSpace())
        {
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteMoreResultAsync<string>(new Command(RedisCommandName.HVals,
            new object[]
            {
                key,
            })).ToListAsync();
    }

    /// <summary>
    /// 获取hash字段对应值的长度
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public async Task<long> HStrLenAsync(string key, string field,
        CancellationToken cancellationToken = default)
    {
        if (key.IsNullOrWhiteSpace() || field.IsNullOrWhiteSpace())
        {
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteAsync<long>(new Command(RedisCommandName.HStrLen,
            new object[]
            {
                key,
                field
            }));
    }

    /// <summary>
    /// 设置hast的值 如果不存在的话 就添加
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value">具体的值</param>
    /// <param name="cancellationToken"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public async Task<bool> HSetNxAsync(string key, string field, object value,
        CancellationToken cancellationToken = default)
    {
        if (key.IsNullOrWhiteSpace() || field.IsNullOrWhiteSpace())
        {
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);

        return await client.ExecuteAsync<long>(new Command(RedisCommandName.HSetNx,
            new[]
            {
                key,
                field,
                value
            })) == 1;
    }

    /// <summary>
    /// 扫描hash的数据
    /// https://redis.io/commands/scan/
    /// </summary>
    /// <param name="key"></param>
    /// <param name="count">条数</param>
    /// <param name="cancellationToken"></param>
    /// <param name="matchPattern">匹配条件</param>
    /// <returns></returns>
    public async IAsyncEnumerable<Dictionary<string, string>> HScanAsync(string key,
        string matchPattern = "*", int count = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (key.IsNullOrWhiteSpace())
        {
            yield break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var client = await GetRedisClient(cancellationToken);
        //游标位置
        var cursor = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var resultList = await client.ExecuteAsync<object>(new Command(RedisCommandName.HScan,
                new object[]
                {
                    key,
                    cursor,
                    "MATCH",
                    matchPattern,
                    "COUNT",
                    count
                }));
            if (resultList is not List<object> {Count: >= 2} list)
            {
                yield break;
            }

            //更新游标
            cursor = list[0].ToString().ToInt();
            if (list[1] is List<object> datas)
            {
                yield return datas.ToDic();
            }
            else
            {
                yield break;
            }

            if (cursor == 0)
            {
                yield break;
            }
        }
    }
}