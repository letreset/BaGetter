using System;
using System.Collections.Generic;
using System.Linq;

namespace BaGetter.Web.Helper;

/// <summary>
/// A look for the web UI: BaGetter's own light and dark themes, or one of the Bootswatch 3 themes.
/// The choice is stored per browser in the <see cref="CookieName"/> cookie.
/// </summary>
public class UiTheme
{
    public const string CookieName = "bagetter-theme";

    public static readonly IReadOnlyList<UiTheme> All =
    [
        new("light", "BaGetter Light", isDark: false, isBootswatch: false),
        new("dark", "BaGetter Dark", isDark: true, isBootswatch: false),
        new("cerulean", "Cerulean", isDark: false, isBootswatch: true),
        new("cosmo", "Cosmo", isDark: false, isBootswatch: true),
        new("cyborg", "Cyborg", isDark: true, isBootswatch: true),
        new("darkly", "Darkly", isDark: true, isBootswatch: true),
        new("flatly", "Flatly", isDark: false, isBootswatch: true),
        new("journal", "Journal", isDark: false, isBootswatch: true),
        new("lumen", "Lumen", isDark: false, isBootswatch: true),
        new("paper", "Paper", isDark: false, isBootswatch: true),
        new("readable", "Readable", isDark: false, isBootswatch: true),
        new("sandstone", "Sandstone", isDark: false, isBootswatch: true),
        new("simplex", "Simplex", isDark: false, isBootswatch: true),
        new("slate", "Slate", isDark: true, isBootswatch: true),
        new("spacelab", "Spacelab", isDark: false, isBootswatch: true),
        new("superhero", "Superhero", isDark: true, isBootswatch: true),
        new("united", "United", isDark: false, isBootswatch: true),
        new("yeti", "Yeti", isDark: false, isBootswatch: true),
    ];

    private UiTheme(string id, string name, bool isDark, bool isBootswatch)
    {
        Id = id;
        Name = name;
        IsDark = isDark;
        IsBootswatch = isBootswatch;
    }

    /// <summary>The value of the <c>data-theme</c> attribute and the cookie.</summary>
    public string Id { get; }

    public string Name { get; }

    public bool IsDark { get; }

    public bool IsBootswatch { get; }

    /// <summary>The Bootstrap stylesheet for this theme, relative to the web root of BaGetter.Web.</summary>
    public string StylesheetPath => IsBootswatch
        ? $"lib/bootswatch/{Id}/bootstrap.min.css"
        : "lib/bootstrap/dist/css/bootstrap.min.css";

    /// <summary>
    /// Returns the theme with the given id, or <c>null</c> when the value is missing or unknown,
    /// so a tampered cookie never reaches a stylesheet path.
    /// </summary>
    public static UiTheme Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        return All.FirstOrDefault(t => string.Equals(t.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
