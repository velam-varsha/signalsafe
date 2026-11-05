using System.Text.Json;
using System.Text.Json.Serialization;

using SignalSafe.Core.Domain;

namespace SignalSafe.Core.Layouts;

/// <summary>Reads a layout from the SignalSafe JSON format.</summary>
public static class LayoutLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Layout FromJson(string json)
    {
        LayoutDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<LayoutDto>(json, Options)
                ?? throw new LayoutFormatException("The layout file is empty.");
        }
        catch (JsonException ex)
        {
            throw new LayoutFormatException($"The layout file is not valid JSON: {ex.Message}", ex);
        }

        return new Layout(
            dto.Name ?? "Unnamed layout",
            (dto.Sections ?? []).Select(s => new TrackSection(s.Id ?? "", s.EndA ?? "", s.EndB ?? "", s.LengthM)).ToList(),
            (dto.Points ?? []).Select(p => new Points(p.Id ?? "", p.Toe ?? "", p.Normal ?? "", p.Reverse ?? "")).ToList(),
            (dto.Signals ?? []).Select(s => new Signal(s.Id ?? "", s.Type, s.AtNode ?? "", s.Protects ?? "")).ToList());
    }

    public static Layout FromFile(string path) => FromJson(File.ReadAllText(path));

    private sealed record LayoutDto(string? Name, List<SectionDto>? Sections, List<PointsDto>? Points, List<SignalDto>? Signals);
    private sealed record SectionDto(string? Id, string? EndA, string? EndB, double LengthM);
    private sealed record PointsDto(string? Id, string? Toe, string? Normal, string? Reverse);
    private sealed record SignalDto(string? Id, SignalType Type, string? AtNode, string? Protects);
}

public sealed class LayoutFormatException : Exception
{
    public LayoutFormatException(string message) : base(message) { }
    public LayoutFormatException(string message, Exception inner) : base(message, inner) { }
}
