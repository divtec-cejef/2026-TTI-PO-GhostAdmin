using Mirror;
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

    // État visuel partagé : modifié sur le serveur, diffusé à tous les clients
    [SyncVar(hook = nameof(OnFacingLeftChanged))] bool facingLeft;
    [SyncVar(hook = nameof(OnAnimSpeedChanged))] float animSpeed;

    void Awake()   // Awake et pas Start : Mirror peut appeler les hooks avant Start
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    public override void OnStartLocalPlayer()
    {
        GetComponent<PlayerInput>().enabled = true;   // seul MON personnage écoute le clavier

        // La caméra de la scène suit MON personnage
        CameraFollow follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
            follow.target = transform;
        else
            Debug.LogWarning("Pas de CameraFollow sur la Main Camera : la caméra ne suivra pas le joueur.");
    }

    public override void OnStartClient()
    {
        if (!isLocalPlayer)
            rb.bodyType = RigidbodyType2D.Kinematic;  // les autres sont placés par le réseau, pas par la physique
    }

    // Appelé par le PlayerInput (Unity Event Player/Move)
    public void Move(InputAction.CallbackContext ctx)
    {
        if (!isLocalPlayer) return;
        moveInput = ctx.ReadValue<Vector2>();

        bool left = sr.flipX;                 // par défaut on garde l'orientation actuelle
        if (moveInput.x > 0) left = false;
        else if (moveInput.x < 0) left = true;

        float spd = moveInput.magnitude;

        ApplyVisuals(left, spd);   // tout de suite chez moi, sans attendre le réseau
        CmdSetVisuals(left, spd);  // et on prévient le serveur pour les autres
    }

    [Command]   // exécuté sur le serveur, à la demande du joueur propriétaire
    void CmdSetVisuals(bool left, float spd)
    {
        facingLeft = left;   // modifier une SyncVar côté serveur = envoyée à tous les clients
        animSpeed = spd;
    }

    // Hooks : appelés sur chaque client quand la SyncVar change
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