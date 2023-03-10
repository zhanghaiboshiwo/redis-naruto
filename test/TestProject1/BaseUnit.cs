namespace TestProject1;

public class BaseUnit
{
    protected virtual async Task<IRedisCommand> GetRedisAsync()
    {
        var redisCommand = await RedisConnection.ConnectionAsync(new ConnectionModel
        {
            Connection = new string[]
            {
                "127.0.0.1:55000"
            },
            UserName = null,
            Password = "redispw",
        });
        return redisCommand;
    }
}