using UnityEngine;

// Tells the Relay instance (running next to Resolume) which panel is
// showing, so its mirrored ProjectionCanvas + Spout feed stays in sync with
// this kiosk. No-ops in Relay mode so the same scene works for both builds.
public class KioskStateBroadcaster : MonoBehaviour
{
    private DeploymentConfig config;

    private void Awake() => config = DeploymentConfig.Load();

    public void BroadcastHome()
    {
        if (config.Mode != DeploymentMode.Kiosk) return;
        OscSender.Send(config.relayHost, config.relayPort, "/pvgas/home");
    }

    public void BroadcastTopic(string topicId)
    {
        if (config.Mode != DeploymentMode.Kiosk) return;
        OscSender.Send(config.relayHost, config.relayPort, "/pvgas/topic", topicId);
    }
}
