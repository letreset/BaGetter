using System.ComponentModel.DataAnnotations;

namespace BaGetter.Core.Configuration;

public class DatabaseOptions
{
    public string Type { get; set; }

    [Required]
    public string ConnectionString { get; set; }

    /// <summary>
    /// MySQL only: the server version (e.g. <c>8.0.36-mysql</c>). When set, the version is not auto-detected
    /// from the server.
    /// </summary>
    public string ServerVersion { get; set; }
}
