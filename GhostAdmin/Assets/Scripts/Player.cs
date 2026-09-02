using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour 
{
    public Rigidbody2D rb;
    public float speed;
    public LayerMask grondLayer;
    Vector2 movement;

    private void FixedUpdate()
    {
        rb.linearVelocity = movement * speed;
    }

    public void Move(InputAction.CallbackContext context)
    {
        movement = context.ReadValue<Vector2>(); // x = horizontal, y = vertical
    }
}
