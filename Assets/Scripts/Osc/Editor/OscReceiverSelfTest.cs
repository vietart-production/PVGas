using UnityEngine;
using UnityEditor;

internal static class OscReceiverSelfTest
{
    [MenuItem("Tools/OSC/Self-Test Round Trip")]
    private static void Run()
    {
        bool pass = true;
        pass &= CheckTopic();
        pass &= CheckHome();
        pass &= CheckFloat();

        if (pass)
            Debug.Log("[OscReceiverSelfTest] PASS - encode/decode round trip matches for all cases.");
    }

    private static bool CheckTopic()
    {
        byte[] packet = OscSender.BuildPacket("/pvgas/topic", new object[] { "products" });
        var (address, args) = OscReceiver.ParsePacket(packet);
        bool ok = address == "/pvgas/topic" && args.Length == 1 && (string)args[0] == "products";
        if (!ok) Debug.LogError($"[OscReceiverSelfTest] FAIL topic: address='{address}' args=[{string.Join(",", args)}]");
        return ok;
    }

    private static bool CheckHome()
    {
        byte[] packet = OscSender.BuildPacket("/pvgas/home", new object[0]);
        var (address, args) = OscReceiver.ParsePacket(packet);
        bool ok = address == "/pvgas/home" && args.Length == 0;
        if (!ok) Debug.LogError($"[OscReceiverSelfTest] FAIL home: address='{address}' argCount={args.Length}");
        return ok;
    }

    private static bool CheckFloat()
    {
        byte[] packet = OscSender.BuildPacket("/composition/layers/2/video/opacity", new object[] { 0.73f });
        var (address, args) = OscReceiver.ParsePacket(packet);
        bool ok = args.Length == 1 && Mathf.Approximately((float)args[0], 0.73f);
        if (!ok) Debug.LogError($"[OscReceiverSelfTest] FAIL float: args=[{string.Join(",", args)}]");
        return ok;
    }
}
