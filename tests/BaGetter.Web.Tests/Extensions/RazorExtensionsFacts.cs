using System.Globalization;
using BaGetter.Web.Extensions;
using Xunit;

namespace BaGetter.Web.Tests.Extensions;

public class RazorExtensionsFacts
{
    public class ToMetric
    {
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        [InlineData("de-DE")]
        public void RendersSameOutputInEveryCulture(string culture)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                CultureInfo.CurrentUICulture = new CultureInfo(culture);

                Assert.Equal("1.234k", 1234L.ToMetric());
                Assert.Equal(culture, CultureInfo.CurrentCulture.Name);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }
    }
}
