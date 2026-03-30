using Kotatsu.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSceneBackgroundController : MonoBehaviour
{
    private const string TargetSceneName = "Game";
    private const string BackgroundObjectName = "Image";

    [SerializeField] private Image backgroundImage;
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private LocationSpriteDatabase locationSpriteDatabase;

    private bool subscribed;
    private int lastAppliedCharacterIndex = -1;
    private float reconnectTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, TargetSceneName, System.StringComparison.Ordinal))
        {
            return;
        }

        if (FindAnyObjectByType<GameSceneBackgroundController>() != null)
        {
            return;
        }

        if (GameObject.Find(BackgroundObjectName) == null)
        {
            return;
        }

        GameObject controller = new GameObject(nameof(GameSceneBackgroundController));
        controller.AddComponent<GameSceneBackgroundController>();
    }

    private void Awake()
    {
        EnsureReferences();
        TryBindNetworkManager();
        ApplyBackgroundIfPossible();
    }

    private void Update()
    {
        if (backgroundImage == null || playerController == null || locationSpriteDatabase == null)
        {
            EnsureReferences();
        }

        if (networkManager == null || !subscribed)
        {
            reconnectTimer += Time.deltaTime;
            if (reconnectTimer >= 1f)
            {
                reconnectTimer = 0f;
                TryBindNetworkManager();
            }
        }

        ApplyBackgroundIfPossible();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkManager();
    }

    private void HandleMatchConfigurationUpdated()
    {
        ApplyBackgroundIfPossible(force: true);
    }

    private void HandleMatchJoined(string matchId, string playerId)
    {
        ApplyBackgroundIfPossible(force: true);
    }

    private void HandleGameConnected()
    {
        ApplyBackgroundIfPossible(force: true);
    }

    private void HandleGameDisconnected()
    {
        ApplyBackgroundIfPossible(force: true);
    }

    private void EnsureReferences()
    {
        if (backgroundImage == null)
        {
            GameObject backgroundObject = GameObject.Find(BackgroundObjectName);
            if (backgroundObject != null)
            {
                backgroundImage = backgroundObject.GetComponent<Image>();
            }
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        if (locationSpriteDatabase == null)
        {
            locationSpriteDatabase = Resources.Load<LocationSpriteDatabase>("LocationSpriteDatabase");
        }
    }

    private void TryBindNetworkManager()
    {
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        if (networkManager == null || subscribed)
        {
            return;
        }

        networkManager.OnMatchConfigurationUpdated += HandleMatchConfigurationUpdated;
        networkManager.OnMatchJoined += HandleMatchJoined;
        networkManager.OnGameConnected += HandleGameConnected;
        networkManager.OnGameDisconnected += HandleGameDisconnected;
        subscribed = true;
    }

    private void UnsubscribeFromNetworkManager()
    {
        if (networkManager == null || !subscribed)
        {
            return;
        }

        networkManager.OnMatchConfigurationUpdated -= HandleMatchConfigurationUpdated;
        networkManager.OnMatchJoined -= HandleMatchJoined;
        networkManager.OnGameConnected -= HandleGameConnected;
        networkManager.OnGameDisconnected -= HandleGameDisconnected;
        subscribed = false;
    }

    private void ApplyBackgroundIfPossible(bool force = false)
    {
        if (backgroundImage == null || locationSpriteDatabase == null)
        {
            return;
        }

        if (!TryResolveCharacterIndex(out int characterIndex))
        {
            return;
        }

        if (!force && characterIndex == lastAppliedCharacterIndex)
        {
            return;
        }

        Sprite backgroundSprite = locationSpriteDatabase.GetGameBackground(characterIndex);
        if (backgroundSprite == null)
        {
            return;
        }

        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = Color.white;
        lastAppliedCharacterIndex = characterIndex;
    }

    private bool TryResolveCharacterIndex(out int characterIndex)
    {
        if (networkManager != null &&
            networkManager.TryGetPlayerMatchState(networkManager.CurrentPlayerId, out MatchPlayerState playerState))
        {
            characterIndex = Mathf.Clamp(playerState.color_index, 0, 3);
            return true;
        }

        if (playerController != null)
        {
            characterIndex = Mathf.Clamp(playerController.selectedCharacterIndex, 0, 3);
            return true;
        }

        characterIndex = 0;
        return false;
    }
}
