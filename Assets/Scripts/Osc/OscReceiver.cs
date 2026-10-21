using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Minimal OSC 1.0 UDP receiver - the mirror image of OscSender's packet
// building. A background thread listens; parsed messages are queued and
// dispatched on the main thread from Update() so subscribers can safely
// touch Unity objects.
public class OscReceiver : MonoBehaviour
{
    public event Action<string, object[]> OnMessage;

    private UdpClient client;
    private readonly ConcurrentQueue<(string address, object[] args)> queue = new ConcurrentQueue<(string, object[])>();

    public void StartListening(int port)
    {
        StopListening();
        client = new UdpClient(port);
        client.BeginReceive(OnReceive, null);
    }

    public void StopListening()
    {
        client?.Close();
        client = null;
    }

    private void OnReceive(IAsyncResult ar)
    {
        if (client == null) return;
        try
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.EndReceive(ar, ref remote);
            queue.Enqueue(ParsePacket(data));
        }
        catch (ObjectDisposedException)
        {
            return; // StopListening() was called - don't re-arm.
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OscReceiver] Parse error: {e.Message}");
        }

        client?.BeginReceive(OnReceive, null);
    }

    private void Update()
    {
        while (queue.TryDequeue(out var msg))
            OnMessage?.Invoke(msg.address, msg.args);
    }

    private void OnDestroy() => StopListening();

    public static (string address, object[] args) ParsePacket(byte[] data)
    {
        int offset = 0;
        string address = ReadOscString(data, ref offset);
        string typeTags = ReadOscString(data, ref offset);

        var args = new object[Mathf.Max(0, typeTags.Length - 1)];
        for (int i = 1; i < typeTags.Length; i++)
        {
            switch (typeTags[i])
            {
                case 'i': args[i - 1] = ReadInt(data, ref offset); break;
                case 'f': args[i - 1] = ReadFloat(data, ref offset); break;
                case 's': args[i - 1] = ReadOscString(data, ref offset); break;
                case 'T': args[i - 1] = true; break;
                case 'F': args[i - 1] = false; break;
            }
        }
        return (address, args);
    }

    private static string ReadOscString(byte[] data, ref int offset)
    {
        int start = offset;
        while (data[offset] != 0) offset++;
        string s = Encoding.UTF8.GetString(data, start, offset - start);
        int len = offset - start;
        offset = start + (len / 4 + 1) * 4; // skip null terminator + padding
        return s;
    }

    private static int ReadInt(byte[] data, ref int offset)
    {
        var bytes = new byte[4];
        Array.Copy(data, offset, bytes, 0, 4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        offset += 4;
        return BitConverter.ToInt32(bytes, 0);
    }

    private static float ReadFloat(byte[] data, ref int offset)
    {
        var bytes = new byte[4];
        Array.Copy(data, offset, bytes, 0, 4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        offset += 4;
        return BitConverter.ToSingle(bytes, 0);
    }
}
