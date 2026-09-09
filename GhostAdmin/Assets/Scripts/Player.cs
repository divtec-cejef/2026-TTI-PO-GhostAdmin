using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour
{
    public Rigidbody2D rb;
    public float speed = 3f;
    public LayerMask grondLayer;

    Vector2 moveInput;
    SpriteRenderer sr;
    Animator animator;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    public override void OnStartClient()
    {
        if (!isLocalPlayer)
            rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void Move(InputAction.CallbackContext ctx)
    {
        if (!isLocalPlayer) return;

        moveInput = ctx.ReadValue<Vector2>();

        bool left = sr.flipX;
        if (moveInput.x > 0) left = false;
        else if (moveInput.x < 0) left = true;

        float spd = moveInput.magnitude;

        ApplyVisuals(left, spd);

        if (isActiveAndEnabled && NetworkClient.isConnected)
            CmdSetVisuals(left, spd);
    }

    [Command]
    void CmdSetVisuals(bool left, float spd)
    {
        facingLeft = left;
        animSpeed = spd;
    }

    void OnFacingLeftChanged(bool oldValue, bool newValue) => sr.flipX = newValue;
    void OnAnimSpeedChanged(float oldValue, float newValue) => animator.SetFloat("Speed", newValue);

    void ApplyVisuals(bool left, float spd)
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