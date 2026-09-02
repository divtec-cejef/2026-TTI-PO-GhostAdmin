using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour 
{
    public Rigidbody2D rb;
    public float speed;
    public LayerMask grondLayer;
    Vector2 movement;
    SpriteRenderer sr;
    Animator animator;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = movement * speed;
        animator.SetFloat("Speed", movement.magnitude);
    }

    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
        if (movement.x > 0)
        {
            sr.flipX = false;
        }
        else if (movement.x < 0)
        {
            sr.flipX = true;
        }
    }
}
