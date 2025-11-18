using UnityEngine;
using System;
using System.Reflection;

/// <summary>
/// Simple HUD for SingleBodyTracker:
/// - Reads trackedId, headCenterWorld and _locked from dataProvider
/// - Shows a text line in the top-right corner of the Game view.
/// </summary>
public class BodyGUIOverlay : MonoBehaviour
{
    [Header("Data source")]
    [Tooltip("Drag the SingleBodyTracker component here.")]
    public Component dataProvider;     // SingleBodyTracker

    [Header("Target camera")]
    public Camera targetCamera;        // Camera_Left

    [Header("Field names (must match SingleBodyTracker)")]
    public string fieldTrackedId = "trackedId";
    public string fieldHeadCenterWorld = "headCenterWorld";
    public string fieldLocked = "_locked";

    [Header("HUD")]
    public KeyCode unlockKeyHint = KeyCode.L;
    public bool showHUD = true;

    // cached reflection info
    private FieldInfo fiTrackedId;
    private FieldInfo fiHeadCenter;
    private FieldInfo fiLocked;

    private string hudLine = "BodyGUIOverlay: waiting for provider...";

    void Awake()
    {
        CacheMembers();
    }

    void CacheMembers()
    {
        if (dataProvider == null) return;

        var t = dataProvider.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        fiTrackedId = !string.IsNullOrEmpty(fieldTrackedId)
            ? t.GetField(fieldTrackedId, flags) : null;
        fiHeadCenter = !string.IsNullOrEmpty(fieldHeadCenterWorld)
            ? t.GetField(fieldHeadCenterWorld, flags) : null;
        fiLocked = !string.IsNullOrEmpty(fieldLocked)
            ? t.GetField(fieldLocked, flags) : null;
    }

    void Update()
    {
        if (dataProvider == null)
        {
            hudLine = "BodyGUIOverlay: no provider assigned.";
            return;
        }

        if (fiTrackedId == null && fiHeadCenter == null && fiLocked == null)
        {
            CacheMembers();
        }

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            hudLine = "BodyGUIOverlay: no camera.";
            return;
        }

        int trackedId = ReadInt(dataProvider, fiTrackedId, -1);
        Vector3 head = ReadVector3(dataProvider, fiHeadCenter);
        bool locked = ReadBool(dataProvider, fiLocked, trackedId >= 0);

        float dist = -1f;
        if (trackedId >= 0)
        {
            dist = Vector3.Distance(cam.transform.position, head);
        }

        string idStr = trackedId >= 0 ? trackedId.ToString() : "-";
        string distStr = dist >= 0f ? $"{dist:F2} m" : "-";
        string lockStr = locked ? "Yes" : "No";
        string mouseHint = locked ? $"Press {unlockKeyHint} to unlock"
                                  : "Click a person to select";

        hudLine = $"ID: {idStr} | Dist: {distStr} | Locked: {lockStr} | Mouse: {mouseHint}";
    }

    void OnGUI()
    {
        if (!showHUD) return;

        var style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperRight;
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        float pad = 12f;
        Rect r = new Rect(pad, pad, Screen.width - pad * 2f, 38f);
        GUI.Label(r, hudLine, style);
    }

    int ReadInt(object obj, FieldInfo fi, int defVal)
    {
        try
        {
            if (fi != null)
                return Convert.ToInt32(fi.GetValue(obj));
        }
        catch { }
        return defVal;
    }

    bool ReadBool(object obj, FieldInfo fi, bool defVal)
    {
        try
        {
            if (fi != null)
                return Convert.ToBoolean(fi.GetValue(obj));
        }
        catch { }
        return defVal;
    }

    Vector3 ReadVector3(object obj, FieldInfo fi)
    {
        try
        {
            if (fi != null)
            {
                object v = fi.GetValue(obj);
                if (v is Vector3 vv) return vv;
            }
        }
        catch { }
        return Vector3.zero;
    }
}

