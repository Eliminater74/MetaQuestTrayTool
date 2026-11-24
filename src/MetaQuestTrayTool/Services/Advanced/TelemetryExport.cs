using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MetaQuestTrayTool.Services;

public static class TelemetryExport
{
    private const int MaxRows = 240;

    public static string ToCsv(IReadOnlyList<TelemetryMetric> metrics)
    {
        var builder = new StringBuilder();
        builder.AppendLine("name,unit,value,text,quality,availability,source,provider,timestamp,note");
        foreach (var metric in metrics.Take(MaxRows))
        {
            builder.Append(Csv(metric.Name)).Append(',')
                .Append(Csv(metric.Unit)).Append(',')
                .Append(metric.Value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(Csv(metric.Text)).Append(',')
                .Append(metric.Quality.ToString()).Append(',')
                .Append(metric.Availability.ToString()).Append(',')
                .Append(Csv(metric.Source)).Append(',')
                .Append(Csv(metric.Provider)).Append(',')
                .Append(metric.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(metric.Note))
                .AppendLine();
        }

        return builder.ToString();
    }

    public static string ToJson(IReadOnlyList<TelemetryMetric> metrics)
    {
        var rows = metrics.Take(MaxRows).Select(metric => new
        {
            metric.Name,
            metric.Unit,
            metric.Value,
            metric.Text,
            Quality = metric.Quality.ToString(),
            Availability = metric.Availability.ToString(),
            metric.Source,
            metric.Provider,
            metric.TimestampUtc,
            metric.Note
        });
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }
}
