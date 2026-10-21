using UnityEngine;
using UnityEditor;

internal static class OscSenderSelfTest
{
    [MenuItem("Tools/OSC/Self-Test Packet Encoding")]
    private static void Run()
    {
        // "/a" + int 1 -> "/a\0\0" + ",i\0\0" + 00 00 00 01
        byte[] expected =
        {
            (byte)'/', (byte)'a', 0, 0,
            (byte)',', (byte)'i', 0, 0,
            0, 0, 0, 1,
        };

        byte[] actual = OscSender.BuildPacket("/a", new object[] { 1 });

        bool pass = actual.Length == expected.Length;
        if (pass)
        {
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i]) { pass = false; break; }
        }

        if (pass)
            Debug.Log("[OscSenderSelfTest] PASS - packet encoding matches hand-computed bytes.");
        else
            Debug.LogError($"[OscSenderSelfTest] FAIL - expected [{string.Join(",", expected)}] got [{string.Join(",", actual)}]");
    }
}
