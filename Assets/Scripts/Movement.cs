using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Movement : MonoBehaviour
{
    #region Variables
    [Tooltip("The Input Manager component used to gather player input.")]
    public InputManager inputManager = null;

    private Collision coll;
    [HideInInspector]
    public Rigidbody2D rb;
    private AnimationScript anim;

    [Space]
    [Header("Stats")]
    [Tooltip("Player walk speed")]
    public float speed = 10;
    [Tooltip("Player jump force")]
    public float jumpForce = 50;
    [Tooltip("The player's sliding speed when clinging to the wall")]
    public float slideSpeed = 5;
    [Tooltip("Smooth speed when jumping off walls")]
    public float wallJumpLerp = 10;
    [Tooltip("Player dash speed")]
    public float dashSpeed = 20;

    [Space]
    [Header("Booleans")]
    public bool canMove;
    public bool wallGrab;
    public bool wallJumped;
    public bool wallSlide;
    public bool isDashing;

    [Space]
    [Tooltip("Check if the player has just touched the ground")]
    private bool groundTouch;
    [Tooltip("Check if the player has just dashed yet")]
    private bool hasDashed;
    [Tooltip("Player direction (1 == right, -1 == left)")]
    public int side = 1;

    [Space]
    [Header("Polish (Particle System)")]
    public ParticleSystem dashParticle;
    public ParticleSystem jumpParticle;
    public ParticleSystem wallJumpParticle;
    public ParticleSystem slideParticle;
    #endregion

    #region Input from Input Manager
    // The horizontal movement input collected from the input manager
    public float horizontalMovementInput
    {
        get
        {
            if (inputManager != null)
                return inputManager.horizontalMovement;
            else
                return 0;
        }
    }
    // The vertical movement input collected from the input manager
    public float verticalMovementInput
    {
        get
        {
            if (inputManager != null)
                return inputManager.verticalMovement;
            else
                return 0;
        }
    }
    // the jump input collected from the input manager
    public bool jumpInput
    {
        get
        {
            if (inputManager != null)
                return inputManager.jumpStarted;
            else
                return false;
        }
    }

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        coll = GetComponent<Collision>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<AnimationScript>();
        this.SetUpInputManager();
    }

    private void SetUpInputManager()
    {
        inputManager = InputManager.instance;
        if (inputManager == null)
        {
            Debug.LogError("There is no InputManager set up in the scene for the PlayerController to read from");
        }
    }

    // Update is called once per frame
    void Update()
    {
        //float x = Input.GetAxis("Horizontal");
        //float y = Input.GetAxis("Vertical");
        //float xRaw = Input.GetAxisRaw("Horizontal");
        //float yRaw = Input.GetAxisRaw("Vertical");

        Walk(new Vector2(horizontalMovementInput, verticalMovementInput));
        anim.SetHorizontalMovement(horizontalMovementInput, verticalMovementInput, rb.velocity.y);

        if (coll.onWall && Input.GetButton("Fire3") && canMove)
        {
            if(side != coll.wallSide)
                anim.Flip(side*-1);
            wallGrab = true;
            wallSlide = false;
        }

        if (Input.GetButtonUp("Fire3") || !coll.onWall || !canMove)
        {
            wallGrab = false;
            wallSlide = false;
        }

        if (coll.onGround && !isDashing)
        {
            wallJumped = false;
            GetComponent<BetterJumping>().enabled = true;
        }
        
        if (wallGrab && !isDashing)
        {
            rb.gravityScale = 0;
            if(horizontalMovementInput > .2f || horizontalMovementInput < -.2f)
            rb.velocity = new Vector2(rb.velocity.x, 0);

            float speedModifier = verticalMovementInput > 0 ? .5f : 1;

            rb.velocity = new Vector2(rb.velocity.x, verticalMovementInput * (speed * speedModifier));
        }
        else
        {
            rb.gravityScale = 3;
        }

        if(coll.onWall && !coll.onGround)
        {
            if (horizontalMovementInput != 0 && !wallGrab)
            {
                wallSlide = true;
                WallSlide();
            }
        }

        if (!coll.onWall || coll.onGround)
            wallSlide = false;

        if (Input.GetButtonDown("Jump"))
        {
            anim.SetTrigger("jump");

            if (coll.onGround)
                Jump(Vector2.up, false);
            if (coll.onWall && !coll.onGround)
                WallJump();
        }

        if (Input.GetButtonDown("Fire1") && !hasDashed)
        {
            if(horizontalMovementInput != 0 || verticalMovementInput != 0)
                Dash(horizontalMovementInput, verticalMovementInput);
        }

        if (coll.onGround && !groundTouch)
        {
            GroundTouch();
            groundTouch = true;
        }

        if(!coll.onGround && groundTouch)
        {
            groundTouch = false;
        }

        WallParticle(verticalMovementInput);

        if (wallGrab || wallSlide || !canMove)
            return;

        if(horizontalMovementInput > 0)
        {
            side = 1;
            anim.Flip(side);
        }
        if (horizontalMovementInput < 0)
        {
            side = -1;
            anim.Flip(side);
        }


    }

    void GroundTouch()
    {
        hasDashed = false;
        isDashing = false;

        side = anim.sr.flipX ? -1 : 1;

        jumpParticle.Play();
    }

    private void Dash(float x, float y)
    {
        Camera.main.transform.DOComplete();
        Camera.main.transform.DOShakePosition(.2f, .5f, 14, 90, false, true);
        FindObjectOfType<RippleEffect>().Emit(Camera.main.WorldToViewportPoint(transform.position));

        hasDashed = true;

        anim.SetTrigger("dash");

        rb.velocity = Vector2.zero;
        Vector2 dir = new Vector2(x, y).normalized;

        rb.velocity += dir.normalized * dashSpeed;
        StartCoroutine(DashWait());
    }

    IEnumerator DashWait()
    {
        FindObjectOfType<GhostTrail>().ShowGhost();
        StartCoroutine(GroundDash());
        DOVirtual.Float(14, 0, .8f, RigidbodyDrag);

        dashParticle.Play();
        rb.gravityScale = 0;
        GetComponent<BetterJumping>().enabled = false;
        wallJumped = true;
        isDashing = true;

        yield return new WaitForSeconds(.3f);

        dashParticle.Stop();
        rb.gravityScale = 3;
        GetComponent<BetterJumping>().enabled = true;
        wallJumped = false;
        isDashing = false;
    }

    IEnumerator GroundDash()
    {
        yield return new WaitForSeconds(.15f);
        if (coll.onGround)
            hasDashed = false;
    }

    private void WallJump()
    {
        if ((side == 1 && coll.onRightWall) || side == -1 && !coll.onRightWall)
        {
            side *= -1;
            anim.Flip(side);
        }

        StopCoroutine(DisableMovement(0));
        StartCoroutine(DisableMovement(.1f));

        Vector2 wallDir = coll.onRightWall ? Vector2.left : Vector2.right;

        Jump((Vector2.up / 1.5f + wallDir / 1.5f), true);

        wallJumped = true;
    }

    private void WallSlide()
    {
        if(coll.wallSide != side)
         anim.Flip(side * -1);

        if (!canMove)
            return;

        bool pushingWall = false;
        if((rb.velocity.x > 0 && coll.onRightWall) || (rb.velocity.x < 0 && coll.onLeftWall))
        {
            pushingWall = true;
        }
        float push = pushingWall ? 0 : rb.velocity.x;

        rb.velocity = new Vector2(push, -slideSpeed);
    }

    private void Walk(Vector2 dir)
    {
        if (!canMove)
            return;

        if (wallGrab)
            return;

        if (!wallJumped)
        {
            rb.velocity = new Vector2(dir.x * speed, rb.velocity.y);
        }
        else
        {
            rb.velocity = Vector2.Lerp(rb.velocity, (new Vector2(dir.x * speed, rb.velocity.y)), wallJumpLerp * Time.deltaTime);
        }
    }

    private void Jump(Vector2 dir, bool wall)
    {
        slideParticle.transform.parent.localScale = new Vector3(ParticleSide(), 1, 1);
        ParticleSystem particle = wall ? wallJumpParticle : jumpParticle;

        rb.velocity = new Vector2(rb.velocity.x, 0);
        rb.velocity += dir * jumpForce;

        particle.Play();
    }

    IEnumerator DisableMovement(float time)
    {
        canMove = false;
        yield return new WaitForSeconds(time);
        canMove = true;
    }

    void RigidbodyDrag(float x)
    {
        rb.drag = x;
    }

    void WallParticle(float vertical)
    {
        var main = slideParticle.main;

        if (wallSlide || (wallGrab && vertical < 0))
        {
            slideParticle.transform.parent.localScale = new Vector3(ParticleSide(), 1, 1);
            main.startColor = Color.white;
        }
        else
        {
            main.startColor = Color.clear;
        }
    }

    int ParticleSide()
    {
        int particleSide = coll.onRightWall ? 1 : -1;
        return particleSide;
    }
}
