namespace Infrastructure.Services.Foods;

/// <summary>Defines whether a source is enabled and when repeated failures trigger a cooldown.</summary>
internal sealed record SourceSettings(string Name, bool Enabled, int FailureThreshold, TimeSpan Cooldown);
