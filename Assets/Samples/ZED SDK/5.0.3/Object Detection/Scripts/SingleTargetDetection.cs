using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles single-person tracking logic for ZED camera.
/// - Detects multiple people but focuses on one (nearest or locked)
/// - Lock/unlock with L key
/// - Exposes target ID and pose for other scripts (e.g. PortalFollower)
/// </summary>
public class SingleTargetDetection : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the ZED_Rig_Mono (object containing ZEDManager).")]
    public ZEDManager zedManager;

    [Tooltip("Optional: assign the visualizer to hide non-selected boxes when locked.")]
    public ZED3DObjectVisualizer visualizer;

    [Header("Keys")]
    [Tooltip("Press this key to toggle lock/unlock.")]
    public KeyCode lockKey = KeyCode.L;

    [Header("Lock policy")]
    [Tooltip("If true, never auto-switch while locked (manual unlock required).")]
    public bool hardLock = true;

    [Tooltip("Soft lock only: auto-unlock if target missing for this many seconds.")]
    public float softLockMissingGraceSeconds = 2.0f;

    // runtime state
    private bool locked = false;
    private int? targetId = null;
    private float selectedDistanceM = 0f;

    // soft-lock tracking
    private float? missingSince = null;

    // cache detections
    private List<DetectedObject> lastFrameObjects = new List<DetectedObject>();

    void Start()
    {
        if (!zedManager)
        {
            zedManager = FindAnyObjectByType<ZEDManager>();
            if (!zedManager)
            {
                Debug.LogError("SingleTargetDetection: ZEDManager not found in scene.");
                enabled = false;
                return;
            }
        }

        if (!visualizer)
        {
            visualizer = FindAnyObjectByType<ZED3DObjectVisualizer>();
        }

        zedManager.OnZEDReady += OnZEDReady;
        zedManager.OnObjectDetection += OnObjectDetections;
        zedManager.OnStopObjectDetection += OnStopDetection;
    }

    private void OnZEDReady()
    {
        if (!zedManager.IsObjectDetectionRunning)
        {
            zedManager.StartObjectDetection();
        }
    }

    private void OnStopDetection()
    {
        lastFrameObjects.Clear();
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

    private void OnDestroy()
    {
        if (!zedManager) return;
        zedManager.OnZEDReady -= OnZEDReady;
        zedManager.OnObjectDetection -= OnObjectDetections;
        zedManager.OnStopObjectDetection -= OnStopDetection;
    }

    private void OnObjectDetections(ObjectDetectionFrame frame)
    {
        lastFrameObjects = frame.GetFilteredObjectList(true, false, false);
    }

    void Update()
    {
        // toggle lock
        if (Input.GetKeyDown(lockKey))
        {
            locked = !locked;
            if (!locked)
            {
                targetId = null;
                missingSince = null;
            }
        }

        if (lastFrameObjects == null || lastFrameObjects.Count == 0)
        {
            HandleMissing();
            PushSelectionToVisualizer();
            return;
        }

        if (locked && targetId.HasValue)
        {
            bool present = lastFrameObjects.Any(o => o.id == targetId.Value);

            if (present)
            {
                missingSince = null;
                UpdateDistanceToCurrent();
            }
            else
            {
                HandleMissing();
            }

            PushSelectionToVisualizer();
            return;
        }

        // not locked → auto select nearest
        ChooseNearest();
        UpdateDistanceToCurrent();
        PushSelectionToVisualizer();
    }

    private void HandleMissing()
    {
        if (locked && hardLock)
        {
            selectedDistanceM = 0f;
            if (missingSince == null) missingSince = Time.time;
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

    private void ChooseNearest()
    {
        Transform cam = zedManager ? zedManager.GetLeftCameraTransform() : Camera.main.transform;
        var nearest = lastFrameObjects
            .OrderBy(o => (o.Get3DWorldPosition() - cam.position).sqrMagnitude)
            .FirstOrDefault();

        if (nearest != null)
        {
            targetId = nearest.id;
        }
    }

    private void UpdateDistanceToCurrent()
    {
        if (!targetId.HasValue) { selectedDistanceM = 0f; return; }

        var sel = lastFrameObjects.FirstOrDefault(o => o.id == targetId.Value);
        if (sel == null) { selectedDistanceM = 0f; return; }

        Transform cam = zedManager ? zedManager.GetLeftCameraTransform() : Camera.main.transform;
        selectedDistanceM = Vector3.Distance(cam.position, sel.Get3DWorldPosition());
    }

    private void PushSelectionToVisualizer()
    {
        if (!visualizer) return;
        visualizer.selectedId = targetId.HasValue ? targetId.Value : -1;
        visualizer.onlyShowSelectedBox = locked;
    }

    void OnGUI()
    {
        string idStr = targetId.HasValue ? targetId.Value.ToString() : "None";
        string status = locked ? "Locked" : "Unlocked";

        if (locked && targetId.HasValue && (missingSince != null))
        {
            status += " (LOST)";
        }

        GUI.Label(new Rect(10, 10, 520, 22),
            $"Target ID: {idStr}  |  {status}  |  Distance: {selectedDistanceM:F2} m");
        GUI.Label(new Rect(10, 30, 520, 22),
            $"Press '{lockKey}' to Lock/Unlock  |  HardLock: {hardLock}  |  Grace: {softLockMissingGraceSeconds:F1}s");
    }

    // === Public getters for other scripts (e.g. PortalFollower) ===

    /// <summary>
    /// Returns the currently selected target ID, or -1 if none.
    /// </summary>
    public int GetCurrentTargetId()
    {
        return targetId.HasValue ? targetId.Value : -1;
    }

    /// <summary>
    /// Try to get the current target's world position & rotation.
    /// Returns true if a valid target exists in current frame.
    /// </summary>
    public bool TryGetTargetPose(out int id, out Vector3 position, out Quaternion rotation)
    {
        id = -1;
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!targetId.HasValue) return false;
        id = targetId.Value;

        var sel = lastFrameObjects.FirstOrDefault(o => o.id == targetId.Value);
        if (sel == null) return false;

        position = sel.Get3DWorldPosition();
        rotation = sel.Get3DWorldRotation(false);
        return true;
    }
}



