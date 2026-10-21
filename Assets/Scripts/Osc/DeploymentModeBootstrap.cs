using UnityEngine;

// Central on/off switch between Kiosk and Relay. A Kiosk (Android, touch)
// instance doesn't need the Spout/Resolume output pipeline - and SpoutSender's
// native plugin isn't even available on Android - so that whole subtree stays
// disabled there. A Relay (Windows, next to Resolume) instance needs it active.
public class DeploymentModeBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject projectionCamera;
    [SerializeField] private GameObject projectionCanvas;
    [SerializeField] private GameObject resolumeFader;
    [SerializeField] private KioskStateBroadcaster broadcaster;
    [SerializeField] private RelayStateReceiver receiver;

    // Back/Home buttons on the MAIN canvas - pointless in Relay mode since
    // nobody touches that screen, and visually unwanted on whatever output
    // (window, extended monitor, capture card) ends up showing it.
    [SerializeField] private GameObject mainBottomBar;

    private void Awake()
    {
        bool isRelay = DeploymentConfig.Load().Mode == DeploymentMode.Relay;

        projectionCamera.SetActive(isRelay);
        projectionCanvas.SetActive(isRelay);
        resolumeFader.SetActive(isRelay);

        if (broadcaster != null) broadcaster.enabled = !isRelay;
        if (receiver != null) receiver.enabled = isRelay;
        if (mainBottomBar != null) mainBottomBar.SetActive(!isRelay);
    }
}
