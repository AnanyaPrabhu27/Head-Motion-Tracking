using UnityEngine;

/// <summary>
/// SingleBodyTracker (skeleton-click version)
/// - Does NOT use ZED Bodies directly
/// - Uses the Skeleton_ID_xxx objects spawned by the ZED sample
/// - Automatically adds a SphereCollider to each Skeleton_ID_xxx
/// - Click a person in the Game view to lock on that skeleton
/// - Exposes: trackedId, headCenterWorld, headForward, _locked, Locked
/// </summary>
public class SingleBodyTracker : MonoBehaviour
{
    [Header("Camera used for picking (usually Camera_Left / Virtual View Camera)")]
    public Camera pickCamera;

    [Header("Selection settings")]
    public KeyCode unlockKey = KeyCode.L;
    public float maxPickDistance = 50f;
    public string skeletonNamePrefix = "Skeleton_ID_";

    [Header("Smoothing")]
    [Range(0f, 1f)] public float positionSmoothing = 0.2f;
    [Range(0f, 1f)] public float directionSmoothing = 0.2f;

    [Header("Debug Gizmos")]
    public bool drawGizmos = true;
    public float gizmoPointSize = 0.05f;
    public Color headColor = Color.yellow;
    public Color forwardColor = Color.cyan;

    // Output (read only – used by HUD / portal)
    [Header("Tracked Output (Read Only)")]
    public int trackedId = -1;
    public Vector3 headCenterWorld = Vector3.zero;
    public Vector3 headForward = Vector3.forward;

    [Header("Lock State (Read Only)")]
    public bool _locked = false;
    public bool Locked => _locked;   // used by SimpleBodyHUD

    // internal runtime state
    private Transform _currentSkeleton;
    private Vector3 _headSmooth;
    private Vector3 _forwardSmooth;

    void Update()
    {
        var cam = pickCamera != null ? pickCamera : Camera.main;
        if (cam == null) return;

        // Make sure skeletons have colliders so Raycast can hit them
        EnsureSkeletonColliders();

        // Unlock key
        if (Input.GetKeyDown(unlockKey))
        {
            trackedId = -1;
            _locked = false;
            _currentSkeleton = null;
            Debug.Log("[SingleBodyTracker] Unlocked (L pressed).");
        }

        // Mouse click to select a skeleton
        if (Input.GetMouseButtonDown(0))
        {
            TryPickSkeleton(cam);
        }

        // If we have a locked skeleton, update head position and forward
        if (_locked && _currentSkeleton != null)
        {
            // Get a better estimate of the head position from the skeleton
            Vector3 rawHeadPos = GetHeadPosition(_currentSkeleton);
            Vector3 rawForward = _currentSkeleton.forward;

            _headSmooth = Smooth(_headSmooth, rawHeadPos, positionSmoothing);
            _forwardSmooth = Smooth(_forwardSmooth, rawForward, directionSmoothing);

            headCenterWorld = _headSmooth;
            headForward = _forwardSmooth;
        }
        else
        {
            // If locked but skeleton was destroyed / lost, try to find it again by name
            if (_locked && _currentSkeleton == null && trackedId >= 0)
            {
                _currentSkeleton = FindSkeletonById(trackedId);
                if (_currentSkeleton == null)
                {
                    Debug.Log("[SingleBodyTracker] Lost skeleton, unlocking.");
                    trackedId = -1;
                    _locked = false;
                }
            }
        }
    }

    /// <summary>
    /// Ensure each Skeleton_ID_xxx has some Collider so Physics.Raycast can hit it.
    /// </summary>
    void EnsureSkeletonColliders()
    {
        var all = FindObjectsOfType<Transform>();
        foreach (var t in all)
        {
            if (!t.name.StartsWith(skeletonNamePrefix))
                continue;

            // If no collider on this object, add a SphereCollider
            if (t.GetComponent<Collider>() == null)
            {
                var col = t.gameObject.AddComponent<SphereCollider>();
                col.radius = 0.4f;
                col.center = new Vector3(0f, 1f, 0f); // around upper body
                // Debug.Log($"[SingleBodyTracker] Added SphereCollider to {t.name}");
            }
        }
    }

    // Mouse picking with debug logs
    void TryPickSkeleton(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Debug.Log($"[SingleBodyTracker] Mouse click at {Input.mousePosition}, ray = {ray.origin} -> {ray.direction}");

        if (Physics.Raycast(ray, out RaycastHit hit, maxPickDistance))
        {
            Debug.Log($"[SingleBodyTracker] Raycast hit: {hit.collider.gameObject.name}");

            Transform skeletonRoot = FindSkeletonRoot(hit.transform);
            if (skeletonRoot != null)
            {
                int id = ExtractIdFromName(skeletonRoot.name);
                trackedId = id;
                _currentSkeleton = skeletonRoot;
                _locked = true;

                // Initialize smoothing with the current head pose
                Vector3 rawHeadPos = GetHeadPosition(_currentSkeleton);
                Vector3 rawForward = _currentSkeleton.forward;

                _headSmooth = rawHeadPos;
                _forwardSmooth = rawForward;
                headCenterWorld = _headSmooth;
                headForward = _forwardSmooth;

                Debug.Log($"[SingleBodyTracker] Selected skeleton {skeletonRoot.name} (id={trackedId})");
            }
            else
            {
                Debug.Log("[SingleBodyTracker] Hit something, but no Skeleton_ID_... in parent chain.");
            }
        }
        else
        {
            Debug.Log("[SingleBodyTracker] Raycast hit nothing.");
        }
    }

    /// <summary>
    /// Try to get a real head joint position from the skeleton hierarchy.
    /// If no head joint is found, fall back to a simple estimate above the root.
    /// </summary>
    Vector3 GetHeadPosition(Transform skeletonRoot)
    {
        if (skeletonRoot == null)
            return Vector3.zero;

        // 1) Search for a child whose name contains "head" (case-insensitive)
        Transform[] all = skeletonRoot.GetComponentsInChildren<Transform>();
        Transform bestHead = null;

        foreach (var t in all)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("head"))   // e.g., "Head", "head_joint", "HEAD"
            {
                bestHead = t;
                break;
            }
        }

        if (bestHead != null)
        {
            return bestHead.position;
        }

        // 2) If no explicit head joint, approximate by taking the highest Y joint
        float maxY = float.NegativeInfinity;
        Transform highest = null;
        foreach (var t in all)
        {
            if (t.position.y > maxY)
            {
                maxY = t.position.y;
                highest = t;
            }
        }
        if (highest != null)
        {
            return highest.position;
        }

        // 3) Final fallback: a fixed offset above the skeleton root
        //    (approximate human head height ~1.6m above root/hip)
        return skeletonRoot.position + Vector3.up * 1.6f;
    }

    // Walk up the hierarchy to find a parent whose name starts with Skeleton_ID_
    Transform FindSkeletonRoot(Transform t)
    {
        while (t != null)
        {
            if (t.name.StartsWith(skeletonNamePrefix))
                return t;
            t = t.parent;
        }
        return null;
    }

    // Find skeleton by numeric ID (rebuild its name and search all transforms)
    Transform FindSkeletonById(int id)
    {
        string targetName = skeletonNamePrefix + id;
        var all = FindObjectsOfType<Transform>();
        foreach (var t in all)
        {
            if (t.name == targetName)
                return t;
        }
        return null;
    }

    // Extract the numeric id from a name like "Skeleton_ID_256"
    int ExtractIdFromName(string name)
    {
        int lastUnderscore = name.LastIndexOf('_');
        if (lastUnderscore >= 0 &&
            int.TryParse(name.Substring(lastUnderscore + 1), out int value))
        {
            return value;
        }
        return -1;
    }

    Vector3 Smooth(Vector3 prev, Vector3 cur, float amount)
    {
        if (amount <= 0f) return cur;
        if (amount >= 1f) return Vector3.Lerp(prev, cur, 0.1f);
        return Vector3.Lerp(prev, cur, 1f - amount);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = headColor;
        Gizmos.DrawSphere(headCenterWorld, gizmoPointSize);

        Gizmos.color = forwardColor;
        Gizmos.DrawLine(headCenterWorld,
                        headCenterWorld + headForward * 0.3f);
    }
}




