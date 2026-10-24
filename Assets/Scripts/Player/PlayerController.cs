using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;

    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9.5f;
    public float crouchSpeed = 3f;
    public float acceleration = 12f;
    public float airControl = 0.35f;
    public float jumpHeight = 1.25f;
    public float gravity = -22f;

    [Header("Crouch")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.1f;
    public float crouchTransitionSpeed = 10f;
    public float eyeOffset = 0.15f;

    [Header("Fixed position (front-facing shooter)")]
    [Tooltip("Where the player stands for the whole level. Its forward direction is the front of the 180-degree view.")]
    [SerializeField] Transform playerStartPoint;
    [Tooltip("How far you can turn left/right from the start point's forward (90 = a 180-degree front view).")]
    [SerializeField] float yawLimit = 90f;
    [SerializeField] float pitchUpLimit = 60f;
    [SerializeField] float pitchDownLimit = 60f;

    [Header("Fixed camera + moving crosshair (light-gun style)")]
    [Tooltip("On: the camera never turns; the gun/mouse moves a crosshair over the screen and shots go through it.")]
    [SerializeField] bool fixedCamera = true;
    [Tooltip("Relative input (mouse / delta gun modes): crosshair speed multiplier. The absolute gun maps 0-100 to the whole screen.")]
    [SerializeField] float crosshairSpeed = 0.5f;

    [Header("Camera Feel")]
    public float stepFrequency = 1.9f;   // steps per second at walk speed
    public float headBobAmount = 0.035f;
    public float strafeTilt = 1.6f;
    public float recoilRecoverySpeed = 9f;
    public float recoilRecoverDelay = 0.12f;

    public bool IsGrounded => controller.isGrounded;
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public Vector3 Velocity => velocity;
    public float HorizontalSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
    /// <summary>Strafe speed in local space (-1..1), used for viewmodel roll.</summary>
    public float LocalStrafe { get; private set; }
    public Vector2 LookDelta { get; private set; }
    /// <summary>Walk cycle phase in radians; one step every PI. Shared with the weapon bob.</summary>
    public float BobPhase { get; private set; }
    /// <summary>Set for one frame when landing: how hard (0..1).</summary>
    public float LandImpact { get; private set; }
    public bool InputEnabled { get; set; } = true;
    public bool HasControl => InputEnabled && Cursor.lockState == CursorLockMode.Locked
                              && Keyboard.current != null && Mouse.current != null;

    /// <summary>Where the front arc is measured from (the start point), and its forward direction.</summary>
    public Vector3 FrontOrigin { get; private set; }
    public float FrontYaw { get; private set; }
    public Vector3 FrontForward => Quaternion.Euler(0f, FrontYaw, 0f) * Vector3.forward;
    public float YawLimit => yawLimit;

    public bool FixedCamera => fixedCamera;
    /// <summary>Crosshair in screen pixels (origin bottom-left). Screen centre when the camera isn't fixed.</summary>
    public Vector2 CrosshairScreen { get; private set; }
    /// <summary>Ray through the crosshair: what the gun shoots along.</summary>
    public Ray AimRay => viewCamera != null
        ? viewCamera.ScreenPointToRay(fixedCamera ? (Vector3)CrosshairScreen : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f))
        : new Ray(cameraPivot.position, cameraPivot.forward);
    /// <summary>
    /// Half of what the player can see sideways from the front direction: the camera's half horizontal FOV
    /// when the camera is fixed, else how far it can turn.
    /// </summary>
    public float ViewHalfAngle => fixedCamera && viewCamera != null
        ? Camera.VerticalToHorizontalFieldOfView(viewCamera.fieldOfView, viewCamera.aspect) * 0.5f
        : yawLimit;
    float PixelsPerDegree => Screen.height / Mathf.Max(1f, viewCamera != null ? viewCamera.fieldOfView : 75f);

    Camera viewCamera;
    CharacterController controller;
    Vector3 velocity;
    float yawOffset, pitch; // yaw is FrontYaw + yawOffset
    float shakeTrauma;
    float recoilPitch, recoilTarget, lastRecoilTime;
    float landOffset, landVel, tilt, bobBlend;
    float airborneMinVy;
    bool wasGrounded = true, deadView;
    int lastStep;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        viewCamera = cameraPivot != null ? cameraPivot.GetComponentInChildren<Camera>() : null;
        CrosshairScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        FrontOrigin = transform.position;
        FrontYaw = transform.eulerAngles.y;
        if (playerStartPoint != null) Teleport(playerStartPoint.position, playerStartPoint.eulerAngles.y);
    }

    /// <summary>Vertical recoil kicks the view and recovers once you stop firing; horizontal is permanent.</summary>
    public void AddRecoil(float up, float side)
    {
        recoilTarget += up;
        if (fixedCamera) CrosshairScreen += new Vector2(side * PixelsPerDegree, 0f);
        else yawOffset += side;
        lastRecoilTime = Time.time;
    }

    public void AddShake(float amount) => shakeTrauma = Mathf.Clamp01(shakeTrauma + amount);

    /// <summary>Knockback (explosions, slams). Horizontal part decays; vertical feeds into the jump arc.</summary>
    public void AddImpulse(Vector3 impulse)
    {
        if (fixedPosition) return;
        externalVelocity += new Vector3(impulse.x, 0f, impulse.z);
        if (impulse.y > 0f) velocity.y = Mathf.Max(velocity.y, 0f) + impulse.y;
    }

    /// <summary>Aim-only drills: look and shoot, but no walking or jumping.</summary>
    public bool MovementLocked { get; set; }

    /// <summary>Turret mode: the player holds one spot and only looks and shoots, within the front arc. Knockback is ignored too.</summary>
    public bool fixedPosition = true;

    /// <summary>Instantly move the player and face a direction. That direction becomes the new front.</summary>
    public void Teleport(Vector3 position, float facingYaw)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facingYaw, 0f));
        controller.enabled = true;
        FrontOrigin = position;
        FrontYaw = facingYaw;
        yawOffset = 0f;
        pitch = 0f;
        recoilTarget = recoilPitch = 0f;
        velocity = externalVelocity = Vector3.zero;
    }

    /// <summary>Multiplier from the weapon (lower while zoomed).</summary>
    public float SensitivityScale { get; set; } = 1f;
    Vector3 externalVelocity;

    public void EnterDeathView()
    {
        deadView = true;
        InputEnabled = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        LandImpact = 0f;

        if (deadView)
        {
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, new Vector3(0f, 0.35f, 0f), dt * 3f);
            cameraPivot.localRotation = Quaternion.Slerp(cameraPivot.localRotation, Quaternion.Euler(-10f, 0f, 35f), dt * 3f);
            return;
        }

        Vector2 move = Vector2.zero;
        LookDelta = Vector2.zero;
        bool jump = false, sprint = false, crouch = false, absoluteAim = false;
        Vector2 aimPoint = Vector2.zero;

        if (HasControl)
        {
            var kb = Keyboard.current;
            if (kb.wKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed) move.x -= 1f;
            move = Vector2.ClampMagnitude(move, 1f);

            // Hardware gun (UDP) when its mode is on, otherwise the mouse. Both yield degrees this frame.
            // Absolute gun (x/y 0-100): the gun's position is the view direction within the yaw/pitch limits.
            var hw = HardwareInput.Instance;
            if (hw != null && hw.HasAbsoluteAim)
            {
                absoluteAim = true;
                aimPoint = hw.AimNormalized;
            }
            else
                LookDelta = hw != null && hw.Active
                    ? hw.ConsumeLookDegrees() * SensitivityScale
                    : Mouse.current.delta.ReadValue() * GameSettings.Sensitivity * SensitivityScale;
            jump = kb.spaceKey.wasPressedThisFrame;
            sprint = kb.leftShiftKey.isPressed && move.y > 0.1f;
            crouch = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            if (MovementLocked || fixedPosition)
            {
                move = Vector2.zero;
                jump = sprint = false;
            }
            if (fixedPosition) crouch = false;
        }

        if (fixedCamera)
        {
            // Camera stays on the start point's forward; the input moves the crosshair over the screen instead.
            Vector2 before = CrosshairScreen;
            if (absoluteAim) // gun 0-100 -> whole screen (50,50 = centre)
                CrosshairScreen = new Vector2((aimPoint.x + 1f) * 0.5f * Screen.width, (aimPoint.y + 1f) * 0.5f * Screen.height);
            else
                CrosshairScreen += LookDelta * PixelsPerDegree * crosshairSpeed;
            CrosshairScreen = new Vector2(Mathf.Clamp(CrosshairScreen.x, 0f, Screen.width), Mathf.Clamp(CrosshairScreen.y, 0f, Screen.height));
            LookDelta = (CrosshairScreen - before) / PixelsPerDegree * 0.5f; // weapon sway follows the crosshair
            yawOffset = 0f;
            pitch = 0f;
        }
        else if (absoluteAim)
        {
            // -1..1 maps onto the limits; 0,0 looks straight along the start point's forward.
            float newYaw = aimPoint.x * yawLimit;
            float newPitch = aimPoint.y >= 0f ? -aimPoint.y * pitchUpLimit : -aimPoint.y * pitchDownLimit;
            LookDelta = new Vector2(newYaw - yawOffset, pitch - newPitch); // drives weapon sway like mouse look
            yawOffset = newYaw;
            pitch = newPitch;
        }
        else
        {
            // Fixed mode: yaw stops at the edges of the front arc (no wrap-around).
            yawOffset += LookDelta.x;
            if (fixedPosition) yawOffset = Mathf.Clamp(yawOffset, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch - LookDelta.y, -pitchUpLimit, pitchDownLimit);
        }
        transform.rotation = Quaternion.Euler(0f, FrontYaw + yawOffset, 0f);

        UpdateCrouch(crouch, dt);

        bool grounded = controller.isGrounded;
        bool sprinting = sprint && !IsCrouching;
        IsSprinting = sprinting && grounded;
        float speed = IsCrouching ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);

        Vector3 wish = (transform.right * move.x + transform.forward * move.y) * speed;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float accel = grounded ? acceleration : acceleration * airControl;
        horizontal = Vector3.Lerp(horizontal, wish, 1f - Mathf.Exp(-accel * dt));

        if (grounded && velocity.y < 0f) velocity.y = -2f;
        if (grounded && jump && !IsCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            ProceduralAudio.Play2D(ProceduralAudio.Jump, 0.5f);
        }
        velocity.y += gravity * dt;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;

        externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, 1f - Mathf.Exp(-(grounded ? 6f : 1.5f) * dt));
        var flags = controller.Move((velocity + externalVelocity) * dt);
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

        UpdateLanding(dt);
        UpdateBobAndSteps(dt);
        UpdateCamera(dt, move.x);
    }

    void UpdateLanding(float dt)
    {
        bool grounded = controller.isGrounded;
        if (!grounded) airborneMinVy = Mathf.Min(airborneMinVy, velocity.y);
        if (grounded && !wasGrounded)
        {
            float impact = Mathf.Clamp01((-airborneMinVy - 3f) / 12f);
            if (impact > 0f)
            {
                LandImpact = impact;
                landVel -= impact * 2.2f;
                ProceduralAudio.Play2D(ProceduralAudio.Land, 0.4f + impact * 0.5f);
                AddShake(impact * 0.25f);
            }
            airborneMinVy = 0f;
        }
        wasGrounded = grounded;

        // Spring the landing dip back to zero.
        landVel += (-landOffset * 120f - landVel * 14f) * dt;
        landOffset += landVel * dt;
    }

    void UpdateBobAndSteps(float dt)
    {
        float speed01 = controller.isGrounded ? Mathf.Clamp01(HorizontalSpeed / walkSpeed) : 0f;
        bobBlend = Mathf.MoveTowards(bobBlend, speed01 > 0.1f ? 1f : 0f, dt * 4f);
        if (speed01 > 0.1f)
        {
            float speedRatio = HorizontalSpeed / walkSpeed;
            BobPhase += dt * stepFrequency * Mathf.PI * Mathf.Lerp(1f, 1.35f, Mathf.Clamp01(speedRatio - 1f)) * Mathf.Max(speedRatio, 0.6f);
        }

        int step = Mathf.FloorToInt(BobPhase / Mathf.PI);
        if (step != lastStep)
        {
            lastStep = step;
            if (controller.isGrounded && speed01 > 0.3f)
                ProceduralAudio.Play2D(ProceduralAudio.Footstep, IsCrouching ? 0.15f : IsSprinting ? 0.45f : 0.3f, 0.1f);
        }
    }

    void UpdateCamera(float dt, float strafeInput)
    {
        // Recoil: approach target quickly, then recover after firing stops.
        recoilPitch = Mathf.Lerp(recoilPitch, recoilTarget, 1f - Mathf.Exp(-30f * dt));
        if (Time.time - lastRecoilTime > recoilRecoverDelay)
            recoilTarget = Mathf.MoveTowards(recoilTarget, 0f, recoilRecoverySpeed * dt);

        LocalStrafe = Mathf.Clamp(Vector3.Dot(velocity, transform.right) / walkSpeed, -1f, 1f);
        tilt = Mathf.Lerp(tilt, -LocalStrafe * strafeTilt, dt * 8f);

        float bobAmp = headBobAmount * bobBlend * (IsSprinting ? 1.5f : IsCrouching ? 0.5f : 1f);
        float bobY = -Mathf.Abs(Mathf.Sin(BobPhase)) * bobAmp;
        float bobX = Mathf.Cos(BobPhase) * bobAmp * 0.5f;
        float bobRoll = Mathf.Cos(BobPhase) * bobAmp * 8f;

        shakeTrauma = Mathf.MoveTowards(shakeTrauma, 0f, dt * 1.8f);
        float s = shakeTrauma * shakeTrauma * 6f;
        float t = Time.time * 25f;
        Vector3 shake = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f,
            Mathf.PerlinNoise(t, t) - 0.5f) * s;

        cameraPivot.localPosition = new Vector3(bobX, controller.height - eyeOffset + bobY + landOffset * 0.25f, 0f);
        cameraPivot.localRotation = Quaternion.Euler(
            Mathf.Clamp(pitch - recoilPitch, -pitchUpLimit, pitchDownLimit) + shake.x - landOffset * 6f,
            shake.y,
            shake.z + tilt + bobRoll);
    }

    void UpdateCrouch(bool wantCrouch, float dt)
    {
        if (!wantCrouch && IsCrouching)
        {
            // Don't stand up into a ceiling.
            int mask = ~(1 << gameObject.layer);
            Vector3 origin = transform.position + Vector3.up * controller.radius;
            if (Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.up, out _,
                    standHeight - controller.radius, mask, QueryTriggerInteraction.Ignore))
                wantCrouch = true;
        }

        IsCrouching = wantCrouch;
        float target = IsCrouching ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, target, crouchTransitionSpeed * dt);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
    }
}
