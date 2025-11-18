using UnityEngine;

/// <summary>
/// Very simple on-screen HUD for SingleBodyTracker.
/// Shows tracked id, distance to camera and lock state
/// in the top-right corner of the Game view.
/// </summary>
public class SimpleBodyHUD : MonoBehaviour
{
    [Header("Data source")]
    public SingleBodyTracker tracker;   // drag your SingleBodyTracker here

    [Header("Target camera")]
    public Camera targetCamera;         // drag Virtual View Camera here

    [Header("HUD settings")]
    public bool showHUD = true;
    public KeyCode unlockKeyHint = KeyCode.L;

    private string _line = "";

    void Reset()
    {
        if (tracker == null)
            tracker = FindObjectOfType<SingleBodyTracker>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void Update()
    {
        if (!showHUD) return;
        if (tracker == null)
        {
            _line = "HUD: no SingleBodyTracker assigned.";
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        int id = tracker.trackedId;
        Vector3 head = tracker.headCenterWorld;
        bool locked = tracker.Locked;

        float dist = -1f;
        if (cam != null && id >= 0)
            dist = Vector3.Distance(cam.transform.position, head);

        string idStr = (id >= 0) ? id.ToString() : "-";
        string distStr = (dist >= 0f) ? dist.ToString("F2") + " m" : "-";
        string lockStr = locked ? "Yes" : "No";
        string mouseHint = locked
            ? $"Press {unlockKeyHint} to unlock"
            : "Click a person to select";

        _line = $"ID: {idStr} | Dist: {distStr} | Locked: {lockStr} | Mouse: {mouseHint}";
    }

    void OnGUI()
    {
        if (!showHUD) return;
        if (string.IsNullOrEmpty(_line)) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperRight;
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        float pad = 10f;
        Rect r = new Rect(pad, pad, Screen.width - pad * 2f, 30f);
        GUI.Label(r, _line, style);
    }
}

