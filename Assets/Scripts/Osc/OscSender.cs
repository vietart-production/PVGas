using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

// Minimal OSC 1.0 UDP sender - lets Unity talk to Resolume/TouchDesigner
// directly, no Python bridge process needed. Mirrors osc-bridge.py's logic:
// one cached UDP client per host:port, and the same i/f/s/T/F argument types.
public static class OscSender
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 2269;

    private static readonly Dictionary<string, UdpClient> clients = new Dictionary<string, UdpClient>();

    public static void Send(string address, params object[] args)
        => Send(DefaultHost, DefaultPort, address, args);

    public static void Send(string host, int port, string address, params object[] args)
    {
        try
        {
            var client = GetClient(host, port);
            byte[] packet = BuildPacket(address, args);
            client.Send(packet, packet.Length);
        }
        catch (SocketException e)
        {
            // Happens when nothing is listening on host:port (e.g. Resolume not
            // running yet) - UDP has no connection to lose, so just log and continue.
            UnityEngine.Debug.LogWarning($"[OscSender] Send to {host}:{port}{address} failed: {e.Message}");
        }
    }

    private static UdpClient GetClient(string host, int port)
    {
        string key = $"{host}:{port}";
        if (!clients.TryGetValue(key, out var client))
        {
            client = new UdpClient();
            client.Connect(host, port);
            clients[key] = client;
        }
        return client;
    }

    public static byte[] BuildPacket(string address, object[] args)
    {
        var typeTags = new StringBuilder(",");
        var argBytes = new List<byte>();

        foreach (var arg in args)
        {
            switch (arg)
            {
                case int i:
                    typeTags.Append('i');
                    argBytes.AddRange(ToBigEndian(i));
                    break;
                case float f:
                    typeTags.Append('f');
                    argBytes.AddRange(ToBigEndian(f));
                    break;
                case bool b:
                    // OSC true/false type tags carry no argument bytes.
                    typeTags.Append(b ? 'T' : 'F');
                    break;
                case string s:
                    typeTags.Append('s');
                    argBytes.AddRange(PadToFour(Encoding.UTF8.GetBytes(s)));
                    break;
                default:
                    throw new ArgumentException($"Unsupported OSC arg type: {arg?.GetType()}");
            }
        }

        var packet = new List<byte>();
        packet.AddRange(PadToFour(Encoding.UTF8.GetBytes(address)));
        packet.AddRange(PadToFour(Encoding.UTF8.GetBytes(typeTags.ToString())));
        packet.AddRange(argBytes);
        return packet.ToArray();
    }

    // Null-terminates then zero-pads to the next multiple of 4 bytes (OSC string rule).
    private static byte[] PadToFour(byte[] data)
    {
        int paddedLength = (data.Length / 4 + 1) * 4;
        var padded = new byte[paddedLength];
        Array.Copy(data, padded, data.Length);
        return padded;
    }

    private static byte[] ToBigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }

    private static byte[] ToBigEndian(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }
}
