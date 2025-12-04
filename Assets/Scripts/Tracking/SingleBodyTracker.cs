using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SingleBodyTracker - ZED SDK optimized version
/// Only tracks head keypoints (0,1,14,15,16,17), ignores arms/hands
/// NEW: Added hideTrackedPerson option for invisible tracking
/// </summary>
public class SingleBodyTracker : MonoBehaviour
{
    [Header("Skeleton naming")]
    [Tooltip("Root objects are expected to be named like 'Skeleton_ID_23'.")]
    public string skeletonNamePrefix = "Skeleton_ID_";

    [Header("ZED Keypoint indices (head only)")]
    [Tooltip("ZED SDK keypoint indices to use for head tracking")]
    public int[] headKeypointIndices = new int[] { 0, 1, 14, 15, 16, 17 };
    // 0=NOSE, 1=NECK, 14=RIGHT_EYE, 15=LEFT_EYE, 16=RIGHT_EAR, 17=LEFT_EAR

    [Header("Optional internal picking (keep OFF)")]
    public bool enableInternalPicking = false;
    public Camera pickCamera;
    public LayerMask pickMask = ~0;
    public float maxPickDistance = 50f;
    public KeyCode unlockKey = KeyCode.L;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float positionSmoothing = 0.2f;
    [Range(0f, 1f)] public float directionSmoothing = 0.2f;

    [Header("Visibility control")]
    public bool hideOthers = true;
    [Tooltip("Hide the tracked person too (invisible tracking - only head motion affects camera)")]
    public bool hideTrackedPerson = false;

    [Header("Debug")]
    public bool drawGizmos = true;
    public float gizmoPointSize = 0.05f;
    public Color headColor = Color.yellow;
    public Color forwardColor = Color.cyan;
    public bool logHead = false;
    public bool logKeypointsFound = true;
    public float logInterval = 0.5f;

    // Public outputs
    public int trackedId = -1;
    public Vector3 headCenterWorld = Vector3.zero;
    public Vector3 headForward = Vector3.forward;

    [SerializeField] private bool _locked = false;
    public bool Locked => _locked;
    public bool allowPicking => !_locked;

    // Internals
    private Transform _currentSkeleton;
    private Vector3 _headSmooth;
    private Vector3 _forwardSmooth;
    private float _logTimer = 0f;
    private readonly List<GameObject> _hiddenObjects = new List<GameObject>();

    void Update()
    {
        // Internal picking (usually disabled)
        if (enableInternalPicking && allowPicking && Input.GetMouseButtonDown(0))
        {
            Camera cam = pickCamera != null ? pickCamera : Camera.main;
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, maxPickDistance, pickMask))
                {
                    Transform root = FindSkeletonRoot(hit.transform);
                    if (root != null)
                    {
                        int id = ExtractIdFromName(root.name);
                        LockToId(id);
                    }
                }
            }
        }

        // Unlock hotkey
        if (_locked && Input.GetKeyDown(unlockKey))
        {
            Unlock();
        }

        // Update tracking if locked
        if (_locked && _currentSkeleton != null)
        {
            if (_currentSkeleton.gameObject.activeInHierarchy)
            {
                Vector3 rawHead = GetHeadPosition(_currentSkeleton);
                Vector3 rawForward = GetHeadForward(_currentSkeleton, rawHead);

                _headSmooth = Smooth(_headSmooth, rawHead, positionSmoothing);
                _forwardSmooth = Smooth(_forwardSmooth, rawForward, directionSmoothing);

                headCenterWorld = _headSmooth;
                headForward = _forwardSmooth;

                if (logHead)
                {
                    _logTimer += Time.deltaTime;
                    if (_logTimer >= logInterval)
                    {
                        _logTimer = 0f;
                        Debug.Log($"[SingleBodyTracker] Head (ID={trackedId}) = {headCenterWorld}");
                    }
                }
            }
        }
        else if (_locked && _currentSkeleton == null)
        {
            _currentSkeleton = FindSkeletonById(trackedId);
            if (_currentSkeleton == null)
            {
                Debug.Log("[SingleBodyTracker] Lost skeleton. Unlocking.");
                Unlock();
            }
        }

        if (enableInternalPicking)
            EnsureSkeletonColliders();
    }

    public void LockToId(int id)
    {
        Debug.Log($"[SingleBodyTracker] LockToId({id}) called");

        RestoreAll();

        trackedId = id;
        _currentSkeleton = FindSkeletonById(id);

        if (_currentSkeleton != null)
        {
            _locked = true;

            var rawHead = GetHeadPosition(_currentSkeleton);
            var rawForward = GetHeadForward(_currentSkeleton, rawHead);

            _headSmooth = rawHead;
            _forwardSmooth = rawForward;

            headCenterWorld = _headSmooth;
            headForward = _forwardSmooth;
            _logTimer = 0f;

            if (hideOthers)
            {
                if (hideTrackedPerson)
                {
                    // Hide ALL skeletons including the tracked one (invisible tracking)
                    Debug.Log($"[SingleBodyTracker] Hiding ALL skeletons (invisible tracking mode)");
                    HideAll();
                }
                else
                {
                    // Hide only other skeletons, keep tracked one visible
                    Debug.Log($"[SingleBodyTracker] Hiding other skeletons (keep: {_currentSkeleton.name})");
                    HideAllExcept(_currentSkeleton);
                }
            }

            Debug.Log($"[SingleBodyTracker] Locked to id={id} (object: {_currentSkeleton.name})");
        }
        else
        {
            _locked = false;
            trackedId = -1;
            Debug.LogWarning($"[SingleBodyTracker] LockToId({id}) failed: skeleton not found.");
        }
    }

    public void Unlock()
    {
        RestoreAll();
        _locked = false;
        trackedId = -1;
        _currentSkeleton = null;
        Debug.Log("[SingleBodyTracker] Unlocked.");
    }

    private void EnsureSkeletonColliders()
    {
        var all = FindObjectsOfType<Transform>();
        foreach (Transform t in all)
        {
            if (!t.name.StartsWith(skeletonNamePrefix)) continue;
            if (t.GetComponent<Collider>() == null)
            {
                var col = t.gameObject.AddComponent<SphereCollider>();
                col.radius = 0.4f;
                col.center = new Vector3(0f, 1f, 0f);
            }
        }
    }

    private Transform FindSkeletonRoot(Transform t)
    {
        while (t != null)
        {
            if (t.name.StartsWith(skeletonNamePrefix)) return t;
            t = t.parent;
        }
        return null;
    }

    private Transform FindSkeletonById(int id)
    {
        string name = skeletonNamePrefix + id;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                Transform[] allTransforms = rootObj.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
                {
                    if (t.name == name)
                        return t;
                }
            }
        }

        return null;
    }

    private int ExtractIdFromName(string name)
    {
        int idx = name.LastIndexOf('_');
        if (idx >= 0 && int.TryParse(name.Substring(idx + 1), out int n))
            return n;
        return -1;
    }

    private Vector3 Smooth(Vector3 prev, Vector3 cur, float amt)
    {
        return Vector3.Lerp(prev, cur, 1f - amt);
    }

    /// <summary>
    /// ZED SDK specific head position detection
    /// Only uses head keypoints: 0=NOSE, 1=NECK, 14=RIGHT_EYE, 15=LEFT_EYE, 16=RIGHT_EAR, 17=LEFT_EAR
    /// Completely ignores arm keypoints (4=RIGHT_WRIST, 7=LEFT_WRIST, etc)
    /// </summary>
    private Vector3 GetHeadPosition(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>();

        List<Transform> headKeypoints = new List<Transform>();

        // Find all head-related keypoints by index
        foreach (Transform t in all)
        {
            // Check if this transform represents a keypoint
            if (IsKeypointTransform(t))
            {
                int keypointIndex = ExtractKeypointIndex(t.name);

                // Only accept head keypoint indices
                if (System.Array.IndexOf(headKeypointIndices, keypointIndex) >= 0)
                {
                    headKeypoints.Add(t);
                    if (logKeypointsFound)
                        Debug.Log($"[SingleBodyTracker] Found head keypoint: {t.name} (index {keypointIndex})");
                }
            }
        }

        if (headKeypoints.Count == 0)
        {
            Debug.LogWarning($"[SingleBodyTracker] No head keypoints found for {root.name}. Using fallback.");
            return root.position + Vector3.up * 1.6f;
        }

        // Calculate average position of all head keypoints
        Vector3 sum = Vector3.zero;
        foreach (Transform t in headKeypoints)
        {
            sum += t.position;
        }

        Vector3 avgPos = sum / headKeypoints.Count;

        if (logKeypointsFound)
            Debug.Log($"[SingleBodyTracker] Head position from {headKeypoints.Count} keypoints: {avgPos}");

        return avgPos;
    }

    /// <summary>
    /// Check if a transform represents a ZED keypoint
    /// ZED keypoints are named as pure numbers: "0", "1", "2", etc.
    /// </summary>
    private bool IsKeypointTransform(Transform t)
    {
        string name = t.name;
        // ZED SDK uses pure number names for keypoints
        // Exclude "Cylinder" and other non-keypoint objects
        if (name.Contains("Cylinder") || name.Contains("Unity_Avatar"))
            return false;

        // Check if the entire name is a number
        return int.TryParse(name, out _);
    }

    /// <summary>
    /// Extract keypoint index from transform name
    /// For ZED SDK, the name IS the index (e.g., "0", "1", "14")
    /// </summary>
    private int ExtractKeypointIndex(string name)
    {
        if (int.TryParse(name, out int index))
            return index;

        return -1;
    }

    private Vector3 GetHeadForward(Transform root, Vector3 headPos)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>();
        Transform nose = null, neck = null;
        Transform leftEye = null, rightEye = null;

        // Find specific head keypoints
        foreach (Transform t in all)
        {
            if (!IsKeypointTransform(t)) continue;

            int index = ExtractKeypointIndex(t.name);

            if (index == 0) nose = t;        // NOSE
            else if (index == 1) neck = t;   // NECK
            else if (index == 14) rightEye = t;  // RIGHT_EYE
            else if (index == 15) leftEye = t;   // LEFT_EYE
        }

        // Method 1: Use nose-to-neck vector for forward direction
        if (nose != null && neck != null)
        {
            Vector3 dir = nose.position - neck.position;
            if (dir.sqrMagnitude > 1e-4f)
            {
                Debug.Log("[SingleBodyTracker] Using nose-neck direction");
                return dir.normalized;
            }
        }

        // Method 2: Use eyes to determine forward direction
        if (leftEye != null && rightEye != null)
        {
            Vector3 eyeCenter = (leftEye.position + rightEye.position) * 0.5f;
            Vector3 eyeVec = rightEye.position - leftEye.position;
            Vector3 dir = Vector3.Cross(eyeVec.normalized, Vector3.up);
            Debug.Log("[SingleBodyTracker] Using eyes direction");
            return dir.normalized;
        }

        // Fallback
        return root.forward;
    }

    /// <summary>
    /// Hide ALL skeletons including the tracked one (for invisible tracking)
    /// </summary>
    private void HideAll()
    {
        _hiddenObjects.Clear();
        int hiddenCount = 0;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                Transform[] allTransforms = rootObj.GetComponentsInChildren<Transform>(true);

                foreach (Transform t in allTransforms)
                {
                    if (!t.name.StartsWith(skeletonNamePrefix)) continue;

                    // Disable all renderers in this skeleton
                    Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        if (r.enabled)
                        {
                            r.enabled = false;
                            hiddenCount++;
                        }
                    }

                    if (renderers.Length > 0)
                    {
                        _hiddenObjects.Add(t.gameObject);
                    }
                }
            }
        }

        Debug.Log($"[SingleBodyTracker] Hidden {hiddenCount} renderers total (ALL skeletons)");
    }

    /// <summary>
    /// Hide all skeletons except the one we're tracking
    /// Uses Renderer.enabled = false (more reliable with ZED SDK)
    /// </summary>
    private void HideAllExcept(Transform keep)
    {
        _hiddenObjects.Clear();
        int hiddenCount = 0;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                Transform[] allTransforms = rootObj.GetComponentsInChildren<Transform>(true);

                foreach (Transform t in allTransforms)
                {
                    if (!t.name.StartsWith(skeletonNamePrefix)) continue;
                    if (t == keep) continue;

                    // Disable all renderers in this skeleton
                    Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        if (r.enabled)
                        {
                            r.enabled = false;
                            hiddenCount++;
                        }
                    }

                    if (renderers.Length > 0)
                    {
                        _hiddenObjects.Add(t.gameObject);
                    }
                }
            }
        }

        Debug.Log($"[SingleBodyTracker] Hidden {hiddenCount} renderers total (keeping {keep.name})");
    }

    private void RestoreAll()
    {
        int restoredCount = 0;

        foreach (GameObject go in _hiddenObjects)
        {
            if (go == null) continue;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (!r.enabled)
                {
                    r.enabled = true;
                    restoredCount++;
                }
            }
        }

        _hiddenObjects.Clear();

        if (restoredCount > 0)
            Debug.Log($"[SingleBodyTracker] Restored {restoredCount} renderers");
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = headColor;
        Gizmos.DrawSphere(headCenterWorld, gizmoPointSize);
        Gizmos.color = forwardColor;
        Gizmos.DrawLine(headCenterWorld, headCenterWorld + headForward * 0.3f);
    }
}