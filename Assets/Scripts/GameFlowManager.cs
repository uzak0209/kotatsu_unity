using UnityEngine;
using System.Collections;
using TMPro;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private PlayerController player; // インスペクターでプレイヤーをアサイン
    [SerializeField] private TextMeshProUGUI countdownText; // カウントダウン用UI

    [SerializeField] private int countdownSeconds = 3;

    void Start()
    {
        EnsureReferences();
        // ゲーム開始時は動けないように設定
        if (player != null) player.SetMoveAllowance(false);
        // カウントダウン開始
        StartCoroutine(StartCountDown());
    }

    IEnumerator StartCountDown()
    {
        if (countdownText == null)
        {
            if (player != null) player.SetMoveAllowance(true);
            yield break;
        }

        float timer = countdownSeconds;

        while (timer > 0)
        {
            countdownText.text = timer.ToString("F0"); // 整数で表示
            
            // 演出：数字を少し大きくするなどのアニメーション（任意）
            countdownText.transform.localScale = Vector3.one * 1.5f;
            
            yield return new WaitForSeconds(1f);
            
            timer--;
        }

        // 開始の合図
        countdownText.text = "GO!";
        if (player != null) player.SetMoveAllowance(true);

        // 1秒後に文字を消す
        yield return new WaitForSeconds(1f);
        countdownText.text = "";
    }

    // ゴール時などに外部から呼ぶ用
    public void OnPlayerGoal(string winnerName)
    {
        // プレイヤーの操作を止める
        if (player != null) player.SetMoveAllowance(false);

        // 結果を表示
        if (countdownText != null)
        {
            countdownText.text = $"<color=yellow>GOAL!!</color>\n1st: {winnerName}";
        }

        // 2秒後にタイトルへ戻るコルーチンを開始
        StartCoroutine(ReturnToTitleRoutine());
    }

    private IEnumerator ReturnToTitleRoutine()
    {
        // 2秒待機
        yield return new WaitForSeconds(2f);

        // "Title" という名前のシーンに遷移
        Initiate.Fade("Title", Color.black, 2f);
    }

    private void EnsureReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        if (countdownText == null)
        {
            var countObject = GameObject.Find("count");
            if (countObject != null)
            {
                countdownText = countObject.GetComponent<TextMeshProUGUI>();
            }
        }

        if (countdownText == null)
        {
            Canvas canvas = HudCanvasUtility.GetOrCreateHudCanvas();
            var textGo = new GameObject("count");
            textGo.transform.SetParent(canvas.transform, false);
            countdownText = textGo.AddComponent<TextMeshProUGUI>();
        }

        ConfigureCountdownText(countdownText);
    }

    private static void ConfigureCountdownText(TextMeshProUGUI text)
    {
        if (text == null) return;

        text.font = text.font != null ? text.font : TMP_Settings.defaultFontAsset;
        text.fontSize = 62f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.richText = true;

        RectTransform rt = text.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(440f, 180f);
    }
}
