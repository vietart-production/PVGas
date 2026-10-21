using System.Collections.Generic;
using UnityEngine;

// Forces every per-topic icon/beam tint into just the 3 PV Gas key-visual
// colors (replacing each topic's own distinct hue) - deterministic per
// topic so the same product always gets the same one of the 3. Each topic
// is hand-assigned to whichever of the 3 its own original color (see the
// EnergyTopicData assets under Assets/Data/EnergyTopics) actually reads
// closest to, instead of an arbitrary hash: the blue-ish ones (condensate,
// dry_gas_fertilizer, lng) -> Blue, the green/teal ones (green_hydro, lpg)
// -> Green, the warm/neutral ones (cng, dry_gas_power, steel_pipe) -> White.
public static class BrandPalette
{
    public static readonly Color White = Color.white;
    public static readonly Color Green = new Color(0f, 0.62f, 0.34f);
    public static readonly Color Blue = new Color(0.1f, 0.4f, 0.85f);

    private static readonly Dictionary<string, Color> ByTopic = new Dictionary<string, Color>
    {
        { "condensate", Blue },
        { "dry_gas_fertilizer", Blue },
        { "lng", Blue },
        { "green_hydro", Green },
        { "lpg", Green },
        { "cng", White },
        { "dry_gas_power", White },
        { "steel_pipe", White },
    };

    public static Color For(string topicId)
    {
        if (ByTopic.TryGetValue(topicId, out var c)) return c;
        // Fallback for any topic not in the curated list above.
        int h = Mathf.Abs(topicId.GetHashCode());
        return h % 3 == 0 ? Green : h % 3 == 1 ? Blue : White;
    }

    public static bool IsWhite(Color c) => c == White;
}
