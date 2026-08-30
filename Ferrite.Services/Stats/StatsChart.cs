// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text.Json;

namespace Ferrite.Services.Stats;

public readonly record struct StatsChartSeries(string Name, string Color,
    IReadOnlyList<long> Values);

public static class StatsChart
{
    public const string Line = "line";
    public const string Bar = "bar";
    public const string Area = "area";

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

    public static string ColorAt(int index)
    {
        string[] order = [Blue, Green, Red, Orange, LightBlue, LightGreen, Golden, Indigo];
        return order[index % order.Length];
    }

    public static string? Build(string type, IReadOnlyList<int> xs,
        IReadOnlyList<StatsChartSeries> series, bool dark, bool stacked = false)
    {
        if (xs.Count == 0)
        {
            return null;
        }

        List<StatsChartSeries> measurable = series
            .Where(item => item.Values.Any(value => value != 0))
            .ToList();
        if (measurable.Count == 0)
        {
            return null;
        }

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
            for (int i = 0; i < measurable.Count; i++)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(Column(i));
                foreach (long value in measurable[i].Values)
                {
                    writer.WriteNumberValue(value);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartObject("types");
            writer.WriteString("x", "x");
            for (int i = 0; i < measurable.Count; i++)
            {
                writer.WriteString(Column(i), type);
            }
            writer.WriteEndObject();

            writer.WriteStartObject("names");
            for (int i = 0; i < measurable.Count; i++)
            {
                writer.WriteString(Column(i), measurable[i].Name);
            }
            writer.WriteEndObject();

            writer.WriteStartObject("colors");
            for (int i = 0; i < measurable.Count; i++)
            {
                writer.WriteString(Column(i), Color(measurable[i].Color, dark));
            }
            writer.WriteEndObject();

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
