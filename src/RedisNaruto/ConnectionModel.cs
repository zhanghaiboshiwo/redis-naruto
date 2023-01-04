namespace RedisNaruto;

/// <summary>
/// 连接模型
/// </summary>
public sealed class ConnectionModel
{
    public ConnectionModel()
    {
        DataBase = 0;
        ConnectionPoolCount = Environment.ProcessorCount * 2;
    }
    /// <summary>
    /// 连接地址
    /// </summary>
    public string[] Connection { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// 数据存储库
    /// </summary>
    public int DataBase { get; set; }

    /// <summary>
    /// 连接池
    /// </summary>
    public int ConnectionPoolCount { get; set; }
}