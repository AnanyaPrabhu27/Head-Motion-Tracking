using UnityEngine;

/// <summary>
/// SimpleLookAtTracker - Ultra Simple Head Tracker with LookAt
/// 
/// Features:
/// 1. Camera follows head movement
/// 2. Always looks at target point
/// 3. Smoothing to prevent jitter
/// 4. Dead zone to filter small noise
/// 
/// Fixes:
/// - Camera height is now relative to head, not absolute
/// - Added smoothing to prevent jitter from tracking noise
/// - Added dead zone to filter tiny head movements
/// </summary>
public class SimpleLookAtTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    [Tooltip("Single body tracker component")]
    public SingleBodyTracker tracker;

    [Header("Target Settings")]
    [Tooltip("Point the camera will always look at")]
    public Transform lookAtTarget;

    [Header("Camera Settings")]
    [Tooltip("Movement scale for parallax effect")]
    public float movementScale = 15.0f;

    [Tooltip("Height offset relative to head (NOT absolute height)")]
    public float cameraHeight = 0f;

    [Tooltip("Z-axis offset (negative = backward)")]
    public float cameraZOffset = -30f;

    [Header("Smoothing Settings")]
    [Tooltip("Head position smoothing (filters ZED tracking noise)")]
    [Range(0f, 1f)]
    public float headSmoothing = 0.8f;  // Smooth the head position data first

    [Tooltip("Camera position smoothing (0=full smooth, 1=no smooth)")]
    [Range(0f, 1f)]
    public float positionSmoothing = 0.7f;  // Higher = more responsive

    [Tooltip("Movement dead zone in meters, movements smaller than this are ignored")]
    public float movementDeadZone = 0.005f; // 5mm - smaller for smoother tracking

    [Header("Debug")]
    [Tooltip("Log debug info to Console")]
    public bool enableDebugLog = false;

    [Tooltip("Debug log interval in seconds")]
    public float logInterval = 0.5f;

    // Private variables for tracking state
    private Vector3 _initialHeadPos;      // Initial head position when tracking starts
    private Vector3 _lastTrackedHead;     // Last significant head position (used for dead zone)
    private Vector3 _smoothedHeadPos;     // Smoothed head position (filters noise)
    private bool _initialized = false;    // Whether tracking has been initialized
    private float _logTimer = 0f;         // Timer for debug logging

    void Start()
    {
        // Create a default look-at target if none provided
        if (lookAtTarget == null)
        {
            GameObject target = new GameObject("LookAtTarget");
            target.transform.position = new Vector3(0, 0, 200);
            lookAtTarget = target.transform;

            if (enableDebugLog)
                Debug.Log("[SimpleLookAtTracker] Created default look-at target at " + target.transform.position);
        }
    }

    void LateUpdate()
    {
        // Only update if tracker is valid and locked to a person
        if (tracker == null || !tracker.Locked)
            return;

        // Initialize on first frame
        if (!_initialized)
        {
            _initialHeadPos = tracker.headCenterWorld;
            _lastTrackedHead = _initialHeadPos;
            _smoothedHeadPos = _initialHeadPos;  // Initialize smoothed position
            _initialized = true;

            if (enableDebugLog)
                Debug.Log($"[SimpleLookAtTracker] Initialized at {_initialHeadPos}");
        }

        UpdateCamera();
    }

    /// <summary>
    /// Update camera position and rotation based on head tracking
    /// Uses double smoothing: first smooth head position, then smooth camera position
    /// </summary>
    void UpdateCamera()
    {
        Vector3 rawHead = tracker.headCenterWorld;

        // STEP 1: Smooth the raw head position to filter ZED tracking noise
        _smoothedHeadPos = Vector3.Lerp(_smoothedHeadPos, rawHead, headSmoothing);

        Vector3 currentHead = _smoothedHeadPos;

        // STEP 2: Dead zone check on smoothed position
        float movementDistance = Vector3.Distance(currentHead, _lastTrackedHead);
        if (movementDistance < movementDeadZone)
        {
            // Movement too small, skip update
            return;
        }

        // Update last tracked position
        _lastTrackedHead = currentHead;

        // STEP 3: Calculate head movement delta from initial position
        Vector3 headDelta = currentHead - _initialHeadPos;

        // STEP 4: Calculate target camera position
        // X: follows head horizontal movement (scaled for parallax)
        // Y: relative to head height (NOT absolute)
        // Z: offset backward from head position
        float camX = currentHead.x + (headDelta.x * movementScale);
        float camY = currentHead.y + cameraHeight;  // Now relative to head!
        float camZ = currentHead.z + cameraZOffset;

        Vector3 targetPos = new Vector3(camX, camY, camZ);

        // STEP 5: Smooth camera position transition
        // This is the second level of smoothing for extra stability
        transform.position = Vector3.Lerp(transform.position, targetPos, positionSmoothing);

        // Always look at the target point
        if (lookAtTarget != null)
        {
            transform.LookAt(lookAtTarget);
        }

        // Debug logging (throttled by timer)
        if (enableDebugLog)
        {
            _logTimer += Time.deltaTime;
            if (_logTimer >= logInterval)
            {
                _logTimer = 0f;
                Debug.Log($"[SimpleLookAtTracker] Raw: {rawHead}, Smoothed: {currentHead}, " +
                         $"Camera: {transform.position}, Movement: {movementDistance:F4}m");
            }
        }
    }

    /// <summary>
    /// Reset tracking when person is unlocked
    /// </summary>
    public void ResetTracking()
    {
        _initialized = false;
        _logTimer = 0f;

        if (enableDebugLog)
            Debug.Log("[SimpleLookAtTracker] Tracking reset");
    }

    void OnDrawGizmos()
    {
        // Draw debug visualization in Scene view
        if (lookAtTarget != null)
        {
            // Draw line from camera to look-at target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, lookAtTarget.position);

            // Draw sphere at look-at target
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lookAtTarget.position, 0.2f);
        }

        // Draw camera direction
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}