using BaGetter.Web.Helper;
using Xunit;

namespace BaGetter.Web.Tests.Helper;

public class UiThemeFacts
{
    public class Resolve
    {
        [Theory]
        [InlineData("light", "light")]
        [InlineData("dark", "dark")]
        [InlineData("flatly", "flatly")]
        [InlineData("Darkly", "darkly")]
        [InlineData(" yeti ", "yeti")]
        public void ReturnsKnownTheme(string value, string expectedId)
        {
            Assert.Equal(expectedId, UiTheme.Resolve(value)?.Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("solar")]
        [InlineData("../bootstrap")]
        [InlineData("flatly/../../x")]
        public void ReturnsNullForUnknownValue(string value)
        {
            Assert.Null(UiTheme.Resolve(value));
        }
    }

    public class StylesheetPath
    {
        [Fact]
        public void UsesBootstrapForBaGetterThemes()
        {
            Assert.Equal("lib/bootstrap/dist/css/bootstrap.min.css", UiTheme.Resolve("light").StylesheetPath);
            Assert.Equal("lib/bootstrap/dist/css/bootstrap.min.css", UiTheme.Resolve("dark").StylesheetPath);
        }

        [Fact]
        public void UsesBootswatchFolderForBootswatchThemes()
        {
            Assert.Equal("lib/bootswatch/slate/bootstrap.min.css", UiTheme.Resolve("slate").StylesheetPath);
        }
    }
}
