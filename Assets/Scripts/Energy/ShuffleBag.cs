using System.Collections.Generic;
using UnityEngine;

// Fisher-Yates shuffle, extracted as a pure static method so it can be
// self-tested without needing a live MonoBehaviour/scene.
public static class ShuffleBag
{
    public static List<T> Shuffled<T>(IReadOnlyList<T> items)
    {
        var list = new List<T>(items);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
