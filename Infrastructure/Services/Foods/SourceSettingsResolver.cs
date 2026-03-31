using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Foods;

internal static class SourceSettingsResolver
{
    public static SourceSettings Build(
        IConfiguration configuration,
        string sourceName,
        bool defaultEnabled,
        string? groupName = null
    )
    {
        var groupEnabled = groupName is null ? defaultEnabled : IsEnabled(configuration, groupName, defaultEnabled);
        var enabled = IsEnabled(configuration, sourceName, groupEnabled);
        var failureThreshold = ReadInt(configuration, sourceName, groupName, "FailureThreshold", 2);
        var cooldownSeconds = ReadInt(configuration, sourceName, groupName, "CooldownSeconds", 120);

        return new SourceSettings(
            sourceName,
            enabled,
            Math.Max(1, failureThreshold),
            TimeSpan.FromSeconds(Math.Max(5, cooldownSeconds))
        );
    }

    private static bool IsEnabled(IConfiguration configuration, string sourceName, bool defaultValue)
    {
        var raw = configuration[$"Sources:{sourceName}:Enabled"];
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static int ReadInt(
        IConfiguration configuration,
        string sourceName,
        string? groupName,
        string setting,
        int defaultValue
    )
    {
        var sourceRaw = configuration[$"Sources:{sourceName}:{setting}"];
        if (int.TryParse(sourceRaw, out var sourceValue))
        {
            return sourceValue;
        }

        if (!string.IsNullOrWhiteSpace(groupName))
        {
            var groupRaw = configuration[$"Sources:{groupName}:{setting}"];
            if (int.TryParse(groupRaw, out var groupValue))
            {
                return groupValue;
            }
        }

        var globalRaw = configuration[$"Sources:Global:{setting}"];
        return int.TryParse(globalRaw, out var globalValue) ? globalValue : defaultValue;
    }
}
