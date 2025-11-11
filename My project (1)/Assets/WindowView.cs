using UnityEngine;

public class WindowView : MonoBehaviour
{
    public Transform MainCamera;  // Player's main camera
    public Camera PortalCamera;   // The camera rendering to the window

    void LateUpdate()
    {
        // Match rotation (mouse look) only
        PortalCamera.transform.rotation = MainCamera.rotation;

        // Optional: keep PortalCamera at a fixed position
        // PortalCamera.transform.position = new Vector3(0,1,0); // set wherever the "other side" should be
    }
}
