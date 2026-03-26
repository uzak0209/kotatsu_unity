using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Kotatsu.Network;

public class PlayerController : MonoBehaviour, PlayerControls.IPlayerActions
{
    // 操作対象のステート
    public enum ControlState { Gravity, Speed, Friction }
    [SerializeField] private MovementSettings settings;
    [SerializeField] private TextMeshProUGUI statusText; // UI表示用
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private float networkPositionSendInterval = 0.05f;

    private Rigidbody2D rb;
    private PlayerControls controls;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool canMove = false; 
    private float nextNetworkSendTime;
    public void SetMoveAllowance(bool allowance) => canMove = allowance;

    // ステート管理用
    private ControlState currentState = ControlState.Gravity;
    private float cooldownTimer = 10f; // 開始時も10秒クールタイム
    private const float MaxCooldown = 10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this);
        EnsureStatusText();

        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
        }

        if (FindAnyObjectByType<OpponentAvatarSync>() == null)
        {
            var sync = new GameObject("OpponentAvatarSync");
            sync.AddComponent<OpponentAvatarSync>();
        }
    }

    void OnEnable()
    {
        if (controls != null) controls.Enable();
    }

    void OnDisable()
    {
        if (controls != null) controls.Disable();
    }

    void Update()
    {
        // クールタイムのカウントダウン
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer < 0) cooldownTimer = 0;
        }

        HandleDesktopParamShortcuts();
        TrySendPositionUpdate();
        UpdateUI();
    }

    // 入力イベント
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!canMove) return;
        if (context.started && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.jumpForce);
        }
    }

    // ステートを切り替える
    public void OnCycleState(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            // Gravity -> Speed -> Friction -> Gravity... の順で切り替え
            SelectControlState((ControlState)(((int)currentState + 1) % 3));
        }
    }

    // 値を上げる
    public void OnStateUp(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            TryApplyCurrentStateChange(increase: true);
        }
    }

    // 値を下げる
    public void OnStateDown(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            TryApplyCurrentStateChange(increase: false);
        }
    }

    // 実際の値変更ロジック
    private void ApplyChange(float multiplier)
    {
        switch (currentState)
        {
            case ControlState.Gravity:
                settings.gravityScale = Mathf.Clamp(settings.gravityScale * multiplier, 1f, 16f);
                break;
            case ControlState.Speed:
                settings.moveSpeed = Mathf.Clamp(settings.moveSpeed * multiplier, 2f, 32f);
                break;
            case ControlState.Friction:
                settings.friction = Mathf.Clamp(settings.friction * multiplier, 4f, 64f);
                break;
        }
    }

    private void HandleDesktopParamShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // J/K/L で操作対象を選択
        if (keyboard.jKey.wasPressedThisFrame)
            SelectControlState(ControlState.Gravity);
        if (keyboard.kKey.wasPressedThisFrame)
            SelectControlState(ControlState.Friction);
        if (keyboard.lKey.wasPressedThisFrame)
            SelectControlState(ControlState.Speed);

        // N/M で - / + 方向に変更
        if (keyboard.nKey.wasPressedThisFrame)
            TryApplyCurrentStateChange(increase: false);
        if (keyboard.mKey.wasPressedThisFrame)
            TryApplyCurrentStateChange(increase: true);
    }

    private void SelectControlState(ControlState nextState)
    {
        if (currentState == nextState) return;
        currentState = nextState;
    }

    private bool TryApplyCurrentStateChange(bool increase)
    {
        // UDP param_change はキー入力ごとに送る（サーバー側クールタイム判定に委ねる）
        SendParamChangeOverNetwork(increase);

        if (cooldownTimer > 0f) return false;

        ApplyChange(increase ? 2.0f : 0.5f);
        cooldownTimer = MaxCooldown;
        return true;
    }

    private void SendParamChangeOverNetwork(bool increase)
    {
        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager == null) return;
        }

        if (!networkManager.IsConnected) return;

        string param = currentState switch
        {
            ControlState.Gravity => "gravity",
            ControlState.Speed => "speed",
            ControlState.Friction => "friction",
            _ => null
        };
        if (string.IsNullOrEmpty(param)) return;

        string direction = increase ? "increase" : "decrease";
        networkManager.ChangeParameter(param, direction);
    }

    private void TrySendPositionUpdate()
    {
        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager == null) return;
        }

        if (!networkManager.IsConnected) return;
        if (Time.unscaledTime < nextNetworkSendTime) return;

        float interval = Mathf.Max(0.01f, networkPositionSendInterval);
        nextNetworkSendTime = Time.unscaledTime + interval;

        Vector2 pos = transform.position;
        Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;
        networkManager.UpdatePosition(pos.x, pos.y, vel.x, vel.y);
    }

    // 物理挙動

    void FixedUpdate()
    {
        if (!canMove)
        {
            // 動けない時は速度をゼロにする
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

    // UI更新

    private void UpdateUI()
    {
        if (statusText == null) return;

        string stateName = currentState switch
        {
            ControlState.Gravity => "重力 (Gravity)",
            ControlState.Speed => "速度 (Speed)",
            ControlState.Friction => "摩擦 (Friction)",
            _ => ""
        };

        string cdStr = cooldownTimer > 0 ? $"<color=red>{cooldownTimer:F1}s</color>" : "<color=green>READY</color>";
        
        statusText.text = $"MODE: {stateName}\n" +
                          $"COOLDOWN: {cdStr}\n" +
                          $"G:{settings.gravityScale:F1} / S:{settings.moveSpeed:F1} / F:{settings.friction:F1}\n" +
                          $"J/K/L: Select  N:-  M:+";
    }

    private void OnCollisionStay2D(Collision2D collision) => isGrounded = true;
    private void OnCollisionExit2D(Collision2D collision) => isGrounded = false;

    private void EnsureStatusText()
    {
        if (statusText != null)
        {
            ConfigureStatusText(statusText);
            return;
        }

        var skillObject = GameObject.Find("skill");
        if (skillObject != null)
        {
            statusText = skillObject.GetComponent<TextMeshProUGUI>();
        }

        if (statusText == null)
        {
            Canvas canvas = HudCanvasUtility.GetOrCreateHudCanvas();
            var textGo = new GameObject("skill");
            textGo.transform.SetParent(canvas.transform, false);
            statusText = textGo.AddComponent<TextMeshProUGUI>();
        }

        ConfigureStatusText(statusText);
    }

    private static void ConfigureStatusText(TextMeshProUGUI text)
    {
        if (text == null) return;

        text.font = text.font != null ? text.font : TMP_Settings.defaultFontAsset;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30f, -28f);
        rt.sizeDelta = new Vector2(520f, 180f);
    }
}
