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
    PlayerRole role;

    // Dernières valeurs envoyées : on ne prévient le serveur que si quelque chose change.
    bool lastLeft;
    float lastSpd;

    [SyncVar(hook = nameof(OnFacingLeftChanged))] bool facingLeft;
    [SyncVar(hook = nameof(OnAnimSpeedChanged))] float animSpeed;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        role = GetComponent<PlayerRole>();
    }

    public override void OnStartLocalPlayer()
    {
        GetComponent<PlayerInput>().enabled = true;   // seul MON personnage écoute le clavier

        var vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
            vcam.Target.TrackingTarget = transform;
        else
            Debug.LogWarning("Pas de CinemachineCamera dans la scène !");
    }

    public override void OnStartClient()
    {
        if (!isLocalPlayer)
            rb.bodyType = RigidbodyType2D.Kinematic;   // les autres sont placés par le réseau
    }

    // Appelé par le PlayerInput (Unity Event Player/Move) : on ne fait que mémoriser l'entrée.
    public void Move(InputAction.CallbackContext ctx)
    {
        if (!isLocalPlayer) return;
        moveInput = ctx.ReadValue<Vector2>();
    }

    // Figé pendant le briefing et pendant une mission.
    bool Frozen => (role != null && !role.matchStarted) || QuestSession.IsOpen;

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        bool frozen = Frozen;

        // 1. Déplacement
        rb.linearVelocity = frozen ? Vector2.zero : moveInput * speed;

        // 2. Visuels calculés d'après le mouvement RÉEL : figé = pas d'animation de marche
        float spd = frozen ? 0f : moveInput.magnitude;
        bool left = sr.flipX;
        if (!frozen && moveInput.x > 0) left = false;
        else if (!frozen && moveInput.x < 0) left = true;

        if (left != lastLeft || !Mathf.Approximately(spd, lastSpd))
        {
            lastLeft = left;
            lastSpd = spd;
            ApplyVisuals(left, spd);                                   // tout de suite chez moi
            if (isActiveAndEnabled && NetworkClient.isConnected)
                CmdSetVisuals(left, spd);                              // et pour les autres
        }
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
}