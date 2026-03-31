using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))] // CanvasGroupのアタッチを必須にする
public class LogItem : MonoBehaviour
{
    [SerializeField, Header("1枚目のイラスト(何が)")] 
    private Image imageBefore;
    
    [SerializeField] 
    private TextMeshProUGUI textAfter;

    [SerializeField, Header("フェードアウトにかける時間(秒)")]
    private float fadeDuration = 1.0f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // アタッチされているCanvasGroupを取得しておく
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // ログを表示して、指定時間後にフェードアウト＆消去するメソッド
    public void Setup(Sprite spriteBefore, string afterStrings, float displayTime)
    {
        // 画像をセット
        imageBefore.sprite = spriteBefore;
        textAfter.text = afterStrings;

        // コルーチンを開始
        StartCoroutine(FadeOutAndDestroy(displayTime));
    }

    // フェードアウトして破棄するコルーチン
    private IEnumerator FadeOutAndDestroy(float displayTime)
    {
        // ① まずは指定された時間（表示時間）だけ待機する
        yield return new WaitForSeconds(displayTime);

        // ② フェードアウト処理
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            // Mathf.Lerpを使って、アルファ値を 1(不透明) から 0(透明) へ徐々に変化させる
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            
            // 1フレーム待機（これがないと一瞬で処理が終わってしまいます）
            yield return null; 
        }

        // 念のため完全に透明にする
        canvasGroup.alpha = 0f;

        // ③ 完全に透明になったら、このオブジェクト(ログ)を破棄する
        Destroy(gameObject);
    }
}