using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Kotatsu.Network;

public class PlayerController : MonoBehaviour, PlayerControls.IPlayerActions
{
    [System.Serializable]
    public struct CharacterSprites
    {
        public string characterName;
        public Sprite groundRight;
        public Sprite groundLeft;
        public Sprite airRight;
        public Sprite airLeft;
    }

    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private CharacterSprites[] characterList;
    public int selectedCharacterIndex = 0;

    public enum ControlState { Gravity, Speed, Friction }

    [Header("State Levels (0, 1, 2)")]
    public int gravityLevel = 1; // 初期は真ん中の 8f
    public int speedLevel = 1;
    public int frictionLevel = 1;

    // 内部計算用の値
    private float[] levelValues = { 2f, 8f, 32f };
    private const float FixedJumpForce = 16f;

    [Header("References")]
    [SerializeField] private MovementSettings settings;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI statusText2;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private NetworkManager networkManager;

    private Rigidbody2D rb;
    private PlayerControls controls;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool canMove = false; 
    private float nextNetworkSendTime;
    private bool isFacingRight = true;
    private float networkPositionSendInterval = 0.05f;

    public void SetMoveAllowance(bool allowance) => canMove = allowance;

    private ControlState currentState = ControlState.Gravity;
    private float cooldownTimer = 11f; 
    private const float MaxCooldown = 8f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this);
        
        if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();

        // 初期値の適用
        SyncSettingsWithLevels();
    }

    void OnEnable() => controls?.Enable();
    void OnDisable() => controls?.Disable();

    void Update()
    {
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer < 0) cooldownTimer = 0;
        }

        HandleDesktopParamShortcuts();
        TrySendPositionUpdate();
        UpdateUI();
        UpdateSprite();
    }

    // --- レベルから実際の値を設定に反映 ---
    private void SyncSettingsWithLevels()
    {
        settings.gravityScale = levelValues[Mathf.Clamp(gravityLevel, 0, 2)];
        settings.moveSpeed = levelValues[Mathf.Clamp(speedLevel, 0, 2)];
        settings.friction = levelValues[Mathf.Clamp(frictionLevel, 0, 2)];
        settings.jumpForce = FixedJumpForce; // ジャンプ力は常に固定
    }

    // --- 入力イベント ---
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!canMove || !isGrounded) return;
        if (context.started)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.jumpForce);
        }
    }

    public void OnCycleState(InputAction.CallbackContext context)
    {
        if (context.started) SelectControlState((ControlState)(((int)currentState + 1) % 3));
    }

    public void OnStateUp(InputAction.CallbackContext context)
    {
        if (context.started) TryApplyCurrentStateChange(increase: true);
    }

    public void OnStateDown(InputAction.CallbackContext context)
    {
        if (context.started) TryApplyCurrentStateChange(increase: false);
    }

    private bool TryApplyCurrentStateChange(bool increase)
    {
        // 現在のレベルが限界値なら何もしない
        int currentLevel = currentState switch
        {
            ControlState.Gravity => gravityLevel,
            ControlState.Speed => speedLevel,
            ControlState.Friction => frictionLevel,
            _ => 1
        };

        if (increase && currentLevel >= 2) return false;
        if (!increase && currentLevel <= 0) return false;

        // 通信：レベル変更を試みる意思を送信
        SendParamChangeOverNetwork(increase);

        if (cooldownTimer > 0f) return false;

        // ローカルのレベルを更新
        int diff = increase ? 1 : -1;
        switch (currentState)
        {
            case ControlState.Gravity: gravityLevel += diff; break;
            case ControlState.Speed: speedLevel += diff; break;
            case ControlState.Friction: frictionLevel += diff; break;
        }

        SyncSettingsWithLevels();
        
        string statName = currentState.ToString();
        float newVal = currentState switch {
            ControlState.Gravity => settings.gravityScale,
            ControlState.Speed => settings.moveSpeed,
            _ => settings.friction
        };

        if (logText != null) logText.text = $"あなたが{statName}変更:{newVal:F1}";
        
        cooldownTimer = MaxCooldown;
        return true;
    }

    // --- 物理挙動 ---
    void FixedUpdate()
    {
        if (!canMove)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        ApplyMovement();
        ApplyFriction();
        rb.gravityScale = settings.gravityScale;
    }

    private void ApplyMovement()
    {
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            float targetVelX = moveInput.x * settings.moveSpeed;
            rb.linearVelocity = new Vector2(targetVelX, rb.linearVelocity.y);
        }
    }

    private void ApplyFriction()
    {
        if (isGrounded && Mathf.Abs(moveInput.x) < 0.01f)
        {
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, 0, settings.friction * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
    }

    // --- 通信関連 ---
    private void SendParamChangeOverNetwork(bool increase)
    {
        if (networkManager == null || !networkManager.IsConnected) return;

        string param = currentState switch {
            ControlState.Gravity => "gravity",
            ControlState.Speed => "speed",
            ControlState.Friction => "friction",
            _ => null
        };
        if (string.IsNullOrEmpty(param)) return;

        networkManager.ChangeParameter(param, increase ? "increase" : "decrease");
    }

    private void TrySendPositionUpdate()
    {
        if (networkManager == null || !networkManager.IsConnected) return;
        if (Time.unscaledTime < nextNetworkSendTime) return;

        nextNetworkSendTime = Time.unscaledTime + networkPositionSendInterval;
        Vector2 pos = transform.position;
        Vector2 vel = rb.linearVelocity;
        networkManager.UpdatePosition(pos.x, pos.y, vel.x, vel.y);
    }

    // --- UI/Visual ---
    private void UpdateUI()
    {
        if (statusText == null) return;
        string stateName = currentState switch {
            ControlState.Gravity => "重力",
            ControlState.Speed => "速度",
            ControlState.Friction => "摩擦",
            _ => ""
        };
        string cdStr = cooldownTimer > 0 ? $"<color=red>{cooldownTimer:F1}s</color>" : "<color=green>READY</color>";
        
        statusText.text = $"MODE: {stateName}\n" +
                          $"COOLDOWN: {cdStr}\n" +
                          $"G:{settings.gravityScale:F1} / S:{settings.moveSpeed:F1} / F:{settings.friction:F1}";
    }

    private void UpdateSprite()
    {
        if (spriteRenderer == null || characterList == null || characterList.Length <= selectedCharacterIndex) return;

        CharacterSprites currentSet = characterList[selectedCharacterIndex];
        if (moveInput.x > 0.1f) isFacingRight = true;
        else if (moveInput.x < -0.1f) isFacingRight = false;

        if (isGrounded) spriteRenderer.sprite = isFacingRight ? currentSet.groundRight : currentSet.groundLeft;
        else spriteRenderer.sprite = isFacingRight ? currentSet.airRight : currentSet.airLeft;
    }

    private void HandleDesktopParamShortcuts()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;
        if (k.jKey.wasPressedThisFrame) SelectControlState(ControlState.Gravity);
        if (k.kKey.wasPressedThisFrame) SelectControlState(ControlState.Friction);
        if (k.lKey.wasPressedThisFrame) SelectControlState(ControlState.Speed);
        if (k.nKey.wasPressedThisFrame) TryApplyCurrentStateChange(false);
        if (k.mKey.wasPressedThisFrame) TryApplyCurrentStateChange(true);
    }

    private void SelectControlState(ControlState nextState) => currentState = nextState;

    private void OnCollisionStay2D(Collision2D collision) => isGrounded = true;
    private void OnCollisionExit2D(Collision2D collision) => isGrounded = false;
}