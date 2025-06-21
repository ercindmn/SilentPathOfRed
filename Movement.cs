using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem;
using static Collision;

public class Movement : MonoBehaviour
{
    private Collision coll;
    [HideInInspector] public Rigidbody2D rb;
    private AnimationScript anim;
    private bool onOnewayPlatform;

    [Header("Stats")]
    public float speed = 10;
    public float jumpForce = 50;
    public float slideSpeed = 5;
    public float wallJumpLerp = 10;
    public float dashSpeed = 20;

    [Header("Booleans")]
    public bool canMove;
    public bool wallGrab;
    public bool isClimbing;
    public bool wallJumped;
    public bool wallSlide;
    public bool isDashing;

    [Header("Sound Effects")]
    public AudioClip jumpSound;
    public AudioClip dashSound;
    public AudioClip deathSound;

    [Header("Wall Climb Sounds")]
    public AudioClip[] stoneWallClimbSounds;
    public AudioClip[] metalWallClimbSounds;
    public AudioClip[] woodWallClimbSounds;
    public AudioClip[] waterWallClimbSounds;
    public AudioClip[] dirtWallClimbSounds;

    [Header("Wall Slide Sounds")]
    public AudioClip[] stoneWallSlideSounds;
    public AudioClip[] metalWallSlideSounds;
    public AudioClip[] woodWallSlideSounds;
    public AudioClip[] dirtWallSlideSounds;
    public AudioClip[] waterWallSlideSounds;

    [Header("Footstep Sounds")]
    public AudioClip[] waterWalkSounds;
    public AudioClip[] dirtWalkSounds;
    public AudioClip[] stoneWalkSounds;
    public AudioClip[] woodWalkSounds;
    public AudioClip[] metalWalkSounds;

    private AudioSource wallClimbSourceInstance;
    private AudioSource wallSlideSourceInstance;

    [Header("Landing Sounds")]
    public AudioClip[] dirtLandingSounds;
    public AudioClip[] stoneLandingSounds;
    public AudioClip[] waterLandingSounds;
    public AudioClip[] woodLandingSounds;
    public AudioClip[] metalLandingSounds;

    [Header("Better Jumping Settings")]
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    private bool isJumpPressed;

    [Header("Surface Detection")]
    public LayerMask waterLayer;
    public LayerMask dirtLayer;
    public LayerMask woodLayer;
    public LayerMask metalLayer;
    public LayerMask stoneLayer;
    private AudioClip surfaceClip;

    [Header("Corner Push")]
    [Tooltip("SlideSpeed'in kaç katı ile aşağı itme yapılacağını ayarlar")]
    public float cornerPushSpeedFactor = 0.2f;


    [Header("Others")]
    public float deathYLimit = -30f;
    public float footstepStartDelay = 0.1f;
    public float footstepsInterval = 0.4f;
    public float wallSoundInterval = 0.35f;
    private float wallClimbTimer;
    private float wallSlideTimer;
    private float stepTimer = 0f;
    private bool wasClimbingLastFrame = false;
    public bool isDead = false;
    private bool wasWalking = false;
    public bool groundTouch;
    public bool hasDashed;
    public int side = 1;
    public Vector2 moveInput;
    public PlayerControls controls;
    private SurfaceType lastSurfaceType = SurfaceType.Other;
    public Transform playerTransform;
    public float walkSoundVolume = 1f;
    [HideInInspector] public bool isOnJumpPad = false;

    [Header("Effects")]
    public TrailRenderer trailRenderer;
    public ParticleSystem dashParticle, jumpParticle, wallJumpParticle, slideParticle, deathParticle, walkParticle, waterSplashParticle;

    void Start()
    {
        coll = GetComponent<Collision>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<AnimationScript>();
        trailRenderer.emitting = false;
        if (playerTransform == null)
        {
            playerTransform = transform;
        }

    }
    private void Awake()
    {
        controls = new PlayerControls();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        controls.Player.Jump.performed += ctx => {
            isJumpPressed = true;
            JumpAction();
        };
        controls.Player.Jump.canceled += ctx => isJumpPressed = false;
        controls.Player.Dash.performed += ctx => DashAction();
        controls.Player.Grab.performed += ctx => WallGrabAction(true);
        controls.Player.Grab.canceled += ctx => WallGrabAction(false);
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }
    private void OnDisable()
    {
        controls.Player.Disable();
        StopWallSounds();
    }
    void FixedUpdate()
    {
        if (isDead) return;

        if (canMove && !wallGrab)
        {
            float moveX = moveInput.x * speed;
            rb.velocity = new Vector2(moveX, rb.velocity.y);
        }

        if (!groundTouch && coll.onGround)
        {
            GroundTouch();
            groundTouch = true;
        }
        else if (groundTouch && !coll.onGround)
        {
            groundTouch = false;
        }

        if (coll.onGround && rb.gravityScale == 0)
        {
            rb.gravityScale = 3f;
        }

        BetterJumping();
    }

    void Update()
    {
        if (transform.position.y < deathYLimit && !isDead)
        {
            Die();
            return;
        }

        if (isDead) return;

        moveInput = controls.Player.Move.ReadValue<Vector2>();
        float verticalVelocity = rb.velocity.y;
        anim.SetHorizontalMovement(moveInput.x, moveInput.y, verticalVelocity);
        Walk(moveInput);

        // Ayak sesleri
        AudioClip[] currentFootsteps = null;
        if (coll.onGround)
        {
            SurfaceType currentSurface = GetCurrentSurface();
            switch (currentSurface)
            {
                case SurfaceType.Dirt: currentFootsteps = dirtWalkSounds; break;
                case SurfaceType.Stone: currentFootsteps = stoneWalkSounds; break;
                case SurfaceType.Water: currentFootsteps = waterWalkSounds; break;
                case SurfaceType.Wood: currentFootsteps = woodWalkSounds; break;
                case SurfaceType.Metal: currentFootsteps = metalWalkSounds; break;
            }

            if (currentSurface != lastSurfaceType)
            {
                Debug.Log("Current Surface: " + currentSurface);
                lastSurfaceType = currentSurface;
            }
        }

        if (wasWalking)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                walkParticle.Play();
                if (currentFootsteps != null && currentFootsteps.Length > 0)
                {
                    int randomIndex = Random.Range(0, currentFootsteps.Length);
                    SoundFxManager.instance.PlaySoundFxClip(currentFootsteps[randomIndex], transform, walkSoundVolume);
                }
                stepTimer = footstepsInterval;
            }
        }
        else stepTimer = 0f;

        // Wall grab bırakma
        if ((coll.onGround || onOnewayPlatform) && wallGrab)
        {
            wallGrab = false;
            rb.gravityScale = 3f;
        }

        // Wall grab tetikleme
        if (!coll.onGround && coll.onWall && controls.Player.Grab.ReadValue<float>() > 0.5f && canMove)
        {
            wallGrab = true;
            Flip(-coll.wallSide); // Duvara bak
        }

        isClimbing = coll.onWall && verticalVelocity > 0.1f && !coll.onGround;
        if (coll.onGround && wasClimbingLastFrame)
        {
            rb.position += new Vector2(side * 0.12f, 0.05f);
        }
        wasClimbingLastFrame = isClimbing;

        if (coll.onGround && !isDashing) wallJumped = false;
        SurfaceType wallSurface = coll.currentWallSurfaceType;
        // --- WALL‑GRAB MODE ---
        if (wallGrab && !isDashing)
        {
            Vector2 dir = coll.onRightWall ? Vector2.right : Vector2.left;

            // 1) If we can climb and the player is holding up, do that *first*:
            if (coll.canClimbToTop && moveInput.y > 0.1f)
            {
                Debug.Log("⛰️ Tepeye çıkış tetiklendi!");
                anim.SetTrigger("ClimbToTop");
                wallGrab = false;
                isClimbing = false;
                int boostDir = -coll.wallSide;
                rb.position += new Vector2(boostDir * 0.025f, 0.025f);
                rb.velocity = Vector2.zero;
                rb.velocity = new Vector2(boostDir * 1.5f, 3f);
                Flip(boostDir);
                rb.gravityScale = 3f;
                return;
            }

            // 2) Only if we're at the corner *and* NOT still trying to climb, slide a bit:
            if (coll.isAtCornerLedge && moveInput.y <= 0.1f)
            {
                float cornerPushSpeed = slideSpeed * cornerPushSpeedFactor;
                rb.velocity = new Vector2(rb.velocity.x, -cornerPushSpeed);
                anim.SetTrigger("WallSlide");
                slideParticle.Play();
                return;
            }

            // 3) If we fall out of either of those, do your normal grab behavior:
            if (!coll.onWall)
            {
                wallGrab = false;
                rb.gravityScale = 3f;
                return;
            }

            rb.gravityScale = 0f;
            float climbSpeed = moveInput.y * (moveInput.y > 0 ? 0.5f : 1f) * speed;
            rb.velocity = new Vector2(0f, climbSpeed);


            SurfaceType wallsurface = coll.currentWallSurfaceType;
            wallClimbTimer -= Time.deltaTime;
            wallSlideTimer -= Time.deltaTime;

            if (rb.velocity.y < -0.1f && wallSlideTimer <= 0f)
            {
                AudioClip[] slideSounds = GetWallSlideSounds(wallSurface);
                if (slideSounds != null && slideSounds.Length > 0)
                {
                    AudioClip selected = slideSounds[Random.Range(0, slideSounds.Length)];
                    SoundFxManager.instance.PlaySoundFxClip(selected, transform);
                    wallSlideTimer = wallSoundInterval;
                }
            }

            if (isClimbing && wallClimbTimer <= 0f)
            {
                AudioClip[] climbSounds = GetWallClimbSounds(wallSurface);
                if (climbSounds != null && climbSounds.Length > 0)
                {
                    AudioClip selected = climbSounds[Random.Range(0, climbSounds.Length)];
                    SoundFxManager.instance.PlaySoundFxClip(selected, transform);
                    wallClimbTimer = wallSoundInterval;
                }
            }

            if (controls.Player.Jump.triggered)
            {
                JumpAction();
            }

            WallParticle(moveInput.y);
            return;
        }

        rb.gravityScale = 3f;

        if (moveInput.x > 0 && side != 1) Flip(1);
        else if (moveInput.x < 0 && side != -1) Flip(-1);

        if (!coll.onWall || !wallGrab || coll.onGround)
        {
            StopWallSounds();
        }
    }
public void SetJumpPadState(bool value)
    {
        isOnJumpPad = value;
    }

    void GroundTouch()
    {
        hasDashed = false;
        isDashing = false;
        jumpParticle.Play();

        if (!isDead)
        {
            if (IsOnSurface(waterLayer))
            {
                surfaceClip = waterLandingSounds[Random.Range(0, waterLandingSounds.Length)];
            }
            else if (IsOnSurface(stoneLayer))
            {
                surfaceClip = stoneLandingSounds[Random.Range(0, stoneLandingSounds.Length)];
            }
            else if (IsOnSurface(dirtLayer))
            {
                surfaceClip = dirtLandingSounds[Random.Range(0, dirtLandingSounds.Length)];
            }
            else if (IsOnSurface(woodLayer))
            {
                surfaceClip = woodLandingSounds[Random.Range(0, woodLandingSounds.Length)];
            }
            else if (IsOnSurface(metalLayer))
            {
                surfaceClip = metalLandingSounds[Random.Range(0, metalLandingSounds.Length)];
            }
            else
            {
                surfaceClip = null;
            }


            if (surfaceClip != null)
            {
                SoundFxManager.instance.PlaySoundFxClip(surfaceClip, transform);
            }
        }
    }
    void JumpAction()
    {
        if (!canMove)
        {
            Debug.Log("Zıplama engellendi: canMove false");
            return;
        }

        if (isOnJumpPad)
        {
            Debug.Log("Zıplama engellendi: JumpPad aktif");
            return;
        }

        anim.SetTrigger("jump");

        if (coll.onGround)
        {
            Jump(Vector2.up, false);
        }
        else if (coll.onWall && wallGrab)
        {
            WallJump();
        }
    }

    private void BetterJumping()
    {
        if (isDead || isDashing || isOnJumpPad || coll.onGround) return;

        // Hızlı düşüş
        if (rb.velocity.y < 0)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        // Kısa zıplama (zıplama yarıda bırakıldığında)
        else if (rb.velocity.y > 0 && !isJumpPressed)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void WallGrabAction(bool grabbing)
    {
        if (!canMove) return;

        if (coll.onWall && canMove)
        {
            wallGrab = grabbing;

            if (grabbing && rb.velocity.y < 0)
            {
                wallGrab = true;
            }
        }
        else if (!coll.onWall && wallGrab)
        {
            // Ray duvarı kaçırsa bile burası artık burayı kontrol ediyor
            wallGrab = false;
            rb.gravityScale = 3f;
        }
    }

    void DashAction()
    {
        if (isDead || !canMove || hasDashed || (moveInput.x == 0 && moveInput.y == 0) || isDashing)
            return; // prevent dash while dashing
        Dash(moveInput.x, moveInput.y);
    }
    private void Dash(float x, float y)
    {
        if (isDead || !canMove) return;

        if (Camera.main != null)
        {
            Camera.main.transform.DOComplete();
            Camera.main.transform.DOShakePosition(.2f, .5f, 14, 90, false, true);
        }
        RippleEffect rippleEffect = FindObjectOfType<RippleEffect>();
        if (rippleEffect != null)
        {
            rippleEffect.Emit(Camera.main.WorldToViewportPoint(transform.position));
        }

        hasDashed = true;
        anim.SetTrigger("dash");

        if (rb != null)
        {
            rb.velocity = Vector2.zero;

            Vector2 dashDirection = new Vector2(x, y).normalized;

            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                dashDirection = new Vector2(x, 0).normalized;
            }
            else if (Mathf.Abs(y) > Mathf.Abs(x))
            {
                dashDirection = new Vector2(0, y).normalized;
            }

            rb.velocity = dashDirection * dashSpeed;
        }
        if (dashSound != null && !isDead)
        {
            SoundFxManager.instance.PlaySoundFxClip(dashSound, transform);
        }

        StartCoroutine(DashWait());
    }

    IEnumerator DashWait()
    {
        FindObjectOfType<GhostTrail>().ShowGhost();
        StartCoroutine(GroundDash());
        DOVirtual.Float(14, 0, .8f, RigidbodyDrag);

        dashParticle.Play();
        rb.gravityScale = 0;
        wallJumped = true;
        isDashing = true;

        yield return new WaitForSeconds(.3f);

        dashParticle.Stop();
        rb.gravityScale = 3;
        wallJumped = false;
        isDashing = false;
    }

    IEnumerator GroundDash()
    {
        yield return new WaitForSeconds(.5f);
        if (coll.onGround) hasDashed = false;
    }

    private void WallJump()
    {
        if (!wallGrab) return;
        Flip(-side);
        StartCoroutine(DisableMovement(.1f));
        Vector2 wallDir = coll.onRightWall ? Vector2.left : Vector2.right;
        rb.velocity = Vector2.zero;
        rb.velocity += new Vector2(wallDir.x * 5, 8);
        if (jumpSound != null)
            SoundFxManager.instance.PlaySoundFxClip(jumpSound, transform);
        wallJumped = true;
        wallGrab = false;
        wallJumpParticle.Play();
    }

    private void Walk(Vector2 moveInput)
    {
        bool isCurrentlyWalking = coll.onGround && Mathf.Abs(moveInput.x) > 0.1f && canMove;

        if (isCurrentlyWalking && !wasWalking)
        {
            stepTimer = footstepStartDelay;
        }

        if (isCurrentlyWalking)
        {
            wasWalking = true;

            SurfaceType surface = GetCurrentSurface();
            AudioClip[] currentFootsteps = null;

            switch (surface)
            {
                case SurfaceType.Dirt: currentFootsteps = dirtWalkSounds; break;
                case SurfaceType.Stone: currentFootsteps = stoneWalkSounds; break;
                case SurfaceType.Water: currentFootsteps = waterWalkSounds; break;
                case SurfaceType.Metal: currentFootsteps = metalWalkSounds; break;
                case SurfaceType.Wood: currentFootsteps = woodWalkSounds; break;
            }

            if (stepTimer <= 0f && currentFootsteps != null && currentFootsteps.Length > 0)
            {
                int index = Random.Range(0, currentFootsteps.Length);
                walkParticle.Play();
                SoundFxManager.instance.PlaySoundFxClip(currentFootsteps[index], transform, walkSoundVolume);
                stepTimer = footstepsInterval;
            }
        }
        else
        {
            wasWalking = false;
            stepTimer = 0f;
        }
    }


    private void Jump(Vector2 dir, bool wall)
    {
        ParticleSystem particle = wall ? wallJumpParticle : jumpParticle;
        rb.velocity = new Vector2(rb.velocity.x, 0);
        rb.velocity += dir * jumpForce;
        particle.Play();

        if (jumpSound != null)
            SoundFxManager.instance.PlaySoundFxClip(jumpSound, transform);
    }
    IEnumerator DisableMovement(float time)
    {
        canMove = false;
        yield return new WaitForSeconds(time);
        canMove = true;

        // Ekstra güvenlik için gravityScale sıfırsa geri aç
        if (rb.gravityScale == 0)
            rb.gravityScale = 3;
    }
    void RigidbodyDrag(float x)
    {
        rb.drag = x;
    }
    void WallParticle(float vertical)
    {
        var main = slideParticle.main;
        slideParticle.transform.parent.localScale = new Vector3(coll.onRightWall ? 1 : -1, 1, 1);
        main.startColor = (wallSlide || (wallGrab && vertical < 0)) ? Color.white : Color.clear;
    }
    public void Flip(int direction)
    {
        if (side != direction)
        {
            side = direction;
            transform.localScale = new Vector3(side, 1, 1);
        }
    }


    public void Die()
    {
        if (isDead) return;
        isDead = true;
        if (deathSound != null)
        {
            SoundFxManager.instance.PlaySoundFxClip(deathSound, transform);
        }
        if (deathParticle != null)
        {
            deathParticle.Play();
        }
        anim.ResetTrigger("jump");
        anim.ResetTrigger("dash");
        anim.ResetTrigger("wallGrab");
        anim.SetTrigger("dead");
        rb.velocity = Vector2.zero;
        rb.simulated = false;
        canMove = false;

        StartCoroutine(Respawn());
        StopWallSounds();

    }
    public IEnumerator Respawn()
    {
        yield return new WaitForSeconds(1f);
        anim.ResetTrigger("dead");
        anim.Play("Idle");
        Transform respawnPoint = GameObject.FindWithTag("Respawn")?.transform;
        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
        rb.simulated = true;
        isDead = false;
        canMove = true;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("OnewayPlatform"))
        {
            onOnewayPlatform = true;
        }
        if (collision.collider.CompareTag("Trap"))
        {
            Die();
        }
    }
    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("OnewayPlatform"))
        {
            onOnewayPlatform = false;
        }
    }
    private bool IsOnSurface(LayerMask layer)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1f, layer);
        return hit.collider != null;
    }

    public void PlayWalkSound(string surfaceType)
    {
        AudioClip clip = null;

        switch (surfaceType)
        {
            case "Dirt":
                clip = dirtWalkSounds[Random.Range(0, dirtWalkSounds.Length)];
                break;
            case "Stone":
                clip = stoneWalkSounds[Random.Range(0, stoneWalkSounds.Length)];
                break;
            case "Water":
                clip = waterWalkSounds[Random.Range(0, waterWalkSounds.Length)];
                break;
            case "Metal":
                clip = waterWalkSounds[Random.Range(0, metalWalkSounds.Length)];
                break;
            case "Wood":
                clip = waterWalkSounds[Random.Range(0, woodWalkSounds.Length)];
                break;
            default:
                clip = dirtWalkSounds[Random.Range(0, dirtWalkSounds.Length)];
                break;
        }

        FindObjectOfType<SoundFxManager>().PlaySoundFxClip(clip, transform);
    }
    private SurfaceType GetCurrentSurface()
    {
        if (IsOnSurface(waterLayer)) return SurfaceType.Water;
        if (IsOnSurface(stoneLayer)) return SurfaceType.Stone;
        if (IsOnSurface(dirtLayer)) return SurfaceType.Dirt;
        if (IsOnSurface(woodLayer)) return SurfaceType.Wood;
        if (IsOnSurface(metalLayer)) return SurfaceType.Metal;
        return SurfaceType.Other;
    }
    private AudioClip[] GetWallClimbSounds(SurfaceType type)
    {
        switch (type)
        {
            case SurfaceType.Stone: return stoneWallClimbSounds;
            case SurfaceType.Metal: return metalWallClimbSounds;
            case SurfaceType.Water: return waterWallClimbSounds;
            case SurfaceType.Dirt: return dirtWallClimbSounds;
            case SurfaceType.Wood: return woodWallClimbSounds;
            default: return null;
        }
    }

    private AudioClip[] GetWallSlideSounds(SurfaceType type)
    {
        switch (type)
        {
            case SurfaceType.Stone: return stoneWallSlideSounds;
            case SurfaceType.Metal: return metalWallSlideSounds;
            case SurfaceType.Water: return waterWallSlideSounds;
            case SurfaceType.Dirt: return dirtWallSlideSounds;
            case SurfaceType.Wood: return woodWallSlideSounds;
            default: return null;
        }
    }

    void StopWallSounds()
    {
        if (wallClimbSourceInstance != null)
        {
            SoundFxManager.instance.StopLoopingSound(wallClimbSourceInstance);
            wallClimbSourceInstance = null;
        }

        if (wallSlideSourceInstance != null)
        {
            SoundFxManager.instance.StopLoopingSound(wallSlideSourceInstance);
            wallSlideSourceInstance = null;
        }
    }
}