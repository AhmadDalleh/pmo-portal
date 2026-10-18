using System.Reflection;

namespace Pmo.Infrastructure.Analytics;

/// <summary>Reads the embedded .sql files in this folder by file name (without folder).</summary>
public static class SqlResources
{
    public static string Read(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
