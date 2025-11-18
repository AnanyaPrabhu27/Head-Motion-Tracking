using UnityEngine;

/// <summary>
/// HeadTrackedPortalCamera
/// - Moves the main camera based on head tracking data.
/// - Intended to be used together with Portal.cs:
///   Portal.cs uses Camera.main (playerCam) as the reference view.
/// - Attach this script to the Main Camera in the portal scene.
/// </summary>
public class HeadTrackedPortalCamera : MonoBehaviour
{
    [Header("Head Tracking Source")]
    public SingleBodyTracker tracker;
    public Transform windowScreen; // Center of the "portal window" plane

    [Header("View Settings")]
    [Tooltip("How much head movement influences camera offset.")]
    public float viewScale = 0.3f;

    [Tooltip("Distance of the camera in front of the window screen.")]
    public float cameraDistance = 2.0f;

    private Vector3 _initialHeadPos;
    private bool _initialized = false;

    void Start()
    {
        TryInitialize();
    }

    void LateUpdate()
    {
        if (!TryInitialize())
            return;

        // Head movement relative to initial position
        Vector3 headDelta = tracker.headCenterWorld - _initialHeadPos;

        // Only use horizontal (x) and vertical (y) movement for parallax effect
        Vector3 offset = new Vector3(
            headDelta.x * viewScale,
            headDelta.y * viewScale,
            0f
        );

        // Camera sits in front of the window plus offset
        Vector3 basePos = windowScreen.position - windowScreen.forward * cameraDistance;
        transform.position = basePos + offset;

        // Always look at the window center
        transform.LookAt(windowScreen.position);
    }

    bool TryInitialize()
    {
        if (_initialized)
            return true;

        if (tracker == null || windowScreen == null)
            return false;

        _initialHeadPos = tracker.headCenterWorld;
        _initialized = true;
        return true;
    }
}
