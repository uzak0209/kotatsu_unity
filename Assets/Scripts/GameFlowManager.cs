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
        // ゲーム開始時は動けないように設定
        if (player != null) player.SetMoveAllowance(false);
        // カウントダウン開始
        StartCoroutine(StartCountDown());
    }

    IEnumerator StartCountDown()
    {
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
        countdownText.text = $"<color=yellow>GOAL!!</color>\n1st: {winnerName}";

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
}