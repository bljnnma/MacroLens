using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Providers;

/// <summary>
/// Minimal CSV reader for the SDMX providers.
///
/// Exists because BIS embeds commas inside quoted TITLE fields — a naive
/// <c>Split(',')</c> shifts every column after it and silently reads a
/// description where a value should be. Written once here rather than in each
/// client.
/// </summary>
public static class SdmxCsv
{
    public static List<string> SplitRow(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                // Doubled quote inside a quoted field is an escaped quote.
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }
                quoted = !quoted;
                continue;
            }

            if (c == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }

    /// <summary>
    /// Last day of the period a reading covers, given how often it is published.
    ///
    /// Providers stamp a period, never a publication instant, so the release time
    /// has to be approximated. The period END is the right approximation: a
    /// reading covering June cannot possibly have been published before 30 June,
    /// while the period START back-dates it by a full period.
    ///
    /// That back-dating is not cosmetic. Stamped at the start, a June monthly
    /// figure reads as 67 days old in early August and a Q2 figure as 128 days —
    /// which is exactly why NZD's CPI fell off the staleness cliff while being
    /// three weeks old, and why every unemployment reading would have arrived
    /// already expired.
    /// </summary>
    public static DateOnly PeriodEnd(DateOnly period, SyncCadence cadence) => cadence switch
    {
        SyncCadence.Quarterly => period.AddMonths(3).AddDays(-1),
        SyncCadence.Monthly => period.AddMonths(1).AddDays(-1),
        SyncCadence.Weekly => period.AddDays(6),
        // Daily readings already name their own day.
        _ => period
    };

    /// <summary>Header lookup that tolerates a BOM on the first column.</summary>
    public static int ColumnIndex(List<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
            if (header[i].Trim().Trim('﻿').Equals(name, StringComparison.Ordinal))
                return i;
        return -1;
    }

    /// <summary>
    /// SDMX time periods come in several shapes. Every one is mapped to the
    /// FIRST day of the period, matching how <c>SyncReleases</c> already treats
    /// a FRED period — the provider gives a period, not a publication instant,
    /// and inventing a day-of-month would be precision we do not have.
    /// </summary>
    public static DateOnly? ParsePeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period)) return null;
        period = period.Trim();

        // 2026-07
        if (period.Length == 7 && period[4] == '-'
            && int.TryParse(period[..4], out var ym) && int.TryParse(period[5..], out var m)
            && m is >= 1 and <= 12)
            return new DateOnly(ym, m, 1);

        // 2026-Q2
        if (period.Length == 7 && period[4] == '-' && (period[5] == 'Q' || period[5] == 'q')
            && int.TryParse(period[..4], out var yq) && int.TryParse(period[6..], out var q)
            && q is >= 1 and <= 4)
            return new DateOnly(yq, (q - 1) * 3 + 1, 1);

        // 2026-07-31
        if (DateOnly.TryParse(period, System.Globalization.CultureInfo.InvariantCulture, out var date))
            return date;

        // 2026
        if (period.Length == 4 && int.TryParse(period, out var year))
            return new DateOnly(year, 1, 1);

        return null;
    }
}
