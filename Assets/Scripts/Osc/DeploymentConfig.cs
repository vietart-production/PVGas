using System;
using System.IO;
using UnityEngine;

public enum DeploymentMode { Kiosk, Relay }

// Loaded from <persistentDataPath>/config.json. Written out with a sensible
// per-platform default the first time an app runs, so it can be found and
// edited on-site once real IPs are known, without rebuilding.
[Serializable]
public class DeploymentConfig
{
    public string mode = "Kiosk";
    public string relayHost = "127.0.0.1";
    public int relayPort = 9000;
    public int listenPort = 9000;

    public DeploymentMode Mode =>
        mode.Equals("Relay", StringComparison.OrdinalIgnoreCase) ? DeploymentMode.Relay : DeploymentMode.Kiosk;

    private static DeploymentConfig cached;

    public static DeploymentConfig Load()
    {
        if (cached != null) return cached;

        string path = Path.Combine(Application.persistentDataPath, "config.json");
        try
        {
            if (File.Exists(path))
            {
                cached = JsonUtility.FromJson<DeploymentConfig>(File.ReadAllText(path));
            }
            else
            {
                cached = new DeploymentConfig
                {
                    mode = Application.platform == RuntimePlatform.Android ? "Kiosk" : "Relay"
                };
                File.WriteAllText(path, JsonUtility.ToJson(cached, true));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DeploymentConfig] Failed to load/write {path}: {e.Message}. Using defaults.");
            cached = new DeploymentConfig();
        }
        return cached;
    }
}
