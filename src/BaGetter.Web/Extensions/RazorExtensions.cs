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
}
