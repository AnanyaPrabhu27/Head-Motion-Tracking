using UnityEngine;

public class WindowView : MonoBehaviour
{
    [Header("References")]
    public Transform MainCamera;  // Head/view camera to copy from
    public Camera PortalCamera;   // Camera that renders the portal texture

    void LateUpdate()
    {
        // Safety check: do nothing if references are missing
        if (MainCamera == null || PortalCamera == null) return;

        // Copy rotation so the portal view matches the head direction
        PortalCamera.transform.rotation = MainCamera.rotation;

        // OPTIONAL: also match position (e.g., keep a fixed offset in front of the head)
        // PortalCamera.transform.position = MainCamera.position + MainCamera.forward * 2f;
    }
}
