// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text.Json;

namespace Ferrite.Services.Stats;

/// <summary>
/// One named series of a chart. The values are index-paired with the chart's own
/// x column, so a series always has exactly as many points as the chart does.
/// </summary>
public readonly record struct StatsChartSeries(string Name, string Color,
    IReadOnlyList<long> Values);

/// <summary>
/// Renders the chart JSON that `statsGraph.json:DataJSON` carries.
///
/// This is the ONE place JSON is written inside Ferrite, and it is allowed
/// because `dataJSON` is a JSON-DEFINED PROTOCOL BOUNDARY: the field's whole
/// content is a JSON document the client parses itself. Nothing here is stored;
/// the durable side of statistics is ordinary TL.
///
/// The document shape is the one every Telegram client's chart renderer reads:
/// a `columns` array whose first column is the `x` axis in MILLISECONDS since the
/// epoch and whose remaining columns are the series, plus `types`, `names` and
/// `colors` keyed by the same column names.
///
/// A chart with NO DATA still declares its columns and just carries no points.
/// That is deliberate: a period Ferrite has no rows for answers a well-formed
/// empty graph rather than a fabricated one, and the client renders an empty
/// chart instead of failing to parse.
/// </summary>
public static class StatsChart
{
    public const string Line = "line";
    public const string Bar = "bar";
    public const string Area = "area";

    // The documented `colorkey#rrggbb` form, which lets a client substitute its
    // own theme colour for the key and fall back to the literal value. The `dark`
    // flag of the stats request picks which literal travels.
    public const string Blue = "blue";
    public const string Green = "green";
    public const string Red = "red";
    public const string Orange = "orange";
    public const string LightBlue = "lightblue";
    public const string LightGreen = "lightgreen";
    public const string Golden = "golden";
    public const string Indigo = "indigo";

    private static readonly IReadOnlyDictionary<string, (string Light, string Dark)>
        Palette = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [Blue] = ("#327fe5", "#6ab7ff"),
            [Green] = ("#4bcd5e", "#62cd66"),
            [Red] = ("#e05356", "#ff5b56"),
            [Orange] = ("#eba52d", "#f5bd5c"),
            [LightBlue] = ("#58a8ed", "#7dc4ff"),
            [LightGreen] = ("#9ed448", "#b1de70"),
            [Golden] = ("#d5ba3c", "#e8d15a"),
            [Indigo] = ("#7f79f3", "#9d94ff"),
        };

    /// <summary>
    /// The ordered colour keys a multi-series chart assigns to its series, so two
    /// adjacent series never share a colour.
    /// </summary>
    public static string ColorAt(int index)
    {
        string[] order = [Blue, Green, Red, Orange, LightBlue, LightGreen, Golden, Indigo];
        return order[index % order.Length];
    }

    /// <summary>
    /// A chart over a shared x axis. <paramref name="xs"/> are UNIX SECONDS and
    /// are converted to the milliseconds the client's renderer expects.
    /// </summary>
    public static string Build(string type, IReadOnlyList<int> xs,
        IReadOnlyList<StatsChartSeries> series, bool dark, bool stacked = false)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            writer.WriteStartArray("columns");
            writer.WriteStartArray();
            writer.WriteStringValue("x");
            foreach (int x in xs)
            {
                writer.WriteNumberValue(x * 1000L);
            }
            writer.WriteEndArray();
            for (int i = 0; i < series.Count; i++)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(Column(i));
                foreach (long value in series[i].Values)
                {
                    writer.WriteNumberValue(value);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartObject("types");
            writer.WriteString("x", "x");
            for (int i = 0; i < series.Count; i++)
            {
                writer.WriteString(Column(i), type);
            }
            writer.WriteEndObject();

            writer.WriteStartObject("names");
            for (int i = 0; i < series.Count; i++)
            {
                writer.WriteString(Column(i), series[i].Name);
            }
            writer.WriteEndObject();

            writer.WriteStartObject("colors");
            for (int i = 0; i < series.Count; i++)
            {
                writer.WriteString(Column(i), Color(series[i].Color, dark));
            }
            writer.WriteEndObject();

            // "The `bar` chart type and `stacked` option are always used together."
            if (stacked)
            {
                writer.WriteBoolean("stacked", true);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string Column(int index) => $"y{index}";

    private static string Color(string key, bool dark) =>
        Palette.TryGetValue(key, out (string Light, string Dark) value)
            ? $"{key}#{(dark ? value.Dark : value.Light).TrimStart('#')}"
            : key;
}
