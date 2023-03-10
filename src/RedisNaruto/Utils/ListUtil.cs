namespace RedisNaruto.Utils;

public static class ListUtil
{
    public static Dictionary<string, string> ToDic(this List<object> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Execute();

        Dictionary<string, string> Execute()
        {
            //下标
            var i = 1;
            //上一个值
            var preName = "";
            var res = new Dictionary<string, string>();
            foreach (var item in source)
            {
                var itemStr = item.ToString();
                //双数为值
                if (i % 2 == 0)
                {
                    res[preName] = itemStr;
                }
                //单数为key
                else
                {
                    preName = itemStr;
                    res[itemStr] = "";
                }

                i++;
            }

            return res;
        }
    }
}