using System;
using System.Collections.Generic;
using System.IO;

namespace TraducaoRealtime.Configuration;

internal static class EnvironmentLoader
{
    public static void LoadDotEnvIfAvailable()
    {
        var path = FindEnvFilePath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = trimmed[(separatorIndex + 1)..].Trim();
            value = Unquote(value);

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindEnvFilePath()
    {
        foreach (var baseDirectory in GetSearchRoots())
        {
            var current = new DirectoryInfo(baseDirectory);
            for (var depth = 0; depth < 8 && current is not null; depth++)
            {
                var candidate = Path.Combine(current.FullName, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var currentDirectory = Directory.GetCurrentDirectory();
        if (seen.Add(currentDirectory))
        {
            yield return currentDirectory;
        }

        var appBaseDirectory = AppContext.BaseDirectory;
        if (seen.Add(appBaseDirectory))
        {
            yield return appBaseDirectory;
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                return value[1..^1];
            }

            if (value.StartsWith('\'') && value.EndsWith('\''))
            {
                return value[1..^1];
            }
        }

        return value;
    }
}