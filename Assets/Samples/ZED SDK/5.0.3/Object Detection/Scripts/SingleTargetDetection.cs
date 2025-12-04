using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Single-person tracking using ZED Body Tracking.
/// - Mouse click to select target (screen-space nearest to cursor)
/// - Press L to Lock/Unlock
/// - Hard lock / Soft lock with grace
/// - Reattach when target re-enters (same ID first, else nearest-to-last-known)
/// - Pushes selectedId/onlyShowSelectedBox to ZED3DObjectVisualizer
/// - Tidy HUD with configurable origin
/// </summary>
public class SingleTargetDetection : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the ZED_Rig_Mono (object containing ZEDManager).")]
    public ZEDManager zedManager;

    [Tooltip("Optional: ZED3DObjectVisualizer to filter/hide boxes when locked.")]
    public ZED3DObjectVisualizer visualizer;

    [Tooltip("Camera for screen-space selection. Drag Camera_Left here.")]
    public Camera selectionCamera;

    [Header("Input")]
    [Tooltip("Mouse button to select (0=Left).")]
    public int mouseButton = 0;

    [Tooltip("Lock/Unlock key.")]
    public KeyCode lockKey = KeyCode.L;

    [Header("Lock policy")]
    [Tooltip("If true, never auto-switch while locked (manual unlock required).")]
    public bool hardLock = true;

    [Tooltip("Soft lock only: auto-unlock if missing beyond this time (seconds).")]
    public float softLockMissingGraceSeconds = 2.0f;

    [Header("Reattach")]
    [Tooltip("Try to reattach when target re-enters view.")]
    public bool tryReattach = true;

    [Tooltip("Max 3D distance from last-known pos to accept as same target when ID changed.")]
    public float reattachMaxMeters = 1.2f;

    [Tooltip("Require reattached body be the nearest to last-known pos.")]
    public bool reattachRequireNearest = true;

    [Header("UI")]
    public bool showHUD = true;
    public Vector2 hudStart = new Vector2(10, 10); // HUD 起点（本脚本放更靠上）

    // Runtime state
    private bool locked = false;
    private int? targetId = null;
    private float selectedDistanceM = 0f;

    private float? missingSince = null;
    private Vector3 lastKnownWorldPos = Vector3.zero;

    private List<DetectedBody> lastFrameBodies = new List<DetectedBody>();

    // Public read-only lock state (for other scripts)
    public bool IsLocked => locked;

    void Start()
    {
        if (!zedManager)
        {
            zedManager = FindAnyObjectByType<ZEDManager>();
            if (!zedManager)
            {
                Debug.LogError("SingleTargetDetection: ZEDManager not found.");
                enabled = false;
                return;
            }
        }

        if (!visualizer) visualizer = FindAnyObjectByType<ZED3DObjectVisualizer>();

        if (!selectionCamera)
        {
            selectionCamera = Camera.main;
            if (!selectionCamera)
            {
                var camT = zedManager.GetLeftCameraTransform();
                selectionCamera = camT ? camT.GetComponent<Camera>() : null;
            }
        }

        zedManager.OnZEDReady += OnZEDReady;
        zedManager.OnBodyTracking += OnBodyDetections;
        zedManager.OnStopObjectDetection += OnStopDetection; // SDK stop event is reused
    }

    private void OnZEDReady()
    {
        // 保险：确保只跑 BodyTracking，不跑 ObjectDetection
        if (zedManager.IsObjectDetectionRunning)
            zedManager.StopObjectDetection();

        if (!zedManager.IsBodyTrackingRunning)
            zedManager.StartBodyTracking();

        if (!selectionCamera)
        {
            selectionCamera = Camera.main;
            if (!selectionCamera)
            {
                var camT = zedManager.GetLeftCameraTransform();
                selectionCamera = camT ? camT.GetComponent<Camera>() : null;
            }
        }
    }

    private void OnDestroy()
    {
        if (!zedManager) return;
        zedManager.OnZEDReady -= OnZEDReady;
        zedManager.OnBodyTracking -= OnBodyDetections;
        zedManager.OnStopObjectDetection -= OnStopDetection;
    }

    private void OnStopDetection()
    {
        lastFrameBodies.Clear();
        targetId = null;
        locked = false;
        selectedDistanceM = 0f;
        missingSince = null;

        if (visualizer)
        {
            visualizer.selectedId = -1;
            visualizer.onlyShowSelectedBox = false;
        }
    }

    private void OnBodyDetections(BodyTrackingFrame frame)
    {
        // 放宽过滤，便于点选
        lastFrameBodies = frame.GetFilteredObjectList(true, true, true);
    }

    void Update()
    {
        // Toggle lock
        if (Input.GetKeyDown(lockKey))
        {
            locked = !locked;
            if (!locked)
            {
                targetId = null;
                missingSince = null;
            }
        }

        // Mouse selection
        if (Input.GetMouseButtonDown(mouseButton))
        {
            TrySelectByMouse();
            if (targetId.HasValue && !locked)
            {
                locked = true; // click-then-lock
                missingSince = null;
            }
        }

        if (lastFrameBodies == null || lastFrameBodies.Count == 0)
        {
            HandleMissing();
            PushSelectionToVisualizer();
            return;
        }

        if (locked && targetId.HasValue)
        {
            var current = lastFrameBodies.FirstOrDefault(b => b.id == targetId.Value);
            if (current != null)
            {
                missingSince = null;
                UpdateDistanceAndLastKnown(current);
            }
            else
            {
                TryReattachOrHandleMissing();
            }

            PushSelectionToVisualizer();
            return;
        }

        // Not locked → auto pick nearest
        AutoPickNearest();
        if (targetId.HasValue)
        {
            var sel = lastFrameBodies.FirstOrDefault(b => b.id == targetId.Value);
            if (sel != null) UpdateDistanceAndLastKnown(sel);
        }

        PushSelectionToVisualizer();
    }

    private void TrySelectByMouse()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!selectionCamera)
        {
            selectionCamera = Camera.main;
            if (!selectionCamera)
            {
                Debug.LogWarning("[SingleTargetDetection] No selectionCamera found!");
                return;
            }
        }
        if (lastFrameBodies == null || lastFrameBodies.Count == 0) return;

        Vector2 mouse = Input.mousePosition;
        float best = float.MaxValue;
        int? bestIdLocal = null;
        const float pickRadius = 80f; // 像素点击半径

        foreach (var b in lastFrameBodies)
        {
            Vector3 wpos = b.Get3DWorldPosition();
            if (ZEDSupportFunctions.IsVector3NaN(wpos)) continue;

            Vector3 sp = selectionCamera.WorldToScreenPoint(wpos);
            if (sp.z <= 0f) continue; // 在摄像机后面

            float dist = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude;
            if (dist < best)
            {
                best = dist;
                bestIdLocal = b.id;
            }
        }

        if (bestIdLocal.HasValue && best <= pickRadius * pickRadius)
        {
            targetId = bestIdLocal.Value;
            locked = true;
            missingSince = null;

            var sel = lastFrameBodies.FirstOrDefault(x => x.id == targetId.Value);
            if (sel != null) UpdateDistanceAndLastKnown(sel);

            Debug.Log("[SingleTargetDetection] Selected ID: " + targetId.Value);
        }
    }

    private void AutoPickNearest()
    {
        Transform cam = selectionCamera ? selectionCamera.transform
                         : (zedManager ? zedManager.GetLeftCameraTransform()
                                       : Camera.main ? Camera.main.transform : null);
        if (!cam) return;

        var nearest = lastFrameBodies
            .OrderBy(b => (b.Get3DWorldPosition() - cam.position).sqrMagnitude)
            .FirstOrDefault();

        if (nearest != null)
        {
            targetId = nearest.id;
            UpdateDistanceAndLastKnown(nearest);
        }
    }

    private void TryReattachOrHandleMissing()
    {
        if (!(locked && tryReattach))
        {
            HandleMissing();
            return;
        }

        var same = targetId.HasValue ? lastFrameBodies.FirstOrDefault(b => b.id == targetId.Value) : null;
        if (same != null)
        {
            missingSince = null;
            UpdateDistanceAndLastKnown(same);
            return;
        }

        if (lastFrameBodies.Count > 0)
        {
            var byDist = lastFrameBodies
                .Select(b => new { body = b, d = Vector3.Distance(lastKnownWorldPos, b.Get3DWorldPosition()) })
                .OrderBy(x => x.d)
                .FirstOrDefault();

            if (byDist != null && byDist.d <= reattachMaxMeters)
            {
                targetId = byDist.body.id;
                UpdateDistanceAndLastKnown(byDist.body);
                missingSince = null;
                return;
            }
        }

        HandleMissing();
    }

    private void HandleMissing()
    {
        if (locked && hardLock)
        {
            if (missingSince == null) missingSince = Time.time;
            selectedDistanceM = 0f;
            return;
        }

        if (locked && !hardLock)
        {
            if (missingSince == null) missingSince = Time.time;

            if (Time.time - missingSince.Value >= softLockMissingGraceSeconds)
            {
                locked = false;
                targetId = null;
                missingSince = null;
            }
            selectedDistanceM = 0f;
            return;
        }

        selectedDistanceM = 0f;
        missingSince = null;
    }

    private void UpdateDistanceAndLastKnown(DetectedBody body)
    {
        Transform cam = selectionCamera ? selectionCamera.transform
                         : (zedManager ? zedManager.GetLeftCameraTransform()
                                       : Camera.main ? Camera.main.transform : null);
        if (!cam) return;

        Vector3 wpos = body.Get3DWorldPosition();
        selectedDistanceM = Vector3.Distance(cam.position, wpos);
        lastKnownWorldPos = wpos;
    }

    private void PushSelectionToVisualizer()
    {
        if (!visualizer) return;
        visualizer.selectedId = targetId.HasValue ? targetId.Value : -1;
        visualizer.onlyShowSelectedBox = locked;
    }

    // ===== HUD =====
    private static void DrawShadowedLabel(Rect r, string text, GUIStyle style)
    {
        var old = style.normal.textColor;
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
        style.normal.textColor = old;
        GUI.Label(r, text, style);
    }

    void OnGUI()
    {
        if (!showHUD) return;

        float x = hudStart.x;
        float y = hudStart.y;
        float w = 720f;
        float h = 22f;
        float pad = 6f;

        // 半透明底板
        Color oldCol = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.35f);
        GUI.Box(new Rect(x - 6, y - 6, w + 12, h * 3 + pad * 2 + 12), GUIContent.none);
        GUI.color = oldCol;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.white },
            alignment = TextAnchor.UpperLeft
        };

        string idStr = targetId.HasValue ? targetId.Value.ToString() : "None";
        string status = locked ? "Locked" : "Unlocked";
        if (locked && targetId.HasValue && (missingSince != null)) status += " (LOST)";

        DrawShadowedLabel(new Rect(x, y, w, h),
            "[BodyTracking] Target ID: " + idStr + "  |  " + status + "  |  Distance: " + selectedDistanceM.ToString("F2") + " m", style);
        y += h + pad;

        DrawShadowedLabel(new Rect(x, y, w, h),
            "Mouse select | '" + lockKey + "' Lock/Unlock | HardLock: " + hardLock + " | Grace: " + softLockMissingGraceSeconds.ToString("F1") + "s | Reattach<=" + reattachMaxMeters.ToString("F1") + "m", style);
        y += h + pad;

        DrawShadowedLabel(new Rect(x, y, w, h),
            "SelCam: " + (selectionCamera ? selectionCamera.name : "NULL") + " | Bodies: " + (lastFrameBodies != null ? lastFrameBodies.Count : 0), style);
    }

    // ===== Public API =====
    /// <summary>Return current target ID, or -1 if none.</summary>
    public int GetCurrentTargetId()
    {
        return targetId.HasValue ? targetId.Value : -1;
    }

    /// <summary>Try to get current target pose. Returns true if valid this frame.</summary>
    public bool TryGetTargetPose(out int id, out Vector3 position, out Quaternion rotation)
    {
        id = -1; position = Vector3.zero; rotation = Quaternion.identity;
        if (!targetId.HasValue || lastFrameBodies == null) return false;
        var sel = lastFrameBodies.FirstOrDefault(o => o.id == targetId.Value);
        if (sel == null) return false;

        id = targetId.Value;
        position = sel.Get3DWorldPosition();
        rotation = sel.Get3DWorldRotation(false);
        return true;
    }
}
