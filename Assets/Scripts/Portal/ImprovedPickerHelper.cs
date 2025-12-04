using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ImprovedPickerHelper - Complete version with OnGUI visualization
/// Screen-space picker to make selecting a person in RGB mode easier.
/// </summary>
public class ImprovedPickerHelper : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ZED RGB camera (e.g., Camera_Left). Used to project heads to screen.")]
    public Camera zedCamera;

    [Tooltip("SingleBodyTracker for checking lock state.")]
    public SingleBodyTracker tracker;

    [Header("Picking Settings")]
    [Tooltip("Pixel radius around the mouse within which a skeleton can be selected.")]
    public float screenPickRadius = 100f;

    [Tooltip("Max allowed world distance (meters) from camera to head when picking.")]
    public float maxPickDistance = 15f;

    [Header("Skeleton Settings")]
    [Tooltip("Root objects are expected to be named like 'Skeleton_ID_23'.")]
    public string skeletonPrefix = "Skeleton_ID_";

    [Header("Mode Control")]
    [Tooltip("If true, picker only works in RGB mode.")]
    public bool onlyPickInRGB = true;

    private bool _isRGBMode = true;
    public void SetRGBMode(bool isRGB) => _isRGBMode = isRGB;

    [Header("Debug")]
    public bool showDebug = true;
    public Color debugColor = Color.green;

    private readonly List<SkeletonInfo> _cached = new List<SkeletonInfo>();

    private class SkeletonInfo
    {
        public Transform root;
        public int id;
        public Vector3 headWorld;
        public Vector3 headScreen;
        public float distance;
    }

    void Update()
    {
        RefreshSkeletonCache();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"[ImprovedPicker] Found {_cached.Count} skeletons in scene:");
            if (_cached.Count == 0)
            {
                Debug.LogWarning("[ImprovedPicker] No skeletons cached! Checking hierarchy manually...");
                GameObject[] all = FindObjectsOfType<GameObject>(true);
                int skelCount = 0;
                foreach (var go in all)
                {
                    if (go.name.StartsWith(skeletonPrefix))
                    {
                        skelCount++;
                        Debug.Log($"  Found in hierarchy: {go.name}, active={go.activeInHierarchy}");
                    }
                }
                Debug.Log($"  Total skeletons in hierarchy: {skelCount}");
            }
            else
            {
                foreach (var s in _cached)
                {
                    Debug.Log($"  - ID {s.id}: {s.root.name} at {s.headWorld}, screen: {s.headScreen}, distance: {s.distance:F2}m");
                }
            }
        }
    }

    public int TryPickByScreenSpace()
    {
        if (tracker != null && !tracker.allowPicking)
            return -1;

        if (onlyPickInRGB && !_isRGBMode)
            return -1;

        if (zedCamera == null)
        {
            Debug.LogWarning("[ImprovedPicker] ZED camera not assigned.");
            return -1;
        }

        RefreshSkeletonCache();
        if (_cached.Count == 0)
        {
            Debug.Log("[ImprovedPicker] No skeletons found in scene.");
            return -1;
        }

        Vector2 mouse = Input.mousePosition;
        float bestDist = float.MaxValue;
        SkeletonInfo best = null;

        foreach (var sk in _cached)
        {
            if (sk.headScreen.z <= 0f) continue;
            if (sk.distance > maxPickDistance) continue;

            float d = Vector2.Distance(mouse, new Vector2(sk.headScreen.x, sk.headScreen.y));
            if (d < screenPickRadius && d < bestDist)
            {
                bestDist = d;
                best = sk;
            }
        }

        if (best != null)
        {
            Debug.Log($"[ImprovedPicker] Selected ID {best.id} (screen {bestDist:F1}px, world {best.distance:F2}m)");
            return best.id;
        }

        Debug.Log($"[ImprovedPicker] No skeleton within {screenPickRadius}px of mouse position. Found {_cached.Count} skeletons total.");
        return -1;
    }

    private void RefreshSkeletonCache()
    {
        _cached.Clear();
        if (zedCamera == null) return;

        var all = FindObjectsOfType<Transform>(true);

        foreach (var t in all)
        {
            if (!t.name.StartsWith(skeletonPrefix)) continue;
            if (!t.gameObject.activeInHierarchy) continue;

            int id = ExtractId(t.name);
            if (id < 0) continue;

            Vector3 head = GetHeadWorld(t);
            Vector3 screen = zedCamera.WorldToScreenPoint(head);
            float dist = Vector3.Distance(zedCamera.transform.position, head);

            _cached.Add(new SkeletonInfo
            {
                root = t,
                id = id,
                headWorld = head,
                headScreen = screen,
                distance = dist
            });
        }
    }

    private static int ExtractId(string name)
    {
        int idx = name.LastIndexOf('_');
        if (idx >= 0 && int.TryParse(name.Substring(idx + 1), out int n))
            return n;
        return -1;
    }

    private static Vector3 GetHeadWorld(Transform root)
    {
        var children = root.GetComponentsInChildren<Transform>();
        foreach (var t in children)
        {
            if (t.name.ToLower().Contains("head"))
                return t.position;
        }

        float maxY = float.NegativeInfinity;
        Transform highest = null;
        foreach (var t in children)
        {
            if (t.position.y > maxY)
            {
                maxY = t.position.y;
                highest = t;
            }
        }

        if (highest != null) return highest.position;
        return root.position + Vector3.up * 1.6f;
    }

    void OnGUI()
    {
        if (!showDebug) return;

        // Draw skeleton head positions
        foreach (var sk in _cached)
        {
            if (sk.headScreen.z <= 0) continue;
            if (sk.distance > maxPickDistance) continue;

            Vector2 screenPos = new Vector2(sk.headScreen.x, Screen.height - sk.headScreen.y);

            DrawCross(screenPos, 20f, Color.yellow);

            var style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.yellow;
            style.fontSize = 14;
            GUI.Label(new Rect(screenPos.x + 15, screenPos.y - 7, 100, 20), $"ID {sk.id}", style);
        }

        // Draw mouse cursor
        Vector2 mouse = Input.mousePosition;
        mouse.y = Screen.height - mouse.y;
        DrawCross(mouse, 10f, Color.red);
    }

    void DrawCross(Vector2 center, float size, Color color)
    {
        var prevColor = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(new Rect(center.x - size, center.y - 1, size * 2, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - 1, center.y - size, 2, size * 2), Texture2D.whiteTexture);

        GUI.color = prevColor;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || zedCamera == null) return;

        Gizmos.color = debugColor;
        foreach (var sk in _cached)
        {
            if (sk.distance > maxPickDistance) continue;
            Gizmos.DrawWireSphere(sk.headWorld, 0.15f);
            Gizmos.DrawLine(zedCamera.transform.position, sk.headWorld);
        }
    }
}