using System;
using System.Collections;
using Kotatsu.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
    [Serializable]
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
    [SerializeField] private NetworkManager networkManager;

    [Header("Visual Settings")]
    [SerializeField] private CountdownSpriteSet[] characterCountdownSprites;
    public int selectedCharacterIndex;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countdownSE;
    [SerializeField] private AudioClip startSE;
    [SerializeField] private AudioClip goalSE;

    private bool countdownStarted;
    private bool subscribedToNetwork;

    private void Start()
    {
        EnsureReferences();
        SyncCharacterSelection();

        if (player != null)
        {
            player.SetMoveAllowance(false);
        }

        if (countdownImage != null)
        {
            countdownImage.gameObject.SetActive(false);
        }

        if (GText != null)
        {
            GText.text = string.Empty;
        }

        if (networkManager != null && networkManager.IsConnected && !networkManager.HasMatchConfiguration)
        {
            SubscribeToNetworkConfiguration();
            return;
        }

        BeginCountdown();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkConfiguration();
    }

    private void SubscribeToNetworkConfiguration()
    {
        if (networkManager == null || subscribedToNetwork)
        {
            return;
        }

        networkManager.OnMatchConfigurationUpdated += HandleMatchConfigurationUpdated;
        subscribedToNetwork = true;
    }

    private void UnsubscribeFromNetworkConfiguration()
    {
        if (networkManager == null || !subscribedToNetwork)
        {
            return;
        }

        networkManager.OnMatchConfigurationUpdated -= HandleMatchConfigurationUpdated;
        subscribedToNetwork = false;
    }

    private void HandleMatchConfigurationUpdated()
    {
        if (networkManager == null || !networkManager.HasMatchConfiguration)
        {
            return;
        }

        SyncCharacterSelection();
        BeginCountdown();
    }

    private void BeginCountdown()
    {
        if (countdownStarted)
        {
            return;
        }

        countdownStarted = true;
        UnsubscribeFromNetworkConfiguration();
        StartCoroutine(StartCountDown());
    }

    private IEnumerator StartCountDown()
    {
        if (countdownImage == null || characterCountdownSprites == null || characterCountdownSprites.Length <= selectedCharacterIndex)
        {
            if (player != null)
            {
                player.SetMoveAllowance(true);
            }
            yield break;
        }

        countdownImage.gameObject.SetActive(true);
        CountdownSpriteSet currentSet = characterCountdownSprites[selectedCharacterIndex];

        SetCountdownStep(currentSet.sprite3, countdownSE);
        yield return new WaitForSeconds(1f);

        SetCountdownStep(currentSet.sprite2, countdownSE);
        yield return new WaitForSeconds(1f);

        SetCountdownStep(currentSet.sprite1, countdownSE);
        yield return new WaitForSeconds(1f);

        SetCountdownStep(currentSet.spriteStart, startSE);
        if (player != null)
        {
            player.SetMoveAllowance(true);
        }

        yield return new WaitForSeconds(1f);
        countdownImage.gameObject.SetActive(false);
    }

    private void SetCountdownStep(Sprite nextSprite, AudioClip clip)
    {
        if (countdownImage != null)
        {
            countdownImage.sprite = nextSprite;
        }

        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnPlayerGoal(string winnerName)
    {
        if (player != null)
        {
            player.SetMoveAllowance(false);
        }

        if (audioSource != null && goalSE != null)
        {
            audioSource.PlayOneShot(goalSE);
        }

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

    private void SyncCharacterSelection()
    {
        if (networkManager != null &&
            networkManager.TryGetPlayerMatchState(networkManager.CurrentPlayerId, out MatchPlayerState playerState))
        {
            selectedCharacterIndex = playerState.color_index;
            if (player != null)
            {
                player.selectedCharacterIndex = selectedCharacterIndex;
            }
            return;
        }

        if (player != null)
        {
            selectedCharacterIndex = player.selectedCharacterIndex;
        }
    }

    private void EnsureReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        if (GText == null)
        {
            GameObject goalObject = GameObject.Find("G");
            if (goalObject != null)
            {
                GText = goalObject.GetComponent<TextMeshProUGUI>();
            }
        }

        if (countdownImage == null)
        {
            GameObject imageObject = GameObject.Find("count");
            if (imageObject != null)
            {
                countdownImage = imageObject.GetComponent<Image>();
            }
        }
    }
}
