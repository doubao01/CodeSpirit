namespace CodeSpirit.Core.Mvvm;

public static class ToneAnalyzer
{
    public static string? ForStatus(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "available" or "active" or "ok" or "success" or "completed" or "approved" or "verified" or "ready" => "green",
            "overdue" or "error" or "failed" or "cancelled" or "suspended" or "rejected" or "expired" or "blocked" => "red",
            "reserved" or "pending" or "waiting" or "processing" or "review" or "attention" or "hold" or "flagged" or "warning" => "amber",
            "borrowed" or "archived" or "inactive" or "draft" or "info" or "scheduled" or "noted" or "seen" => "blue",
            _ => null
        };
    }

    public static string? ForNumeric(decimal value, decimal[]? siblings)
    {
        if (siblings is null || siblings.Length < 2)
            return value > 0 ? "green" : value < 0 ? "red" : "blue";

        var sorted = siblings.OrderBy(x => x).ToArray();
        var p70 = sorted[(int)(sorted.Length * 0.7)];
        var p30 = sorted[(int)(sorted.Length * 0.3)];

        if (value >= p70) return "green";
        if (value >= p30) return "amber";
        if (value < 0) return "red";
        return "blue";
    }

    public static string? ForDueDate(DateTime? dueDate)
    {
        if (dueDate is null) return null;

        var now = DateTime.UtcNow;
        var daysLeft = (dueDate.Value - now).TotalDays;

        if (daysLeft < 0) return "red";
        if (daysLeft <= 3) return "amber";
        if (daysLeft <= 14) return "blue";
        return "green";
    }
}
