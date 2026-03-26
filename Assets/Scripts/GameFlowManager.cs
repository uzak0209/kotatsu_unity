using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private PlayerController player; // インスペクターでプレイヤーをアサイン
    [SerializeField] private TextMeshProUGUI GText; // ゴール用UI
    [SerializeField] private Image countdownImage; // それ以外
    // カウントダウン用のスプライトとサウンド
    [SerializeField] private Sprite sprite3;
    [SerializeField] private Sprite sprite2;
    [SerializeField] private Sprite sprite1;
    [SerializeField] private Sprite spriteStart;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countdownSE; 
    [SerializeField] private AudioClip startSE;
    [SerializeField] private AudioClip goalSE;

    void Start()
    {
        EnsureReferences();
        // ゲーム開始時は動けないように設定
        if (player != null) player.SetMoveAllowance(false);
        countdownImage.gameObject.SetActive(false);
        GText.text = "";
        // カウントダウン開始
        StartCoroutine(StartCountDown());
    }

    IEnumerator StartCountDown()
    {
        if (countdownImage == null)
        {
            if (player != null) player.SetMoveAllowance(true);
            yield break;
        }

        countdownImage.gameObject.SetActive(true);

        SetCountdownStep(sprite3, countdownSE);
        yield return new WaitForSeconds(1f);

        SetCountdownStep(sprite2, countdownSE);
        yield return new WaitForSeconds(1f);

        SetCountdownStep(sprite1, countdownSE);
        yield return new WaitForSeconds(1f);

        SetCountdownStep(spriteStart, startSE);
        if (player != null) player.SetMoveAllowance(true);

        yield return new WaitForSeconds(1f);
        countdownImage.gameObject.SetActive(false);
    }

    private void SetCountdownStep(Sprite nextSprite, AudioClip clip)
    {
        countdownImage.sprite = nextSprite;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // 外部参照
    public void OnPlayerGoal(string winnerName)
    {
        if (player != null) player.SetMoveAllowance(false);
        if (audioSource != null && goalSE != null)
        {
            audioSource.PlayOneShot(goalSE);
        }

        // ゴール表示
        if (GText != null)
        {
            GText.text = $"<color=yellow>GOAL!!</color>\n1st: {winnerName}";
        }
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

        if (GText == null)
        {
            var goalObject = GameObject.Find("G");
            if (goalObject != null)
            {
                GText = goalObject.GetComponent<TextMeshProUGUI>();
            }
        }

        if (countdownImage == null)
        {
            var imageObject = GameObject.Find("count");
            if (imageObject != null)
            {
                countdownImage = imageObject.GetComponent<Image>();
            }
        }
    }
}
