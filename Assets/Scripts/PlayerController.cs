using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Kotatsu.Network;
using System;
using UnityEngine.UI;
using Unity.VisualScripting;

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
    // private float[] levelValues = { 2f, 8f, 32f };
    private float[] speedLevelValues = { 4f, 8f, 16f };
    private float[] frictionLevelValues = { 4f, 8f, 16f };
    private float[] gravityLevelValues = { 2f, 4f, 8f };
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
    private float nextNetworkStageSendTime;
    private bool isFacingRight = true;
    private float networkPositionSendInterval = 0.05f;
    private float networkStageSendInterval = 0.2f;
    private int lastSentStageIndex = -1;

    public void SetMoveAllowance(bool allowance) => canMove = allowance;

    private ControlState currentState = ControlState.Gravity;
    private float cooldownTimer = 11f; 

    public const float MaxCooldown = 8f;
    private float hangTimeVelocityThreshold = 2.0f; // 頂点と判定するY速度の閾値
    private float hangTimeGravityMultiplier = 0.5f; // 頂点付近での重力倍率（1より小さくするとふわっとする）
    private bool jumpRequest;
    private bool isJumpKeyHeld;
    [SerializeField] 
    private PlayerRespawn playerRespawn;
    private bool isRespawning = false;
    private float respawnDownKeyTime = 1f; // リスポーンのためにFキーを押し続ける必要がある時間
    [SerializeField]
    GaugeController gaugeController;
    [SerializeField]
    private GameObject[] statusSelectors; // 0: Gravity, 1: Speed, 2: Friction
    [SerializeField]
    private TextMeshProUGUI[] statusValues; // 0: Gravity, 1: Speed, 2: Friction;
    [SerializeField]
    private Image characterIconImage; // キャラアイコン表示用のSpriteRenderer
    [SerializeField]
    private Sprite[] characterIconList; // キャラアイコン用のスプライトセット
    // [SerializeField]
    // private Image statusIconImage; // ステータスアイコン表示用のImage
    // [SerializeField]
    // private Sprite[] statusIconList; // ステータスアイコン用のスプライトセット
    // [SerializeField]
    // private TextMeshProUGUI statusLevelText; // キャラ名表示用のTextMeshProUGUI

    private bool appliedAssignedCharacter;
    private bool subscribedToNetworkManager;
    [SerializeField]
    private LogManager logManager;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this);
        jumpRequest = false;
        if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();

        gaugeController.StartGauge(cooldownTimer);

        TryBindNetworkManager();


        // 初期値の適用
        SyncSettingsWithLevels();
    }

    void Start()
    {
        ApplyLatestNetworkParamsIfAvailable();
        ApplyAssignedCharacterIfAvailable();
    }

    void OnEnable()
    {
        controls?.Enable();
        TryBindNetworkManager();
        ApplyLatestNetworkParamsIfAvailable();
    }

    void OnDisable()
    {
        controls?.Disable();
        UnsubscribeFromNetworkManager();
    }

    void Update()
    {
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer < 0) cooldownTimer = 0;
        }
        if (isRespawning)
        {
            respawnDownKeyTime -= Time.deltaTime;
            if (respawnDownKeyTime <= 0) 
            {
                playerRespawn.Respawn();
                respawnDownKeyTime = 1f; // タイマーをリセット
                isRespawning = false;
            }
        }
        // HandleDesktopParamShortcuts();　多分もう使わない。


        ApplyAssignedCharacterIfAvailable();

        TrySendPositionUpdate();
        TrySendStageProgressUpdate();
        UpdateUI();
        UpdateSprite();
    }

    // --- レベルから実際の値を設定に反映 ---
    private void SyncSettingsWithLevels()
    {
        settings.gravityScale = gravityLevelValues[Mathf.Clamp(gravityLevel, 0, 2)];
        settings.lowJumpMultiplier = gravityLevelValues[Mathf.Clamp(gravityLevel, 0, 2)]; // ジャンプ中の重力倍率も重力レベルに応じて変化させる
        settings.moveSpeed = speedLevelValues[Mathf.Clamp(speedLevel, 0, 2)];
        settings.friction = frictionLevelValues[Mathf.Clamp(frictionLevel, 0, 2)];
        settings.jumpForce = FixedJumpForce; // ジャンプ力は常に固定
    }

    // --- 入力イベント ---
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!canMove) return;
        if (context.started && isGrounded)
        {
            jumpRequest = true;
            isJumpKeyHeld = true;
        } 
        else if (context.canceled) isJumpKeyHeld = false;
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
    public void OnRespawn(InputAction.CallbackContext context)
    {
        if (context.started){
            isRespawning = true;
        }
        if (context.canceled){
            respawnDownKeyTime = 1f; // タイマーをリセット
            isRespawning = false;
        }
    }

    private bool TryApplyCurrentStateChange(bool increase)
    {
        int maxLevel = GetMaxLevelForState(currentState);

        // 現在のレベルが限界値なら何もしない
        int currentLevel = GetCurrentLevel(currentState);

        if (increase && currentLevel >= maxLevel) return false;
        if (!increase && currentLevel <= 0) return false;

        bool useAuthoritativeNetwork = networkManager != null && networkManager.IsConnected;
        if (useAuthoritativeNetwork)
        {
            SendParamChangeOverNetwork(increase);
            return true;
        }

        if (cooldownTimer > 0f) return false;

        // ローカルのレベルを更新
        int diff = increase ? 1 : -1;
        switch (currentState)
        {
            case ControlState.Gravity: gravityLevel += diff; break;
            case ControlState.Speed: speedLevel += diff; break;
            case ControlState.Friction: frictionLevel += diff; break;
        }
        gaugeController.StartGauge(MaxCooldown);
        SyncSettingsWithLevels();
        
        // switch (currentState)
        // {
        //     case ControlState.Gravity: statusIconImage.sprite = statusIconList[0];
        //     statusLevelText.text = gravityLevel switch { 0 => "低", 1 => "中", 2 => "高", _ => "" }; break;
        //     case ControlState.Speed: statusIconImage.sprite = statusIconList[1];
        //     statusLevelText.text = speedLevel switch { 0 => "低", 1 => "中", 2 => "高", _ => "" }; break;
        //     case ControlState.Friction: statusIconImage.sprite = statusIconList[2];
        //     statusLevelText.text = frictionLevel switch { 0 => "低", 1 => "中", 2 => "高", _ => "" }; break;
        // }
        if (logManager != null)        {
            int beforeIndex = (int)currentState;
            int afterIndex = currentState switch
            {
                ControlState.Gravity => gravityLevel,
                ControlState.Speed => speedLevel,
                ControlState.Friction => frictionLevel,
                _ => 1
            };
            logManager.ShowLog(beforeIndex, afterIndex);
        }
        // characterIconImage.sprite = characterIconList[selectedCharacterIndex]; //本来はselectedCharacterIndexではなく変えた人。
        // string statName = currentState.ToString();
        // float newVal = currentState switch {
        //     ControlState.Gravity => settings.gravityScale,
        //     ControlState.Speed => settings.moveSpeed,
        //     _ => settings.friction
        // };

        // if (logText != null) logText.text = $"あなたが{statName}変更:{newVal:F1}";
        

        // ApplyLocalStateDelta(increase ? 1 : -1);

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
        ApplyJump();
        rb.gravityScale = settings.gravityScale;
    }
    private void ApplyJump()
    {
        if (jumpRequest)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, settings.jumpForce);
            jumpRequest = false;
        }
        if (rb.linearVelocity.y < 0)
        {
            // 落下中
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (rb.gravityScale - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !isJumpKeyHeld)
        {
            // Debug.Log($"Low jump applied: gravityScale={settings.gravityScale}, lowJumpMultiplier={settings.lowJumpMultiplier}");
            // 上昇中 ＋ ボタンを離した
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (settings.lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (Mathf.Abs(rb.linearVelocity.y) < hangTimeVelocityThreshold)
        {
            // 頂点付近（ふわっとさせる）
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (hangTimeGravityMultiplier - 1) * Time.fixedDeltaTime;
        }
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

    private void TrySendStageProgressUpdate()
    {
        if (networkManager == null || !networkManager.IsConnected) return;

        StageManager stageManager = StageManager.Instance != null ? StageManager.Instance : FindFirstObjectByType<StageManager>();
        if (stageManager == null) return;

        int currentStageIndex = stageManager.GetCurrentStageIndexForPosition(transform.position.x);
        bool stageChanged = currentStageIndex != lastSentStageIndex;
        if (!stageChanged && Time.unscaledTime < nextNetworkStageSendTime) return;

        lastSentStageIndex = currentStageIndex;
        nextNetworkStageSendTime = Time.unscaledTime + networkStageSendInterval;
        networkManager.UpdateStageProgress(currentStageIndex);
    }

    private void TryBindNetworkManager()
    {
        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
        }

        if (networkManager == null || subscribedToNetworkManager)
        {
            return;
        }

        networkManager.OnPlayerParamsChanged += HandlePlayerParamsChanged;
        subscribedToNetworkManager = true;
    }

    private void UnsubscribeFromNetworkManager()
    {
        if (networkManager == null || !subscribedToNetworkManager)
        {
            return;
        }

        networkManager.OnPlayerParamsChanged -= HandlePlayerParamsChanged;
        subscribedToNetworkManager = false;
    }

    private void ApplyLatestNetworkParamsIfAvailable()
    {
        if (networkManager == null)
        {
            return;
        }

        if (networkManager.TryGetLatestParams(out int gravity, out int friction, out int speed))
        {
            ApplyAuthoritativeParams(gravity, friction, speed, null);
        }
    }

    private void HandlePlayerParamsChanged(string playerId, int gravity, int friction, int speed)
    {
        ApplyAuthoritativeParams(gravity, friction, speed, playerId);
    }

    private void ApplyAuthoritativeParams(int gravity, int friction, int speed, string sourcePlayerId)
    {
        int previousGravity = gravityLevel;
        int previousFriction = frictionLevel;
        int previousSpeed = speedLevel;

        gravityLevel = ConvertServerLevelToLocalIndex(gravity, 2);
        frictionLevel = ConvertServerLevelToLocalIndex(friction, 1);
        speedLevel = ConvertServerLevelToLocalIndex(speed, 2);
        SyncSettingsWithLevels();

        bool changed = previousGravity != gravityLevel || previousFriction != frictionLevel || previousSpeed != speedLevel;
        if (!changed || logText == null || string.IsNullOrWhiteSpace(sourcePlayerId))
        {
            return;
        }

        bool changedBySelf = networkManager != null &&
            !string.IsNullOrWhiteSpace(networkManager.CurrentPlayerId) &&
            string.Equals(networkManager.CurrentPlayerId, sourcePlayerId, System.StringComparison.Ordinal);
        // switch (currentState)
        // {
        //     case ControlState.Gravity: statusIconImage.sprite = statusIconList[0];
        //     statusLevelText.text = gravityLevel switch { 0 => "低", 1 => "中", 2 => "高", _ => "" }; break;
        //     case ControlState.Speed: statusIconImage.sprite = statusIconList[1];
        //     statusLevelText.text = speedLevel switch { 0 => "低", 1 => "中", 2 => "高", _ => "" }; break;
        //     case ControlState.Friction: statusIconImage.sprite = statusIconList[2];
        //     statusLevelText.text = frictionLevel switch { 0 => "低", 1 => "中", 2 => "高", _ => "" }; break;
        // }
        // characterIconImage.sprite = characterIconList[sourcePlayerId]; 
        // string actor = changedBySelf ? "あなた" : sourcePlayerId;
        // logText.text = $"{actor}の変更を反映 G:{settings.gravityScale:F1} / S:{settings.moveSpeed:F1} / F:{settings.friction:F1}";


        if (logManager != null)        {
            int beforeIndex = (int)currentState;
            int afterIndex = currentState switch
            {
                ControlState.Gravity => gravityLevel,
                ControlState.Speed => speedLevel,
                ControlState.Friction => frictionLevel,
                _ => 1
            };
            logManager.ShowLog(beforeIndex, afterIndex);
        }
    }

    private void ApplyLocalStateDelta(int diff)
    {
        switch (currentState)
        {
            case ControlState.Gravity:
                gravityLevel += diff;
                break;
            case ControlState.Speed:
                speedLevel += diff;
                break;
            case ControlState.Friction:
                frictionLevel += diff;
                break;
        }

        SyncSettingsWithLevels();

        string statName = currentState.ToString();
        float newVal = currentState switch
        {
            ControlState.Gravity => settings.gravityScale,
            ControlState.Speed => settings.moveSpeed,
            _ => settings.friction
        };

        // if (logText != null)
        // {
        //     logText.text = $"あなたが{statName}変更:{newVal:F1}";
        // }

        // if (logManager != null)        {
        //     int beforeIndex = (int)currentState;
        //     int afterIndex = currentState switch
        //     {
        //         ControlState.Gravity => gravityLevel,
        //         ControlState.Speed => speedLevel,
        //         ControlState.Friction => frictionLevel,
        //         _ => 1
        //     };
        //     logManager.ShowLog(beforeIndex, afterIndex);
        // }
    }

    private int GetCurrentLevel(ControlState state)
    {
        return state switch
        {
            ControlState.Gravity => gravityLevel,
            ControlState.Speed => speedLevel,
            ControlState.Friction => frictionLevel,
            _ => 1
        };
    }

    private static int GetMaxLevelForState(ControlState state)
    {
        return state == ControlState.Friction ? 1 : 2;
    }

    private static int ConvertServerLevelToLocalIndex(int serverLevel, int maxLocalIndex)
    {
        return Mathf.Clamp(serverLevel - 1, 0, maxLocalIndex);
    }

    // --- UI/Visual ---
    private void UpdateUI()
    {
        // if (statusText == null) return;
        GameObject selectedSelector = currentState switch {
            ControlState.Gravity => statusSelectors[0],
            ControlState.Speed => statusSelectors[1],
            ControlState.Friction => statusSelectors[2],
            _ => null
        };
        foreach (var selector in statusSelectors) selector.SetActive(selector == selectedSelector);
        // string cdStr = cooldownTimer > 0 ? $"<color=red>{cooldownTimer:F1}s</color>" : "<color=green>READY</color>";
        
        // statusText.text = $"MODE: {stateName}\n" +
        // //                   $"COOLDOWN: {cdStr}\n" +
        //                   $"G:{settings.gravityScale:F1} / S:{settings.moveSpeed:F1} / F:{settings.friction:F1}";
        Debug.Log($"Gravity Level: {gravityLevel}, Speed Level: {speedLevel}, Friction Level: {frictionLevel}");
        switch (gravityLevel)
        {
            case 0: statusValues[0].text = "低"; break;
            case 1: statusValues[0].text = "中"; break;
            case 2: statusValues[0].text = "高"; break;
        }       switch (speedLevel)
        {
            case 0: statusValues[1].text = "低"; break;
            case 1: statusValues[1].text = "中"; break;
            case 2: statusValues[1].text = "高"; break;
        }        switch (frictionLevel)
        {
            case 0: statusValues[2].text = "低"; break;
            case 1: statusValues[2].text = "中"; break;
            case 2: statusValues[2].text = "高"; break;
        }
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

    private void HandleDesktopParamShortcuts() //多分もう使わない。
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

    private void ApplyAssignedCharacterIfAvailable()
    {
        if (appliedAssignedCharacter || networkManager == null)
        {
            return;
        }

        if (!networkManager.TryGetPlayerMatchState(networkManager.CurrentPlayerId, out MatchPlayerState playerState))
        {
            return;
        }

        selectedCharacterIndex = Mathf.Clamp(playerState.color_index, 0, Mathf.Max(0, characterList.Length - 1));
        appliedAssignedCharacter = true;
    }

    private void OnCollisionStay2D(Collision2D collision) => isGrounded = true;
    private void OnCollisionExit2D(Collision2D collision) => isGrounded = false;
}
