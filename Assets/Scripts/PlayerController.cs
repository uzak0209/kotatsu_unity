using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerController : MonoBehaviour, PlayerControls.IPlayerActions
{
    // 操作対象のステート
    public enum ControlState { Gravity, Speed, Friction }
    [SerializeField] private MovementSettings settings;
    [SerializeField] private TextMeshProUGUI statusText; // UI表示用

    private Rigidbody2D rb;
    private PlayerControls controls;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool canMove = false; 
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
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Update()
    {
        // クールタイムのカウントダウン
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer < 0) cooldownTimer = 0;
        }

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
            currentState = (ControlState)(((int)currentState + 1) % 3);
            Debug.Log($"操作対象変更: {currentState}");
        }
    }

    // 値を上げる
    public void OnStateUp(InputAction.CallbackContext context)
    {
        if (context.started && cooldownTimer <= 0)
        {
            ApplyChange(2.0f); // 2倍にする
            cooldownTimer = MaxCooldown;
        }
    }

    // 値を下げる
    public void OnStateDown(InputAction.CallbackContext context)
    {
        if (context.started && cooldownTimer <= 0)
        {
            ApplyChange(0.5f); // 半分にする
            cooldownTimer = MaxCooldown;
        }
    }

    // 実際の値変更ロジック
    private void ApplyChange(float multiplier)
    {
        switch (currentState)
        {
            case ControlState.Gravity:
                settings.gravityScale = Mathf.Clamp(settings.gravityScale * multiplier, 1f, 16f);
                Debug.Log($"重力変更: {settings.gravityScale}");
                break;
            case ControlState.Speed:
                settings.moveSpeed = Mathf.Clamp(settings.moveSpeed * multiplier, 2f, 32f);
                Debug.Log($"速度変更: {settings.moveSpeed}");
                break;
            case ControlState.Friction:
                settings.friction = Mathf.Clamp(settings.friction * multiplier, 4f, 64f);
                Debug.Log($"摩擦変更: {settings.friction}");
                break;
        }
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
                          $"G:{settings.gravityScale:F1} / S:{settings.moveSpeed:F1} / F:{settings.friction:F1}";
    }

    private void OnCollisionStay2D(Collision2D collision) => isGrounded = true;
    private void OnCollisionExit2D(Collision2D collision) => isGrounded = false;
}