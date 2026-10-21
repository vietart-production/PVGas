using UnityEngine;

// Applies incoming /pvgas/home and /pvgas/topic <id> OSC messages from the
// Kiosk to this instance's UIManager, keeping the Relay's mirrored output in
// sync. Only listens in Relay mode so the same scene works for both builds.
public class RelayStateReceiver : MonoBehaviour
{
    [SerializeField] private UIManager uiManager;
    [SerializeField] private TopicData[] topics;
    [SerializeField] private OscReceiver receiver;

    private void Awake()
    {
        var config = DeploymentConfig.Load();
        if (config.Mode != DeploymentMode.Relay)
        {
            enabled = false;
            return;
        }

        receiver.OnMessage += HandleMessage;
        receiver.StartListening(config.listenPort);
    }

    private void HandleMessage(string address, object[] args)
    {
        if (address == "/pvgas/home")
        {
            uiManager.ShowHome();
        }
        else if (address == "/pvgas/topic" && args.Length > 0 && args[0] is string id)
        {
            foreach (var t in topics)
            {
                if (t != null && t.topicId == id)
                {
                    uiManager.ShowDetail(t);
                    return;
                }
            }
            Debug.LogWarning($"[RelayStateReceiver] Unknown topic id '{id}'.");
        }
    }
}
