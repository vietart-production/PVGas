using System.Collections.Generic;
using UnityEngine;

// Forces every per-topic icon/beam tint into just the 3 PV Gas key-visual
// colors (replacing each topic's own distinct hue) - deterministic per
// topic so the same product always gets the same one of the 3. Rebalanced
// to a 4 Green / 2 Blue / 2 White split per request: condensate and
// dry_gas_fertilizer -> Blue; green_hydro, lpg, lng and steel_pipe ->
// Green; cng and dry_gas_power -> White.
public static class BrandPalette
{
    // Not literal (1,1,1) - pure white is always at peak luma regardless of
    // intensity, which read noticeably brighter than Green/Blue (both have
    // at least one low channel). Dimmed to roughly match their luma.
    public static readonly Color White = new Color(0.4f, 0.4f, 0.4f);
    public static readonly Color Green = new Color(0f, 1f, 0.34f);
    // Base hex 384084, with G pinned to 0.7 (not the hex's own 64/255) to
    // keep the same brightness compensation as the other 2 colors. R/B
    // scaled to ~70% to deepen/darken the tone per request.
    public static readonly Color Blue = new Color(0x38 / 255f * 0.7f, 0.7f, 0x84 / 255f * 0.7f);

    private static readonly Dictionary<string, Color> ByTopic = new Dictionary<string, Color>
    {
        { "condensate", Blue },
        { "dry_gas_fertilizer", Blue },
        { "lng", Green },
        { "green_hydro", Green },
        { "lpg", Green },
        { "steel_pipe", Green },
        { "cng", White },
        { "dry_gas_power", White },
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
