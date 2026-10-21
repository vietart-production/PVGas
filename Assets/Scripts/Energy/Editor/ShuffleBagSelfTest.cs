using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

internal static class ShuffleBagSelfTest
{
    [MenuItem("Tools/SanEnergy/Self-Test Shuffle Bag")]
    private static void Run()
    {
        bool pass = true;
        var items = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

        for (int trial = 0; trial < 50; trial++)
        {
            var shuffled = ShuffleBag.Shuffled(items);
            var seen = new HashSet<int>(shuffled);
            if (shuffled.Count != 8 || seen.Count != 8)
            {
                Debug.LogError($"[ShuffleBagSelfTest] FAIL trial {trial}: count={shuffled.Count} uniqueCount={seen.Count}");
                pass = false;
            }
        }

        if (pass)
            Debug.Log("[ShuffleBagSelfTest] PASS - 50 shuffles, each produced exactly 8 unique items, no repeats/drops.");
    }
}
