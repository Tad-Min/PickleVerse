using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Tooltip("Player speed")]
    public float Speed = 1.0f;

    [Tooltip("Hit Box")]
    public Collider HitBox;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            Debug.LogWarning("PlayerController need Rigidbody");
    }

    private void FixedUpdate()
    {
        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveInput.y = 1.0f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveInput.y = -1.0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveInput.x = 1.0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput.x = -1.0f;

        Vector3 direction = transform.forward * moveInput.y + transform.right * moveInput.x;
        direction = Vector3.ClampMagnitude(direction, 1f);
        Vector3 movement = direction * Speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position +  movement);

    }
}
