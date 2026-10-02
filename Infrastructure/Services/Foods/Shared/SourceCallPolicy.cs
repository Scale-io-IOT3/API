using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Foods.Shared;

internal sealed record SourceCallPolicy(SourceSettings Settings, int TimeoutMs)
{
    internal static SourceCallPolicy From(
        IConfiguration configuration, string name, bool enabled, int timeoutMs, string? fallback = null) =>
        new(SourceSettingsResolver.Build(configuration, name, enabled, fallback),
            SourceSettingsResolver.ReadTimeoutMs(configuration, name, fallback, timeoutMs));
}
