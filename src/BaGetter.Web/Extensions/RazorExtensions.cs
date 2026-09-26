using System.Globalization;
using Humanizer;

namespace BaGetter.Web.Extensions;

public static class RazorExtensions
{
    public static string ToMetric(this long value)
    {
        // Humanizer formats with the current culture and has no culture parameter, so switch to the
        // invariant culture to render the same text (e.g. "1.2k") regardless of the server culture.
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            return ((double) value).ToMetric();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>
    /// Formats a size with binary units and the invariant culture, e.g. "812 B", "2.43 MB".
    /// </summary>
    public static string ToFileSize(this long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];

        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return size.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[unit];
    }
}
