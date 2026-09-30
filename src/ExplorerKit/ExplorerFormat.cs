namespace SSP.ExplorerKit;

/// <summary>Display formatting for Explorer window information (German), as used by the window selection dialog.</summary>
public static class ExplorerFormat
{
    /// <summary>"dd.MM.yyyy HH:mm", or "–" if unknown.</summary>
    public static string OpenedAt(DateTimeOffset? openedAt) =>
        openedAt is { } value ? value.LocalDateTime.ToString("dd.MM.yyyy HH:mm") : "–";

    /// <summary>A duration such as <see cref="ExplorerWindow.Age"/>, e.g. "3 Min.", "2 Std. 05 Min.", "4 Tage 3 Std.", or "–" if unknown.</summary>
    public static string Age(TimeSpan? age)
    {
        if (age is not { } value)
        {
            return "–";
        }

        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value switch
        {
            { TotalMinutes: < 1 } => "< 1 Min.",
            { TotalHours: < 1 } => $"{value.Minutes} Min.",
            { TotalDays: < 1 } => $"{value.Hours} Std. {value.Minutes:00} Min.",
            _ => $"{value.Days} {(value.Days == 1 ? "Tag" : "Tage")} {value.Hours} Std.",
        };
    }
}
