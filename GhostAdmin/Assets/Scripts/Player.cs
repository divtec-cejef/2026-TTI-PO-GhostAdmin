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

    [SyncVar(hook = nameof(OnFacingLeftChanged))] bool facingLeft;
    [SyncVar(hook = nameof(OnAnimSpeedChanged))] float animSpeed;

void Awake()
{
    sr = GetComponent<SpriteRenderer>();
    animator = GetComponent<Animator>();
    Debug.Log("Animator trouvé : " + animator);
    Debug.Log("Controller : " + animator.runtimeAnimatorController);
}

    public override void OnStartLocalPlayer()
    {
        GetComponent<PlayerInput>().enabled = true;

        var vcam = FindObjectOfType<CinemachineCamera>();
        if (vcam != null)
            vcam.Target.TrackingTarget = transform;
        else
            Debug.LogWarning("Pas de CinemachineCamera dans la scène !");
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
        sr.flipX = left;
        animator.SetFloat("Speed", spd);  
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        rb.linearVelocity = moveInput * speed;
    }
}