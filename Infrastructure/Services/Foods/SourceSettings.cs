namespace Infrastructure.Services.Foods;

internal sealed record SourceSettings(string Name, bool Enabled, int FailureThreshold, TimeSpan Cooldown);
