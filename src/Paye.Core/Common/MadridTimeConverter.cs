using System.Globalization;

namespace Paye.Core.Common;

public static class MadridTimeConverter
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");
    private static readonly TimeZoneInfo MadridTimeZone = ResolveMadridTimeZone();

    public static TimeZoneInfo TimeZone => MadridTimeZone;

    public static CultureInfo Culture => SpanishCulture;

    public static DateTimeOffset ToMadridTime(DateTimeOffset utcDateTime)
    {
        var utc = utcDateTime.ToUniversalTime();
        return TimeZoneInfo.ConvertTime(utc, MadridTimeZone);
    }

    public static DateTimeOffset ToUtcFromMadrid(DateTime localMadridDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localMadridDateTime, DateTimeKind.Unspecified);
        var offset = MadridTimeZone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    public static string FormatSpanishDateTime(DateTimeOffset utcDateTime)
    {
        var local = ToMadridTime(utcDateTime);
        return local.ToString("dd/MM/yyyy HH:mm", SpanishCulture);
    }

    public static string FormatSpanishDate(DateTimeOffset utcDateTime)
    {
        var local = ToMadridTime(utcDateTime);
        return local.ToString("dd/MM/yyyy", SpanishCulture);
    }

    private static TimeZoneInfo ResolveMadridTimeZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Madrid", out var ianaTz))
        {
            return ianaTz;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById("Romance Standard Time", out var windowsTz))
        {
            return windowsTz;
        }

        return TimeZoneInfo.Utc;
    }
}
