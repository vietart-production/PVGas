using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Nguồn "người đang được lidar track" thuần OSC, tự nghe UDP + tự parse OSC binary
/// (không phụ thuộc thư viện ngoài). Theo đúng protocol/logic track-matching của
/// TDReceiver tham khảo (/person/count, /person/x, /person/y theo đúng thứ tự),
/// nhưng KHÔNG spawn prefab — chỉ expose ActivePoints để các consumer khác (ripple
/// effect, click UI...) tự quyết định làm gì với từng điểm.
/// </summary>
public class OscTouchSource : MonoBehaviour
{
    [Header("OSC")]
    public int port = 12000;

    [Header("Space — RectTransform của vùng được lidar track (ví dụ Canvas)")]
    public RectTransform spaceRect;

    public enum CornerOrigin { BottomLeft, TopLeft, TopRight, BottomRight }
    [Header("Court Origin")]
    public CornerOrigin courtOrigin = CornerOrigin.TopLeft;

    [Header("Tracking")]
    public float trackMatchRadius = 1.5f;
    [Header("Timeout")]
    public float signalTimeout = 0.3f;

    [Header("Bound Visual")]
    [Tooltip("Bấm phím L lúc Play để tắt/mở toàn bộ visual debug (bound line, sphere marker, OnGUI).")]
    public bool showVisual = true;
    public Color boundColor = Color.red;
    public Color originColor = Color.yellow;
    [Range(0.01f, 0.2f)] public float lineWidth = 0.05f;

    [Header("Person Marker (sphere thật, hiện trong Game view/build)")]
    public Color markerColor = Color.yellow;
    [Range(0.005f, 0.1f)] public float markerSizeFraction = 0.03f;

    [Header("Live Data (Read-only)")]
    public int personCount;
    public Vector2[] persons = new Vector2[0];

    public struct TouchPoint { public int id; public Vector3 world; }

    readonly List<TouchPoint> _activePoints = new();
    public IReadOnlyList<TouchPoint> ActivePoints => _activePoints;

    /// World-space radius of the visible yellow marker — consumers (OscUiClickSource)
    /// use this so "chạm được" matches what the marker actually looks like on screen.
    public float MarkerWorldRadius =>
        (_cornersValid ? Vector3.Distance(_corners[0], _corners[1]) * markerSizeFraction : 0.1f) * 0.5f;

    // Debug-only read accessors (no reflection allowed in sandboxed RunCommand scripts)
    public bool DebugConnected => _connected;
    public float DebugLastMessageAge => _lastMsgTime > 0 ? Time.time - _lastMsgTime : -1f;
    public int DebugPendingPackets => _incoming.Count;
    public int DebugTrackCount => _tracks.Count;

    // ── Internal: OSC receive ──────────────────────────────────────────────────
    UdpClient _udp;
    Thread _thread;
    volatile bool _running;
    readonly ConcurrentQueue<byte[]> _incoming = new();
    bool _connected;
    float _lastMsgTime = -1f;

    // ── Internal: person/track state ───────────────────────────────────────────
    float[] _bufX = new float[32];
    float[] _bufY = new float[32];
    int _xIdx, _yIdx;

    class PersonTrack
    {
        public int id;
        public Vector2 uv;
        public Vector3 worldPos;
        public float lastSeenTime;
    }

    readonly List<PersonTrack> _tracks = new();
    int _nextTrackId = 0;
    readonly HashSet<int> _claimedTrackIds = new();

    // ── Internal: corner cache ─────────────────────────────────────────────────
    readonly Vector3[] _rectCorners = new Vector3[4];
    readonly Vector3[] _corners = new Vector3[4];
    bool _cornersValid;

    // ── Internal: debug visuals (Editor-only — not needed/not rendered in builds) ──
    readonly Queue<string> _recentData = new(6);
    readonly Queue<string> _logs = new(8);
#if UNITY_EDITOR
    LineRenderer _boundLine;
    LineRenderer _originMarker;
    readonly List<GameObject> _personMarkers = new();
    Material _markerMaterial;
#endif

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void OnEnable() => Application.logMessageReceived += OnLog;

    void OnDisable()
    {
        Application.logMessageReceived -= OnLog;
        StopReceiving();
    }

    void Start()
    {
        UpdateCornerCache();
#if UNITY_EDITOR
        SetupLineRenderers();
#endif
        StartReceiving();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (WasToggleKeyPressedThisFrame()) showVisual = !showVisual;
#endif

        UpdateCornerCache();
#if UNITY_EDITOR
        if (showVisual) UpdateBoundVisual(); else HideBoundVisual();
#endif

        while (_incoming.TryDequeue(out byte[] packet))
            MiniOsc.ParsePacket(packet, 0, packet.Length, HandleOscMessage);

        for (int i = _tracks.Count - 1; i >= 0; i--)
            if (Time.time - _tracks[i].lastSeenTime > signalTimeout)
                _tracks.RemoveAt(i);
        personCount = _tracks.Count;

        _activePoints.Clear();
        foreach (var tr in _tracks)
            _activePoints.Add(new TouchPoint { id = tr.id, world = tr.worldPos });

#if UNITY_EDITOR
        UpdatePersonMarkers();
#endif
    }

    static bool WasToggleKeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.lKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.L);
#endif
    }

    // ── Person marker (sphere thật, chỉ dựng trong Editor để debug — không cần/không
    // tốn chi phí trong build thật, interaction vẫn chạy đầy đủ qua ActivePoints) ────
#if UNITY_EDITOR
    void UpdatePersonMarkers()
    {
        if (!showVisual)
        {
            foreach (var m in _personMarkers) m.SetActive(false);
            return;
        }

        float size = _cornersValid
            ? Vector3.Distance(_corners[0], _corners[1]) * markerSizeFraction
            : 0.1f;

        for (int i = 0; i < _activePoints.Count; i++)
        {
            GameObject marker = i < _personMarkers.Count ? _personMarkers[i] : CreatePersonMarker();
            marker.SetActive(true);
            marker.transform.position = _activePoints[i].world;
            marker.transform.localScale = Vector3.one * size;
        }
        for (int i = _activePoints.Count; i < _personMarkers.Count; i++)
            _personMarkers[i].SetActive(false);
    }

    GameObject CreatePersonMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "OscTouchSource_PersonMarker";
        go.transform.SetParent(transform);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        if (_markerMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");
            _markerMaterial = new Material(shader) { color = markerColor };
        }
        go.GetComponent<Renderer>().sharedMaterial = _markerMaterial;

        _personMarkers.Add(go);
        return go;
    }
#endif

    // ── OSC receive (background thread) ──────────────────────────────────────────

    void StartReceiving()
    {
        try
        {
            _udp = new UdpClient(port);
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true };
            _thread.Start();
            _connected = true;
            Debug.Log($"[OscTouchSource] Bind port {port} OK");
        }
        catch (Exception e)
        {
            _connected = false;
            Debug.LogError($"[OscTouchSource] Bind port {port} FAILED: {e.Message}");
        }
    }

    void StopReceiving()
    {
        _running = false;
        try { _udp?.Close(); } catch { }
        _thread?.Join(200);
        _udp = null;
    }

    void ReceiveLoop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref remote);
                _incoming.Enqueue(data);
            }
            catch (SocketException)
            {
                break; // socket closed (Stop) or real error — either way, exit loop
            }
            catch (Exception e)
            {
                if (_running) Debug.LogError($"[OscTouchSource] Receive exception: {e.Message}");
            }
        }
    }

    // ── OSC message handling ─────────────────────────────────────────────────────

    void HandleOscMessage(string address, List<object> args)
    {
        _lastMsgTime = Time.time;
        switch (address)
        {
            case "/person/count":
                int newCount = args.Count > 0 ? Convert.ToInt32(args[0]) : 0;
                _xIdx = _yIdx = 0;
                if (persons.Length != newCount) persons = new Vector2[newCount];
                _claimedTrackIds.Clear();
                break;
            case "/person/x":
                if (_xIdx < _bufX.Length && args.Count > 0) _bufX[_xIdx++] = Convert.ToSingle(args[0]);
                break;
            case "/person/y":
                if (_yIdx < _bufY.Length && args.Count > 0)
                {
                    _bufY[_yIdx] = Convert.ToSingle(args[0]);
                    if (_yIdx < persons.Length)
                    {
                        var uv = new Vector2(_bufX[_yIdx], _bufY[_yIdx]);
                        if (uv.x != 0f || uv.y != 0f)
                        {
                            persons[_yIdx] = uv;
                            MatchOrCreateTrack(uv);
                            string entry = $"[{Time.time:F1}s] p[{_yIdx}] uv=({uv.x:F3},{uv.y:F3}) world={NormalizedToWorld(uv)}";
                            if (_recentData.Count >= 5) _recentData.Dequeue();
                            _recentData.Enqueue(entry);
                        }
                    }
                    _yIdx++;
                }
                break;
        }
    }

    // ── Log ───────────────────────────────────────────────────────────────────────

    void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        string prefix = type == LogType.Exception ? "<EXC> " : "<ERR> ";
        string firstLine = "";
        if (!string.IsNullOrEmpty(stackTrace))
            foreach (var l in stackTrace.Split('\n'))
                if (!string.IsNullOrEmpty(l.Trim())) { firstLine = l.Trim(); break; }
        string line = $"[{Time.time:F1}s] {prefix}{condition}";
        if (_logs.Count >= 7) _logs.Dequeue(); _logs.Enqueue(line);
        if (!string.IsNullOrEmpty(firstLine))
        { if (_logs.Count >= 7) _logs.Dequeue(); _logs.Enqueue($"  → {firstLine}"); }
    }

    // ── Corner cache ──────────────────────────────────────────────────────────────

    void UpdateCornerCache()
    {
        _cornersValid = spaceRect != null;
        if (!_cornersValid) return;

        spaceRect.GetWorldCorners(_rectCorners);
        Vector3 wBL = _rectCorners[0];
        Vector3 wTL = _rectCorners[1];
        Vector3 wTR = _rectCorners[2];
        Vector3 wBR = _rectCorners[3];

        switch (courtOrigin)
        {
            case CornerOrigin.BottomLeft: _corners[0] = wBL; _corners[1] = wBR; _corners[2] = wTR; _corners[3] = wTL; break;
            case CornerOrigin.TopLeft: _corners[0] = wTL; _corners[1] = wTR; _corners[2] = wBR; _corners[3] = wBL; break;
            case CornerOrigin.TopRight: _corners[0] = wTR; _corners[1] = wTL; _corners[2] = wBL; _corners[3] = wBR; break;
            case CornerOrigin.BottomRight: _corners[0] = wBR; _corners[1] = wBL; _corners[2] = wTL; _corners[3] = wTR; break;
        }
    }

    public Vector3 NormalizedToWorld(Vector2 uv)
    {
        if (!_cornersValid) return new Vector3(uv.x, uv.y, 0f);
        Vector3 uEdge = Vector3.Lerp(_corners[0], _corners[1], uv.x);
        Vector3 vEdge = Vector3.Lerp(_corners[3], _corners[2], uv.x);
        return Vector3.Lerp(uEdge, vEdge, uv.y);
    }

    public Vector3 CourtOriginWorld => _cornersValid ? _corners[0] : Vector3.zero;

    // ── Track matching ────────────────────────────────────────────────────────────

    void MatchOrCreateTrack(Vector2 uv)
    {
        Vector3 world = NormalizedToWorld(uv);
        PersonTrack best = null;
        float bestD2 = trackMatchRadius * trackMatchRadius;
        foreach (var tr in _tracks)
        {
            if (_claimedTrackIds.Contains(tr.id)) continue;
            float d2 = (tr.worldPos - world).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = tr; }
        }
        if (best == null)
        {
            best = new PersonTrack { id = _nextTrackId++ };
            _tracks.Add(best);
        }

        best.uv = uv;
        best.worldPos = world;
        best.lastSeenTime = Time.time;
        _claimedTrackIds.Add(best.id);
    }

    // ── Line Renderers (debug, Editor-only) ────────────────────────────────────────
#if UNITY_EDITOR
    void SetupLineRenderers()
    {
        _boundLine = CreateLR("OscTouchSource_Bound", 4, true, boundColor, lineWidth);
        _originMarker = CreateLR("OscTouchSource_Origin", 6, false, originColor, lineWidth * 2f);
    }

    LineRenderer CreateLR(string goName, int pts, bool loop, Color color, float width)
    {
        var go = new GameObject(goName); go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true; lr.loop = loop; lr.positionCount = pts;
        lr.startWidth = width; lr.endWidth = width;
        var shader = Shader.Find("Hidden/Internal-Colored")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("UI/Default");
        if (shader != null) { lr.material = new Material(shader); lr.material.color = color; }
        else { lr.startColor = color; lr.endColor = color; }
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void UpdateBoundVisual()
    {
        if (!_cornersValid) { HideBoundVisual(); return; }
        float lift = 0.02f;
        if (_boundLine != null)
        {
            _boundLine.enabled = true;
            _boundLine.startWidth = _boundLine.endWidth = lineWidth;
            for (int i = 0; i < 4; i++)
                _boundLine.SetPosition(i, _corners[i] + Vector3.up * lift);
        }
        if (_originMarker != null)
        {
            _originMarker.enabled = true;
            _originMarker.startWidth = _originMarker.endWidth = lineWidth * 2f;
            float cs = Mathf.Min(
                Vector3.Distance(_corners[0], _corners[1]),
                Vector3.Distance(_corners[0], _corners[3])) * 0.15f;
            Vector3 o = _corners[0] + Vector3.up * (lift + 0.005f);
            Vector3 dirU = (_corners[1] - _corners[0]).normalized * cs;
            Vector3 dirV = (_corners[3] - _corners[0]).normalized * cs;
            _originMarker.SetPosition(0, o - dirU * 0.3f); _originMarker.SetPosition(1, o + dirU);
            _originMarker.SetPosition(2, o + dirU); _originMarker.SetPosition(3, o);
            _originMarker.SetPosition(4, o - dirV * 0.3f); _originMarker.SetPosition(5, o + dirV);
        }
    }

    void HideBoundVisual()
    {
        if (_boundLine != null) _boundLine.enabled = false;
        if (_originMarker != null) _originMarker.enabled = false;
    }
#endif

    // ── OnGUI debug panel (Editor-only) ─────────────────────────────────────────────

    // Window can be a thin 8:1 strip (e.g. 1536x192), far shorter than this panel's full
    // content needs at normal size — so the whole block is scaled down to fit instead of
    // trimming any information out of it.
#if UNITY_EDITOR
    void OnGUI()
    {
        if (!showVisual) return;
        // KlakSpout's SpoutSender blits into its own buffer every frame and never restores
        // RenderTexture.active afterward — without this, OnGUI silently draws into that
        // leftover offscreen target instead of the real screen.
        RenderTexture.active = null;

        float neededHeight = 24 + 20 + 26 + 22 + (_cornersValid ? (20 * 4 + 26) : 26) +
                              22 + persons.Length * 18 + 8 +
                              22 + _tracks.Count * 18 + 8 +
                              22 + _recentData.Count * 18 + 8 +
                              22 + _logs.Count * 18;
        float scale = Mathf.Min(1f, (Screen.height - 4f) / Mathf.Max(neededHeight, 1f));
        var prevMatrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

        var h = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        var b = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        float x = 10f, y = 10f;

        h.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 500, 24), "── [OscTouchSource] ──", h); y += 24;

        bool receiving = _lastMsgTime > 0f && (Time.time - _lastMsgTime) < 2f;
        b.normal.textColor = _connected ? Color.green : Color.red;
        GUI.Label(new Rect(x, y, 400, 22), $"Port {port}: {(_connected ? "bound" : "FAILED")}", b); y += 20;
        b.normal.textColor = receiving ? Color.green : Color.yellow;
        GUI.Label(new Rect(x, y, 600, 22),
            $"Receiving: {(receiving ? "YES" : "waiting...")} last={(_lastMsgTime > 0 ? $"{Time.time - _lastMsgTime:F1}s ago" : "never")}", b); y += 26;

        h.normal.textColor = boundColor;
        GUI.Label(new Rect(x, y, 500, 22), $"── Space (RectTransform) origin={courtOrigin} ──", h); y += 22;
        if (_cornersValid)
        {
            b.normal.textColor = originColor;
            GUI.Label(new Rect(x, y, 700, 22), $"(0,0) = {_corners[0]:F2}", b); y += 20;
            b.normal.textColor = Color.cyan;
            GUI.Label(new Rect(x, y, 700, 22), $"(1,0) = {_corners[1]:F2}", b); y += 20;
            GUI.Label(new Rect(x, y, 700, 22), $"(0,1) = {_corners[3]:F2}", b); y += 20;
            GUI.Label(new Rect(x, y, 700, 22), $"(1,1) = {_corners[2]:F2}", b); y += 20;
            b.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, 700, 22),
                $"Size: {Vector3.Distance(_corners[0], _corners[1]):F2} × {Vector3.Distance(_corners[0], _corners[3]):F2}", b); y += 26;
        }
        else
        {
            b.normal.textColor = Color.gray;
            GUI.Label(new Rect(x, y, 400, 22), "Chưa gán spaceRect (RectTransform)", b); y += 26;
        }

        h.normal.textColor = Color.green;
        GUI.Label(new Rect(x, y, 500, 22), $"── Persons: {persons.Length} ──", h); y += 22;
        b.normal.textColor = Color.white;
        for (int i = 0; i < persons.Length; i++)
        {
            GUI.Label(new Rect(x, y, 750, 20),
                $"  [{i}] uv=({persons[i].x:F3},{persons[i].y:F3}) world={NormalizedToWorld(persons[i])}", b);
            y += 18;
        }
        y += 8;

        h.normal.textColor = Color.cyan;
        GUI.Label(new Rect(x, y, 500, 22), $"── Tracks: {_tracks.Count} ──", h); y += 22;
        b.normal.textColor = Color.white;
        foreach (var tr in _tracks)
        {
            GUI.Label(new Rect(x, y, 750, 20),
                $"  id={tr.id} pos={tr.worldPos:F2} lastSeen={Time.time - tr.lastSeenTime:F2}s ago", b);
            y += 18;
        }
        y += 8;

        h.normal.textColor = Color.gray;
        GUI.Label(new Rect(x, y, 500, 22), "── Recent data ──", h); y += 22;
        b.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        foreach (var entry in _recentData) { GUI.Label(new Rect(x, y, 900, 20), entry, b); y += 18; }
        y += 8;

        h.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 500, 22), "── Log ──", h); y += 22;
        foreach (var entry in _logs)
        {
            b.normal.textColor = entry.Contains("<ERR>") ? Color.red
                               : entry.Contains("<EXC>") ? Color.magenta
                               : new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(x, y, 900, 20), entry, b); y += 18;
        }

        GUI.matrix = prevMatrix;
    }
#endif

    // ── Gizmos ──────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        UpdateCornerCache();
        if (!_cornersValid) return;
        Vector3[] c = _corners;
        Gizmos.color = boundColor;
        Gizmos.DrawLine(c[0], c[1]); Gizmos.DrawLine(c[1], c[2]);
        Gizmos.DrawLine(c[2], c[3]); Gizmos.DrawLine(c[3], c[0]);
        Gizmos.color = new Color(boundColor.r, boundColor.g, boundColor.b, 0.25f);
        Gizmos.DrawLine(c[0], c[2]); Gizmos.DrawLine(c[1], c[3]);
        float r = Vector3.Distance(c[0], c[1]) * 0.04f;
        Gizmos.color = originColor;
        Gizmos.DrawSphere(c[0], r);
        UnityEditor.Handles.color = originColor;
        UnityEditor.Handles.Label(c[0] + Vector3.up * r * 2f, $"ORIGIN (0,0)\n[{courtOrigin}]");
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(c[1] + Vector3.up * r, "(1,0)");
        UnityEditor.Handles.Label(c[2] + Vector3.up * r, "(1,1)");
        UnityEditor.Handles.Label(c[3] + Vector3.up * r, "(0,1)");
        Gizmos.color = Color.green;
        foreach (var p in persons) Gizmos.DrawWireSphere(NormalizedToWorld(p), r * 0.8f);
        if (Application.isPlaying)
        {
            UnityEditor.Handles.color = Color.cyan;
            foreach (var tr in _tracks)
                UnityEditor.Handles.Label(tr.worldPos + Vector3.up * 0.3f, $"id:{tr.id}");
        }
    }
#endif

    // ── Minimal OSC binary parser (messages + flat bundles; int32/float32/string/blob) ──
    static class MiniOsc
    {
        public static void ParsePacket(byte[] data, int offset, int length, Action<string, List<object>> onMessage)
        {
            if (length >= 8 && Encoding.ASCII.GetString(data, offset, 8) == "#bundle\0")
            {
                int pos = offset + 16; // "#bundle\0" (8) + OSC time tag (8)
                int end = offset + length;
                while (pos + 4 <= end)
                {
                    int elemSize = ReadInt32BE(data, pos); pos += 4;
                    if (elemSize <= 0 || pos + elemSize > end) break;
                    ParsePacket(data, pos, elemSize, onMessage);
                    pos += elemSize;
                }
                return;
            }

            int p = offset;
            int limit = offset + length;
            string address = ReadPaddedString(data, ref p, limit);
            if (string.IsNullOrEmpty(address) || address[0] != '/') return;
            string typeTags = ReadPaddedString(data, ref p, limit);

            var args = new List<object>();
            if (!string.IsNullOrEmpty(typeTags) && typeTags[0] == ',')
            {
                for (int t = 1; t < typeTags.Length; t++)
                {
                    switch (typeTags[t])
                    {
                        case 'i': args.Add(ReadInt32BE(data, p)); p += 4; break;
                        case 'f': args.Add(ReadFloatBE(data, p)); p += 4; break;
                        case 's': args.Add(ReadPaddedString(data, ref p, limit)); break;
                        case 'b':
                            int blen = ReadInt32BE(data, p); p += 4;
                            p += ((blen + 3) / 4) * 4;
                            break;
                        default: t = typeTags.Length; break; // unknown tag: stop, avoid misparse
                    }
                }
            }
            onMessage(address, args);
        }

        static int ReadInt32BE(byte[] d, int pos) => (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3];

        static float ReadFloatBE(byte[] d, int pos)
        {
            var be = new byte[4] { d[pos + 3], d[pos + 2], d[pos + 1], d[pos] };
            return BitConverter.ToSingle(be, 0);
        }

        static string ReadPaddedString(byte[] d, ref int pos, int limit)
        {
            int start = pos;
            while (pos < limit && d[pos] != 0) pos++;
            string s = Encoding.ASCII.GetString(d, start, pos - start);
            pos++; // skip null terminator
            while ((pos - start) % 4 != 0) pos++;
            return s;
        }
    }
}
