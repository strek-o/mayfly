using System.Globalization;

namespace Mayfly.Web.Files;

public static class FileSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB"];

    public static string Format(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {Units[unit]}");
    }
}
