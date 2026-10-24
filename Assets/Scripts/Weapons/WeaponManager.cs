using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Reads weapon input, handles owned weapons, switching, FOV, sway, bob and inertia.</summary>
public class WeaponManager : MonoBehaviour
{
    public PlayerController player;
    public Camera cam;
    public Transform holder;
    public Gun[] guns;
    [Tooltip("Which guns the player starts with (same order as guns).")]
    public bool[] startOwned;

    [Header("Feel")]
    public float swayAmount = 0.0025f;
    public float swayMax = 0.06f;
    public float swaySmooth = 8f;
    public float bobAmount = 0.022f;
    public float switchTime = 0.4f;

    public int CurrentIndex { get; private set; }
    public Gun Current => guns[CurrentIndex];
    public bool IsOwned(int i) => owned != null && i >= 0 && i < owned.Length && owned[i];
    float BaseFov => GameSettings.Fov;

    bool[] owned;
    Vector3 holderBasePos, sway, bobOffset, aimShiftFollow;
    Vector2 aimFollow;
    float sprintBlend, switchTimer, roll, landBounce, landBounceVel, crouchBlend;
    int pendingIndex = -1, lastIndex = -1;

    static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

    void Awake()
    {
        owned = new bool[guns.Length];
        for (int i = 0; i < guns.Length; i++) owned[i] = startOwned != null && i < startOwned.Length && startOwned[i];
        if (!System.Array.Exists(owned, o => o)) owned[0] = true;
    }

    void Start()
    {
        holderBasePos = holder.localPosition;
        // Start on the best owned weapon (last in the list).
        CurrentIndex = System.Array.LastIndexOf(owned, true);
        for (int i = 0; i < guns.Length; i++)
        {
            guns[i].Init(this);
            guns[i].gameObject.SetActive(i == CurrentIndex);
        }
        cam.fieldOfView = BaseFov;
    }

    /// <summary>Gives the player a weapon and switches to it.</summary>
    public void Unlock(int index)
    {
        if (index < 0 || index >= guns.Length || owned[index]) return;
        owned[index] = true;
        switchTimer = 0f;
        RequestSwitch(index);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.CurrentState == GameManager.State.GameOver)
        {
            if (holder.gameObject.activeSelf) holder.gameObject.SetActive(false);
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        bool control = player.HasControl && (gm == null || gm.IsPlaying);
        bool fireHeld = false, firePressed = false, reload = false, aim = false;

        if (control)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            // Hardware gun trigger (P1 press / R1 release) when its mode is on, otherwise the left mouse button.
            var hw = HardwareInput.Instance;
            bool hardware = hw != null && hw.Active;
            fireHeld = hardware ? hw.FireHeld : mouse.leftButton.isPressed;
            firePressed = hardware ? hw.FirePressedThisFrame : mouse.leftButton.wasPressedThisFrame;
            // Gun buttons: B1 reloads, S1 switches to the next owned weapon. Keyboard still works too.
            reload = kb.rKey.wasPressedThisFrame || (hardware && hw.ReloadPressedThisFrame);
            if (hardware && hw.SwitchWeaponPressedThisFrame) RequestSwitch(NextOwned(1));
            // No aim-down-sights with a fixed camera: the zoom/scope is screen-centred, the crosshair isn't.
            aim = mouse.rightButton.isPressed && !player.FixedCamera;

            for (int i = 0; i < guns.Length && i < SlotKeys.Length; i++)
                if (kb[SlotKeys[i]].wasPressedThisFrame) RequestSwitch(i);
            if (kb.qKey.wasPressedThisFrame) RequestSwitch(lastIndex);
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.1f) RequestSwitch(NextOwned(-1));
            else if (scroll < -0.1f) RequestSwitch(NextOwned(1));
        }

        // Weapon switch: lower, swap at the midpoint, raise.
        float lower = 0f;
        if (switchTimer > 0f)
        {
            switchTimer -= dt;
            float half = switchTime * 0.5f;
            if (pendingIndex >= 0 && switchTimer <= half)
            {
                guns[CurrentIndex].gameObject.SetActive(false);
                CurrentIndex = pendingIndex;
                pendingIndex = -1;
                guns[CurrentIndex].gameObject.SetActive(true);
                ProceduralAudio.Play2D(ProceduralAudio.WeaponSwitch, 0.5f);
            }
            lower = switchTimer > 0f ? Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(switchTimer - half) / half) : 0f;
        }

        sprintBlend = Mathf.MoveTowards(sprintBlend, player.IsSprinting && !fireHeld && !Current.IsReloading ? 1f : 0f, dt * 6f);
        crouchBlend = Mathf.MoveTowards(crouchBlend, player.IsCrouching ? 1f : 0f, dt * 5f);
        bool canFire = control && switchTimer <= 0f && sprintBlend < 0.3f;

        Current.Tick(fireHeld, firePressed, reload, aim, canFire);
        float aimT = Current.AimAmount;
        float hipT = 1f - aimT * 0.85f;

        float targetFov = Mathf.Lerp(BaseFov, BaseFov * Current.adsFovMultiplier, aimT) + sprintBlend * 6f;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, dt * 14f);
        // Slow the mouse down while zoomed so aiming feels consistent.
        player.SensitivityScale = Mathf.Lerp(1f, Current.adsFovMultiplier, aimT * 0.85f);

        // Sway lags behind mouse movement.
        Vector2 look = player.LookDelta;
        Vector3 swayTarget = Vector3.ClampMagnitude(new Vector3(-look.x, -look.y, 0f) * swayAmount * (1f - aimT * 0.8f), swayMax);
        sway = Vector3.Lerp(sway, swayTarget, dt * swaySmooth);

        // Walk bob, synced to the player's footsteps (figure-eight).
        float phase = player.BobPhase;
        float speed01 = player.IsGrounded ? Mathf.Clamp01(player.HorizontalSpeed / player.walkSpeed) : 0f;
        float bobScale = bobAmount * speed01 * hipT * (1f + sprintBlend * 0.8f);
        Vector3 bobTarget = new Vector3(Mathf.Cos(phase) * bobScale, -Mathf.Abs(Mathf.Sin(phase)) * bobScale, 0f);
        bobOffset = Vector3.Lerp(bobOffset, bobTarget, dt * 12f);

        float breathe = Mathf.Sin(Time.time * 1.6f) * 0.004f * (1f - speed01) * hipT;

        roll = Mathf.Lerp(roll, -player.LocalStrafe * 4f * hipT, dt * 8f);
        landBounceVel -= player.LandImpact * 1.5f;
        landBounceVel += (-landBounce * 150f - landBounceVel * 12f) * dt;
        landBounce += landBounceVel * dt;

        float air = player.IsGrounded ? 0f : Mathf.Clamp(-player.Velocity.y * 0.003f, -0.02f, 0.03f);

        // Fixed camera: the gun turns toward the on-screen crosshair and leans a little in its direction.
        Vector2 aimAngles = Vector2.zero;
        Vector3 aimShift = Vector3.zero;
        if (player.FixedCamera)
        {
            Vector3 d = cam.transform.InverseTransformDirection(player.AimRay.direction);
            aimAngles = new Vector2(Mathf.Atan2(d.x, d.z), -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude)) * Mathf.Rad2Deg;
            Vector2 n = new Vector2(player.CrosshairScreen.x / Screen.width * 2f - 1f, player.CrosshairScreen.y / Screen.height * 2f - 1f);
            aimShift = new Vector3(n.x * 0.07f, n.y * 0.05f, 0f);
        }
        aimFollow = Vector2.Lerp(aimFollow, aimAngles, 1f - Mathf.Exp(-18f * dt));
        aimShiftFollow = Vector3.Lerp(aimShiftFollow, aimShift, 1f - Mathf.Exp(-18f * dt));

        holder.localPosition = holderBasePos + sway + bobOffset + aimShiftFollow
                               + new Vector3(0f, breathe + landBounce * 0.08f * hipT + air * hipT, 0f)
                               + Vector3.down * 0.3f * lower
                               + new Vector3(-0.04f, -0.06f, -0.02f) * sprintBlend
                               + new Vector3(-0.015f, 0.01f, 0f) * crouchBlend * hipT;
        holder.localRotation = Quaternion.Euler(aimFollow.y, aimFollow.x, 0f) * Quaternion.Euler(
            -sway.y * 150f + 40f * lower + 14f * sprintBlend - landBounce * 8f * hipT,
            sway.x * 150f - 35f * sprintBlend,
            sway.x * 100f + 12f * sprintBlend + roll + Mathf.Cos(phase) * bobScale * 60f + 6f * crouchBlend * hipT);
    }

    int NextOwned(int direction)
    {
        for (int step = 1; step <= guns.Length; step++)
        {
            int i = ((CurrentIndex + direction * step) % guns.Length + guns.Length) % guns.Length;
            if (owned[i]) return i;
        }
        return CurrentIndex;
    }

    void RequestSwitch(int index)
    {
        if (index == CurrentIndex || !IsOwned(index) || switchTimer > 0f) return;
        lastIndex = CurrentIndex;
        pendingIndex = index;
        switchTimer = switchTime;
    }

    public bool AddAmmoToAll()
    {
        bool any = false;
        for (int i = 0; i < guns.Length; i++)
            if (owned[i]) any |= guns[i].AddAmmo(guns[i].ammoPerPickup);
        return any;
    }
}
