using UnityEngine;

/// <summary>
/// Simple Window Frame Generator with Mode Control
/// Only visible in Portal mode, hidden in RGB mode
/// </summary>
public class SimpleWindowFrame : MonoBehaviour
{
    [Header("Window Settings")]
    [Tooltip("Camera that will see through the window")]
    public Camera targetCamera;

    [Tooltip("Distance from camera to window frame")]
    public float distanceFromCamera = 1.5f;

    [Tooltip("Window opening size (width, height)")]
    public Vector2 windowSize = new Vector2(2f, 1.5f);

    [Tooltip("Frame thickness")]
    public float frameThickness = 0.2f;

    [Header("Appearance")]
    public Color frameColor = new Color(0.3f, 0.3f, 0.3f);
    public Material frameMaterial;

    [Header("Visibility Control")]
    [Tooltip("Only show frame in Portal mode (not RGB mode)")]
    public bool onlyShowInPortalMode = true;

    private GameObject frameParent;
    private bool isVisible = true;

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            Debug.LogWarning("[SimpleWindowFrame] Target camera not assigned, using Camera.main");
        }

        if (targetCamera == null)
        {
            Debug.LogError("[SimpleWindowFrame] No camera found! Cannot create window frame.");
            return;
        }

        CreateWindowFrame();

        // Start with frame visible
        SetFrameVisible(true);
    }

    void CreateWindowFrame()
    {
        // Create parent object
        frameParent = new GameObject("WindowFrame");
        frameParent.transform.SetParent(targetCamera.transform);
        frameParent.transform.localPosition = Vector3.forward * distanceFromCamera;
        frameParent.transform.localRotation = Quaternion.identity;

        Debug.Log($"[SimpleWindowFrame] Creating frame at local position {frameParent.transform.localPosition}");
        Debug.Log($"[SimpleWindowFrame] Frame world position: {frameParent.transform.position}");

        // Create 4 frame pieces (top, bottom, left, right)

        // Top bar
        CreateFramePiece("TopBar",
            new Vector3(0, windowSize.y / 2 + frameThickness / 2, 0),
            new Vector3(windowSize.x + frameThickness * 2, frameThickness, 0.1f));

        // Bottom bar
        CreateFramePiece("BottomBar",
            new Vector3(0, -windowSize.y / 2 - frameThickness / 2, 0),
            new Vector3(windowSize.x + frameThickness * 2, frameThickness, 0.1f));

        // Left bar
        CreateFramePiece("LeftBar",
            new Vector3(-windowSize.x / 2 - frameThickness / 2, 0, 0),
            new Vector3(frameThickness, windowSize.y, 0.1f));

        // Right bar
        CreateFramePiece("RightBar",
            new Vector3(windowSize.x / 2 + frameThickness / 2, 0, 0),
            new Vector3(frameThickness, windowSize.y, 0.1f));

        Debug.Log("[SimpleWindowFrame] Window frame created successfully!");
    }

    void CreateFramePiece(string name, Vector3 localPos, Vector3 size)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(frameParent.transform);
        piece.transform.localPosition = localPos;
        piece.transform.localScale = size;

        // Set material
        Renderer renderer = piece.GetComponent<Renderer>();
        if (frameMaterial != null)
        {
            renderer.material = frameMaterial;
        }
        else
        {
            renderer.material.color = frameColor;
        }

        // Remove collider (we don't need physics)
        Destroy(piece.GetComponent<Collider>());
    }

    /// <summary>
    /// Public method to show/hide the frame based on mode
    /// Call this from your mode switcher script
    /// </summary>
    public void SetFrameVisible(bool visible)
    {
        if (frameParent != null)
        {
            frameParent.SetActive(visible);
            isVisible = visible;
            Debug.Log($"[SimpleWindowFrame] Frame visibility set to: {visible}");
        }
    }

    /// <summary>
    /// Call this when switching to RGB mode
    /// </summary>
    public void OnRGBMode()
    {
        if (onlyShowInPortalMode)
        {
            SetFrameVisible(false);
            Debug.Log("[SimpleWindowFrame] RGB mode - hiding frame");
        }
    }

    /// <summary>
    /// Call this when switching to Portal mode
    /// </summary>
    public void OnPortalMode()
    {
        SetFrameVisible(true);
        Debug.Log("[SimpleWindowFrame] Portal mode - showing frame");
    }

    void OnDestroy()
    {
        if (frameParent != null)
            Destroy(frameParent);
    }

    void OnValidate()
    {
        // Recreate frame when parameters change in editor
        if (Application.isPlaying && frameParent != null)
        {
            Destroy(frameParent);
            CreateWindowFrame();
            SetFrameVisible(isVisible);
        }
    }
}