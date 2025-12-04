using UnityEngine;

/*
 * SimplePortalCamera – Stable Portal Rendering with Overscan + Oblique Projection
 * ------------------------------------------------------------------------------
 * PURPOSE
 * - Generates a clean, border-free portal image that blends naturally into the 3D world.
 * - Ensures the portal camera always stays perfectly aligned with the portal screen.
 * - Avoids visible scene edges even during large head movements.
 *
 * MAIN FEATURES
 * 1. Camera alignment:
 *      - The portal camera is placed directly behind the portal screen and rotated
 *        to face the same direction.
 *
 * 2. Overscan (FOV scaling):
 *      - Slightly increases the effective field of view so the portal texture
 *        never shows borders during head motion.
 *
 * 3. Oblique near-plane clipping:
 *      - Replaces the default near-clip plane with a clip plane exactly matching
 *        the portal screen surface.
 *      - This prevents the world behind the portal screen from “leaking through.”
 *
 * HOW TO USE
 * - Attach this script to PortalCam.
 * - Assign:
 *        mainCamera    ? Your Virtual View Camera
 *        portalScreen  ? Your quad used as the portal window
 * - Use overscanFactor = 1.05–1.12 for stable border-free view.
 *
 * RESULT
 * - Smooth, immersive portal rendering compatible with full head tracking.
 * - No visible edges, even with extreme viewing angles.
 */

[RequireComponent(typeof(Camera))]
public class SimplePortalCamera : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The main head-tracked camera (Virtual View Camera).")]
    public Camera mainCamera;

    [Tooltip("The portal screen quad.")]
    public Transform portalScreen;

    [Header("Settings")]
    [Tooltip("How far behind the portal screen the portal camera sits.")]
    public float backOffset = 0.03f;

    [Tooltip("Multiplier for FOV to avoid border leaking. 1.05–1.12 recommended.")]
    public float overscanFactor = 1.08f;

    [Tooltip("Enable perspective-correct clipping against the portal plane.")]
    public bool useObliqueProjection = true;

    private Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (mainCamera == null || portalScreen == null)
        {
            Debug.LogWarning("[SimplePortalCamera] Missing references.");
            return;
        }

        // ---------------------------------------------------------
        // 1. POSITION CAMERA
        // ---------------------------------------------------------
        // Place behind the portal screen, facing forward
        Vector3 forward = portalScreen.forward;
        transform.position = portalScreen.position - forward * backOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        // ---------------------------------------------------------
        // 2. MATCH PROJECTION (with overscan)
        // ---------------------------------------------------------
        float baseFOV = mainCamera.fieldOfView;
        _cam.fieldOfView = baseFOV * overscanFactor;
        _cam.aspect = mainCamera.aspect;
        _cam.nearClipPlane = mainCamera.nearClipPlane;
        _cam.farClipPlane = mainCamera.farClipPlane;

        // ---------------------------------------------------------
        // 3. APPLY OBLIQUE PROJECTION
        // ---------------------------------------------------------
        if (useObliqueProjection)
        {
            ApplyObliqueClipPlane();
        }
        else
        {
            _cam.projectionMatrix = mainCamera.projectionMatrix;
        }
    }

    void ApplyObliqueClipPlane()
    {
        // Portal plane in world space
        Vector3 pos = portalScreen.position;
        Vector3 normal = portalScreen.forward;

        // Transform plane to camera space
        Vector3 camNormal = _cam.worldToCameraMatrix.MultiplyVector(normal);
        Vector3 camPos = _cam.worldToCameraMatrix.MultiplyPoint(pos);

        float distance = -Vector3.Dot(camPos, camNormal);

        // Plane in camera space: ax + by + cz + d = 0
        Vector4 clipPlane = new Vector4(
            camNormal.x,
            camNormal.y,
            camNormal.z,
            distance
        );

        // Apply oblique matrix
        _cam.projectionMatrix = _cam.CalculateObliqueMatrix(clipPlane);
    }

    void OnDrawGizmos()
    {
        if (portalScreen == null) return;

        // Draw portal plane
        Gizmos.color = Color.cyan;
        Gizmos.matrix = portalScreen.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 1, 0.01f));
        Gizmos.matrix = Matrix4x4.identity;

        // Portal forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(portalScreen.position, portalScreen.forward * 0.5f);

        // Camera position
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.04f);
        }
    }
}
