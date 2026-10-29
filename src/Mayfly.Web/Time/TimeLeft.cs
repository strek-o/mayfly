namespace Mayfly.Web.Time;

public static class TimeLeft
{
    public static string Format(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        var left = expiresAt - now;

        var minutes = Minutes(left);
        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        var hours = Round(left.TotalHours);
        return hours < 24 ? $"{hours} h" : $"{Round(left.TotalDays)} d";
    }

    public static bool IsSoon(DateTimeOffset? expiresAt, DateTimeOffset now) =>
        expiresAt is { } value && Minutes(value - now) < 60;

    private static int Minutes(TimeSpan left) => Math.Max(1, Round(left.TotalMinutes));

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
