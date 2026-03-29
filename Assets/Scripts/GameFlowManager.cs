using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
    [System.Serializable]
    public struct CountdownSpriteSet
    {
        public string characterName;
        public Sprite sprite3;
        public Sprite sprite2;
        public Sprite sprite1;
        public Sprite spriteStart;
    }

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private TextMeshProUGUI GText;
    [SerializeField] private Image countdownImage;

    [Header("Visual Settings")]
    [SerializeField] private CountdownSpriteSet[] characterCountdownSprites; // 4体分
    public int selectedCharacterIndex = 0; // ここで色（キャラ）を選択

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countdownSE; 
    [SerializeField] private AudioClip startSE;
    [SerializeField] private AudioClip goalSE;

    void Start()
    {
        EnsureReferences();
        
        // プレイヤー側とインデックスを同期させる（必要に応じて）
        if (player != null) selectedCharacterIndex = player.selectedCharacterIndex;

        if (player != null) player.SetMoveAllowance(false);
        
        if (countdownImage != null) countdownImage.gameObject.SetActive(false);
        if (GText != null) GText.text = "";

        StartCoroutine(StartCountDown());
    }

    IEnumerator StartCountDown()
    {
        if (countdownImage == null || characterCountdownSprites.Length <= selectedCharacterIndex)
        {
            if (player != null) player.SetMoveAllowance(true);
            yield break;
        }

        countdownImage.gameObject.SetActive(true);
        CountdownSpriteSet currentSet = characterCountdownSprites[selectedCharacterIndex];

        // 3
        SetCountdownStep(currentSet.sprite3, countdownSE);
        yield return new WaitForSeconds(1f);

        // 2
        SetCountdownStep(currentSet.sprite2, countdownSE);
        yield return new WaitForSeconds(1f);

        // 1
        SetCountdownStep(currentSet.sprite1, countdownSE);
        yield return new WaitForSeconds(1f);

        // START!
        SetCountdownStep(currentSet.spriteStart, startSE);
        if (player != null) player.SetMoveAllowance(true);

        yield return new WaitForSeconds(1f);
        countdownImage.gameObject.SetActive(false);
    }

    private void SetCountdownStep(Sprite nextSprite, AudioClip clip)
    {
        if (countdownImage != null) countdownImage.sprite = nextSprite;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnPlayerGoal(string winnerName)
    {
        if (player != null) player.SetMoveAllowance(false);
        if (audioSource != null && goalSE != null) audioSource.PlayOneShot(goalSE);

        if (GText != null)
        {
            GText.text = $"<color=yellow>GOAL!!</color>\n1st: {winnerName}";
        }
        StartCoroutine(ReturnToTitleRoutine());
    }

    private IEnumerator ReturnToTitleRoutine()
    {
        yield return new WaitForSeconds(2f);
        Initiate.Fade("Title", Color.black, 2f);
    }

    private void EnsureReferences()
    {
        if (player == null) player = Object.FindFirstObjectByType<PlayerController>();

        if (GText == null)
        {
            var goalObject = GameObject.Find("G");
            if (goalObject != null) GText = goalObject.GetComponent<TextMeshProUGUI>();
        }

        if (countdownImage == null)
        {
            var imageObject = GameObject.Find("count");
            if (imageObject != null) countdownImage = imageObject.GetComponent<Image>();
        }
    }
}