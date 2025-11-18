using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 显示眼睛与鼻子的小标记（无大框）。
/// - 鼠标点击选人（依赖 SingleTargetDetection 选中的 ID，或自行点击）
/// - L 键锁定/解锁（依赖 SingleTargetDetection）
/// - 自动禁用 ZED3DObjectVisualizer 以避免显示大框
/// - HUD 位置可调，默认下移避免与 SingleTargetDetection 冲突
/// </summary>
public class FaceMarkerOnly : MonoBehaviour
{
    [Header("References")]
    public ZEDManager zedManager;
    public ZED3DObjectVisualizer visualizer;
    public SingleTargetDetection tracker; // 推荐与 SingleTargetDetection 配合

    [Header("Indices (COCO-18)")]
    public int noseIndex = 0;       // NOSE
    public int leftEyeIndex = 15;   // LEFT_EYE
    public int rightEyeIndex = 14;  // RIGHT_EYE

    [Header("Marker Settings")]
    public float markerRadius = 0.04f; // 球半径
    public Color noseColor = Color.red;
    public Color leftEyeColor = Color.green;
    public Color rightEyeColor = Color.blue;

    [Header("UI")]
    public bool showHUD = true;
    public Vector2 hudStart = new Vector2(10, 80); // 下移，避免和 SingleTargetDetection 重叠

    // State
    private int? selectedId = null;
    private List<DetectedBody> lastBodies = new List<DetectedBody>();

    // Markers
    private GameObject noseMarker;
    private GameObject leftEyeMarker;
    private GameObject rightEyeMarker;

    // 选择用相机（仅在本脚本需要独立点击时用）
    private Camera selCam;

    void Start()
    {
        if (!zedManager) zedManager = FindAnyObjectByType<ZEDManager>();
        if (!visualizer) visualizer = FindAnyObjectByType<ZED3DObjectVisualizer>();
        if (!tracker) tracker = FindAnyObjectByType<SingleTargetDetection>();

        if (!zedManager)
        {
            Debug.LogError("[FaceMarkerOnly] ZEDManager not found.");
            enabled = false;
            return;
        }

        // 禁用可视化器组件，彻底不画大框
        if (visualizer) visualizer.enabled = false;

        // 选择相机：优先 Camera.main，兜底 ZED 左相机
        selCam = Camera.main;
        if (!selCam)
        {
            var lt = zedManager.GetLeftCameraTransform();
            selCam = lt ? lt.GetComponent<Camera>() : null;
        }

        zedManager.OnZEDReady += OnZEDReady;
        zedManager.OnBodyTracking += OnBodyTracking;

        // 创建标记球
        noseMarker = CreateMarker("NoseMarker", noseColor);
        leftEyeMarker = CreateMarker("LeftEyeMarker", leftEyeColor);
        rightEyeMarker = CreateMarker("RightEyeMarker", rightEyeColor);

        HideMarkers();
    }

    private void OnZEDReady()
    {
        if (!zedManager.IsBodyTrackingRunning)
            zedManager.StartBodyTracking();

        if (!selCam)
        {
            selCam = Camera.main;
            if (!selCam)
            {
                var lt = zedManager.GetLeftCameraTransform();
                selCam = lt ? lt.GetComponent<Camera>() : null;
            }
        }
    }

    private void OnDestroy()
    {
        if (zedManager)
        {
            zedManager.OnZEDReady -= OnZEDReady;
            zedManager.OnBodyTracking -= OnBodyTracking;
        }
    }

    private void OnBodyTracking(BodyTrackingFrame frame)
    {
        lastBodies = frame.GetFilteredObjectList(true, true, true);
    }

    void Update()
    {
        // 如果配合了 SingleTargetDetection，则优先用它的选中 ID
        if (tracker)
        {
            int tid = tracker.GetCurrentTargetId();
            selectedId = tid >= 0 ? (int?)tid : null;
        }
        else
        {
            // 本脚本独立点击选人（可选）
            if (Input.GetMouseButtonDown(0)) ClickPick();
        }

        if (selectedId.HasValue && lastBodies.Count > 0)
        {
            var body = lastBodies.FirstOrDefault(b => b.id == selectedId.Value);
            if (body != null)
            {
                UpdateMarkers(body);
            }
            else
            {
                HideMarkers();
            }
        }
        else
        {
            HideMarkers();
        }
    }

    private void ClickPick()
    {
        if (lastBodies.Count == 0 || selCam == null) return;

        Vector2 mouse = Input.mousePosition;
        float best = float.MaxValue;
        int? bestId = null;
        const float pickRadius = 80f;

        foreach (var b in lastBodies)
        {
            Vector3 wpos = b.Get3DWorldPosition();
            if (ZEDSupportFunctions.IsVector3NaN(wpos)) continue;

            Vector3 sp = selCam.WorldToScreenPoint(wpos);
            if (sp.z <= 0f) continue;

            float d = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestId = b.id;
            }
        }

        if (bestId.HasValue && best <= pickRadius * pickRadius)
        {
            selectedId = bestId.Value;
            Debug.Log("[FaceMarkerOnly] Selected ID: " + selectedId.Value);
        }
    }

    private void UpdateMarkers(DetectedBody body)
    {
        Vector3 p;

        if (TryGetKeypoint(body, noseIndex, out p))
        {
            noseMarker.transform.position = p;
            noseMarker.transform.localScale = Vector3.one * (markerRadius * 2f);
            noseMarker.SetActive(true);
        }
        else noseMarker.SetActive(false);

        if (TryGetKeypoint(body, leftEyeIndex, out p))
        {
            leftEyeMarker.transform.position = p;
            leftEyeMarker.transform.localScale = Vector3.one * (markerRadius * 2f);
            leftEyeMarker.SetActive(true);
        }
        else leftEyeMarker.SetActive(false);

        if (TryGetKeypoint(body, rightEyeIndex, out p))
        {
            rightEyeMarker.transform.position = p;
            rightEyeMarker.transform.localScale = Vector3.one * (markerRadius * 2f);
            rightEyeMarker.SetActive(true);
        }
        else rightEyeMarker.SetActive(false);
    }

    private void HideMarkers()
    {
        if (noseMarker) noseMarker.SetActive(false);
        if (leftEyeMarker) leftEyeMarker.SetActive(false);
        if (rightEyeMarker) rightEyeMarker.SetActive(false);
    }

    private bool TryGetKeypoint(DetectedBody body, int index, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (body == null || index < 0) return false;

        try
        {
            // 方式1：Get3DWorldKeypoints()
            var mAll = body.GetType().GetMethod("Get3DWorldKeypoints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (mAll != null)
            {
                var result = mAll.Invoke(body, null) as Vector3[];
                if (result != null && index < result.Length)
                {
                    var p = result[index];
                    if (!ZEDSupportFunctions.IsVector3NaN(p)) { pos = p; return true; }
                }
            }

            // 方式2：Get3DWorldKeypoint(int)
            var mOne = body.GetType().GetMethod("Get3DWorldKeypoint", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (mOne != null)
            {
                object r = mOne.Invoke(body, new object[] { index });
                if (r is Vector3 v && !ZEDSupportFunctions.IsVector3NaN(v)) { pos = v; return true; }
            }
        }
        catch { }

        return false;
    }

    private GameObject CreateMarker(string name, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(transform, false);

        // 移除碰撞体
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        // 颜色
        var mr = go.GetComponent<Renderer>();
        if (mr && mr.material) mr.material.color = color;

        return go;
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

    private void OnGUI()
    {
        if (!showHUD) return;

        float x = hudStart.x;
        float y = hudStart.y;
        float w = 420f;
        float h = 20f;
        float pad = 6f;

        // 底板
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

        string idStr = selectedId.HasValue ? selectedId.Value.ToString() : "None";
        DrawShadowedLabel(new Rect(x, y, w, h), "Face Marker - ID: " + idStr, style);
        y += h + pad;
        DrawShadowedLabel(new Rect(x, y, w, h), "Click to select (or use SingleTargetDetection)", style);
        y += h + pad;
        DrawShadowedLabel(new Rect(x, y, w, h), "Bodies: " + (lastBodies != null ? lastBodies.Count : 0), style);
    }
}
