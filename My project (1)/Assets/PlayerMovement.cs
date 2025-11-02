using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 5f;
    public float gravity = -9.81f;

    private Vector3 velocity;

    void Update()
    {
        if (Keyboard.current == null)
            return;

        // Read WASD input
        float x = 0f;
        float z = 0f;

        if (Keyboard.current.wKey.isPressed) z += 1f;
        if (Keyboard.current.sKey.isPressed) z -= 1f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;

        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move.normalized * speed * Time.deltaTime);

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
