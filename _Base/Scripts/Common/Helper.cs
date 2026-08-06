using System.Globalization;
using System.Collections.Generic;

public static class Helper
{
    public static void AddUnique<T>(List<T> list, T item)
    {
        if (!list.Contains(item))
        {
            list.Add(item);
        }
    }

    public static string ConvertNumericPrefix(float value)
    {
        float absValue = value < 0f ? -value : value;
        string sign = value < 0f ? "-" : "";

        if (absValue >= 1000000f)
        {
            return sign + (absValue / 1000000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (absValue >= 1000f)
        {
            return sign + (absValue / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static readonly string[] LevelStringsCache = new string[201];
    private static readonly string[] SpeedStringsCache = new string[1001];

    static Helper()
    {
        for (int i = 0; i < LevelStringsCache.Length; i++)
        {
            LevelStringsCache[i] = $"LEVEL {i}";
        }

        for (int i = 0; i < SpeedStringsCache.Length; i++)
        {
            SpeedStringsCache[i] = $"Speed: {i}";
        }
    }

    public static string GetCachedLevelString(int level)
    {
        if (level >= 0 && level < LevelStringsCache.Length)
        {
            return LevelStringsCache[level];
        }

        return $"LEVEL {level}";
    }

    public static string GetCachedSpeedString(int speed)
    {
        if (speed >= 0 && speed < SpeedStringsCache.Length)
        {
            return SpeedStringsCache[speed];
        }

        return $"Speed: {speed}";
    }
}
