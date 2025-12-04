using UnityEngine;

/// <summary>
/// Fullscreen RGB / Portal mode switcher
/// Simplified version without window frame control
/// </summary>
[RequireComponent(typeof(Camera))]
public class FullscreenRGBPortalSwitcher : MonoBehaviour
{
    [Header("ZED / RGB input")]
    public Camera zedCamera;            // The ZED RGB camera (e.g., Camera_Left)
    public RenderTexture zedRgbRT;      // ZED RGB render texture

    [Header("Portal render")]
    public RenderTexture portalRT;      // Optional (unused if only displaying RGB)

    [Header("Controls")]
    public KeyCode keyRGB = KeyCode.F1;       // Switch to RGB
    public KeyCode keyPortal = KeyCode.F2;    // Switch to Portal
    public KeyCode altRGB = KeyCode.Alpha1;   // Alternative RGB key (1)
    public KeyCode altPortal = KeyCode.Alpha2;// Alternative Portal key (2)
    public KeyCode toggleKey = KeyCode.Tab;   // Quick toggle between modes

    [Header("Tracking")]
    public SingleBodyTracker tracker;         // Used to lock onto selected ID
    public ImprovedPickerHelper pickerHelper; // Drag ImprovedPickerHelper here

    [Header("Layers (legacy - no longer needed with new picker)")]
    public string portalLayerName = "PortalScreen";
    public LayerMask pickMask = ~0;

    [Header("HUD")]
    public bool showSmallHint = true;
    public bool showPickRadius = true;        // Display pick radius circle in RGB

    private enum Mode { RGB, Portal }
    private Mode mode = Mode.Portal;

    private Camera _cam;
    private int _portalLayer = -1;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.targetTexture = null;

        if (!string.IsNullOrEmpty(portalLayerName))
            _portalLayer = LayerMask.NameToLayer(portalLayerName);

        // Auto-find picker if not assigned
        if (pickerHelper == null)
            pickerHelper = FindObjectOfType<ImprovedPickerHelper>();

        Log("Awake. Starting in Portal mode.");
    }

    void OnEnable()
    {
        if (_cam != null)
            _cam.targetDisplay = 0;
    }

    void Update()
    {
        // Switching input
        if (Pressed(keyRGB) || Pressed(altRGB)) SetRGBMode();
        if (Pressed(keyPortal) || Pressed(altPortal)) SetPortalMode();
        if (Pressed(toggleKey)) ToggleMode();

        // Picking only in RGB mode
        if (mode == Mode.RGB && Input.GetMouseButtonDown(0))
            TryPickOnRGB();
    }

    bool Pressed(KeyCode k) => Input.GetKeyDown(k);

    // ---- Mode switching ----

    public void SetRGBMode()
    {
        mode = Mode.RGB;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Log("Mode → RGB (fullscreen camera feed). Click near a person to select.");
    }

    public void SetPortalMode()
    {
        mode = Mode.Portal;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        Log("Mode → Portal (3D world view).");
    }

    void ToggleMode()
    {
        if (mode == Mode.RGB) SetPortalMode();
        else SetRGBMode();
    }

    // ---- Rendering ----

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // RGB mode shows fullscreen ZED camera feed
        if (mode == Mode.RGB && zedRgbRT != null)
        {
            Graphics.Blit(zedRgbRT, dst);
            return;
        }

        // Default behaviour
        Graphics.Blit(src, dst);
    }

    // ---- Picking ----

    /// <summary>
    /// Screen-space selection using ImprovedPickerHelper
    /// </summary>
    void TryPickOnRGB()
    {
        if (pickerHelper == null)
        {
            Log("ImprovedPickerHelper not assigned. Using legacy raycast method.");
            TryPickOnRGB_Legacy();
            return;
        }

        int selectedId = pickerHelper.TryPickByScreenSpace();

        if (selectedId >= 0)
        {
            if (tracker != null)
            {
                tracker.LockToId(selectedId);
                Log($"Locked to skeleton ID: {selectedId}");

                // Automatically switch to portal mode after selecting
                SetPortalMode();
            }
            else
            {
                Log("Tracker not assigned.");
            }
        }
        else
        {
            Log("No skeleton near the click position.");
        }
    }

    /// <summary>
    /// Legacy physics raycast method (kept as fallback)
    /// </summary>
    void TryPickOnRGB_Legacy()
    {
        if (zedCamera == null)
        {
            Log("ZED camera not assigned.");
            return;
        }

        Ray ray = zedCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 100f, pickMask))
        {
            Transform t = hit.transform;
            while (t != null && !t.name.StartsWith("Skeleton_ID_"))
                t = t.parent;

            if (t != null)
            {
                int id = ParseId(t.name);
                if (tracker != null && id >= 0)
                {
                    tracker.LockToId(id);
                    Log($"Picked skeleton ID: {id}");
                    SetPortalMode();
                }
                else Log("Tracker missing or invalid ID.");
            }
            else Log("Ray hit something but not a skeleton.");
        }
        else
        {
            Log("Raycast hit nothing (check colliders & layer mask).");
        }
    }

    int ParseId(string name)
    {
        int idx = name.LastIndexOf('_');
        if (idx >= 0 && int.TryParse(name.Substring(idx + 1), out int n))
            return n;
        return -1;
    }

    // ---- HUD / GUI ----

    void OnGUI()
    {
        if (!showSmallHint) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperRight
        };
        style.normal.textColor = Color.white;

        string txt = (mode == Mode.RGB)
            ? "RGB mode – Click near a person to select  (F2 / 2 / Tab → Portal)"
            : "Portal mode  (F1 / 1 / Tab → RGB)";

        GUI.Label(new Rect(10, 10, Screen.width - 20, 22), txt, style);

        // Draw selection radius circle in RGB mode
        if (mode == Mode.RGB && showPickRadius && pickerHelper != null)
        {
            Vector2 mouse = Input.mousePosition;
            mouse.y = Screen.height - mouse.y; // GUI Y-axis is inverted

            float radius = pickerHelper.screenPickRadius;

            DrawCircle(mouse, radius, Color.green);

            GUIStyle tip = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            tip.normal.textColor = Color.green;

            GUI.Label(
                new Rect(mouse.x - 100, mouse.y + radius + 10, 200, 20),
                $"Pick radius: {radius}px",
                tip
            );
        }
    }

    /// <summary>
    /// Draws a circle using short line segments (for GUI)
    /// </summary>
    void DrawCircle(Vector2 center, float radius, Color color)
    {
        int segments = 32;
        float step = 360f / segments;

        Color prev = GUI.color;
        GUI.color = color;

        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step * Mathf.Deg2Rad;
            float a2 = (i + 1) * step * Mathf.Deg2Rad;

            Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            Vector2 p2 = center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * radius;

            DrawLine(p1, p2, color, 2f);
        }

        GUI.color = prev;
    }

    void DrawLine(Vector2 p1, Vector2 p2, Color color, float width)
    {
        Color prev = GUI.color;
        GUI.color = color;

        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
        float length = Vector2.Distance(p1, p2);

        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y - width / 2, length, width), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-angle, p1);

        GUI.color = prev;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void Log(string msg)
    {
        Debug.Log($"[RGBPortal] {msg}");
    }
}