using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("Settings")]
    public Transform playerBody;           // The player object to rotate horizontally
    public float mouseSensitivity = 50f;  // Mouse speed
    public float verticalClamp = 50f;     // Max up/down angle

    private float xRotation = 0f;

    void Start()
    {
        // Initialize xRotation to current camera pitch
        xRotation = transform.localEulerAngles.x;
        if (xRotation > 180f) xRotation -= 360f;

        // Lock and hide cursor at start
        LockCursor(true);
    }

    void Update()
    {
        if (Mouse.current == null || playerBody == null) return;

        // Ensure cursor stays locked
        if (Cursor.lockState != CursorLockMode.Locked && !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            LockCursor(true);
        }

        // Toggle cursor lock/visibility with Escape
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            LockCursor(!locked);
        }

        // Read mouse movement
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Scale movement (slower)
        float mouseX = mouseDelta.x * mouseSensitivity * 0.02f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.02f;

        // Vertical rotation (look up/down)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -verticalClamp, verticalClamp);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal rotation (turn player body)
        playerBody.Rotate(Vector3.up * mouseX);
    }

    private void LockCursor(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}
