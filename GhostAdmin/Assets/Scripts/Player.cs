using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour 
{
    public Rigidbody2D rb;
    public float speed;
    public LayerMask grondLayer;
    float horizontal;


    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(horizontal * speed, rb.linearVelocity.y);
    }

    public void Move(InputAction.CallbackContext context)
    {
        horizontal = context.ReadValue<Vector2>().x;
    }
}
