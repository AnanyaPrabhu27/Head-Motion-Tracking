using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public Transform playerBody;

    private float xRotation = 0f;

    void Update()
    {
        // Make sure the mouse is available
        if (Mouse.current == null)
            return;

        // Read mouse movement from the new Input System
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
        float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;

        // Vertical rotation (looking up/down)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal rotation (turning left/right)
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
