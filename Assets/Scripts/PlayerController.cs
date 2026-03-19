using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, PlayerControls.IPlayerActions
{
    [SerializeField] private MovementSettings settings; // ここに作成した設定ファイル
    
    private Rigidbody2D rb;
    private PlayerControls controls;
    private Vector2 moveInput;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this); // 入力イベントをこのスクリプトで受け取る
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    // Input Action からの移動入力
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // Input Action からのジャンプ入力
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.jumpForce);
        }
    }

    public void OnStateUp(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            settings.gravityScale *= 2f;
            if (settings.gravityScale > 4f) settings.gravityScale = 4f;
            Debug.Log($"重力アップ！ 現在の重力: {settings.gravityScale}");
        }
    }

    // 重力を半分にする
    public void OnStateDown(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            settings.gravityScale /= 2f;
            if (settings.gravityScale < 1f) settings.gravityScale = 1f;
            
            Debug.Log($"重力ダウン！ 現在の重力: {settings.gravityScale}");
        }
    }

    void FixedUpdate()
    {
        ApplyMovement();
        ApplyFriction();
        ApplyCustomGravity();
    }

    private void ApplyMovement()
    {
        // 入力方向への加速
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            float targetVelX = moveInput.x * settings.moveSpeed;
            rb.linearVelocity = new Vector2(targetVelX, rb.linearVelocity.y);
        }
    }

    private void ApplyFriction()
    {
        // 地面にいて、入力がない時に摩擦を適用
        if (isGrounded && Mathf.Abs(moveInput.x) < 0.01f)
        {
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, 0, settings.friction * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
    }

    private void ApplyCustomGravity()
    {
        // 独自の重力計算（共有設定から反映）
        rb.gravityScale = settings.gravityScale;
    }

    // 接地判定（簡易版）
    private void OnCollisionStay2D(Collision2D collision) => isGrounded = true;
    private void OnCollisionExit2D(Collision2D collision) => isGrounded = false;
}