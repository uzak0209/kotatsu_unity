using UnityEngine;
using UnityEngine.UI;

public class GaugeController : MonoBehaviour
{
    [Header("ゲージの設定")]
    public Image gaugeImage;      
    public float maxValue = 100f;   // ゲージの最大値

    [Header("色の設定")]
    public Gradient gaugeColor;   

    private float currentValue;     // 現在の値
    private RectTransform gaugeRectTransform; 
    private float maxWidth;         // ゲージが100の時の横幅
    private bool isMoving = false;  // ゲージが動いているかどうかのスイッチ
    private float maxCooldown;        // ゲージが満タンになるまでの時間（秒）

    void Start()
    {
        gaugeRectTransform = gaugeImage.GetComponent<RectTransform>();
        
        // エディタで設定されている現在の横幅を「最大幅（100の時の幅）」として記憶
        maxWidth = gaugeRectTransform.sizeDelta.x;

        // 初期状態は0にセットし、見た目も空にする
        currentValue = 0f;
        UpdateGaugeDisplay();
    }

    void Update()
    {
        // isMoving（スイッチ）が true の時だけ処理を行う
        if (isMoving)
        {
            // 時間経過に合わせて値を増やす (1秒あたり maxValue / chargeTime ずつ増加)
            currentValue += maxValue / maxCooldown * Time.deltaTime;

            // 100に達したら止める
            if (currentValue >= maxValue)
            {
                currentValue = maxValue;
                isMoving = false; // スイッチをオフにする
                Debug.Log("ゲージが100になりました！");
            }

            // 見た目を更新
            UpdateGaugeDisplay();
        }
    }

    // ==========================================
    // 外部から指示を出すための窓口（メソッド）
    // ==========================================
    public void StartGauge(float cooldown)
    {
        maxCooldown = cooldown;
        currentValue = 0f; // 0からスタート
        isMoving = true;   // スイッチをオンにする！
    }

    // ゲージの見た目（幅と色）を更新する処理
    private void UpdateGaugeDisplay()
    {
        // 割合（0.0 ～ 1.0）を計算
        float fillRatio = currentValue / maxValue;

        // 横幅を計算して適用
        float newWidth = maxWidth * fillRatio;
        gaugeRectTransform.sizeDelta = new Vector2(newWidth, gaugeRectTransform.sizeDelta.y);

        // 色を適用
        gaugeImage.color = gaugeColor.Evaluate(fillRatio);
    }
}