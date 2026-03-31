using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kotatsu.Network
{
    public class TitleLobbyUI : MonoBehaviour
    {
        [Serializable]
        private class KnownLobbySnapshot
        {
            public string matchId;
            public int playerCount;
            public int maxPlayers;
            public long startedAtUnix;
            public bool isReachable;
            public long updatedAtUnix;
        }

        private enum UiSceneMode
        {
            TitleMenu,
            LobbyCreate,
            LobbyJoin,
        }
        [SerializeField]
        private GameObject[] selectObject;

        private const string KnownLobbiesPrefsKey = "Kotatsu.Network.KnownLobbies";
        private const int MaxKnownLobbies = 6;
        private const float ActiveLobbyStartPollInterval = 0.2f;

        [Header("Scene")]
        [SerializeField] private string titleCanvasName = "main";
        [SerializeField] private string createButtonName = "online";
        [SerializeField] private string joinButtonName = "offline";
        [SerializeField] private string startButtonName = "manual";
        [SerializeField] private string exitButtonName = "exit";
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private string createLobbySceneName = "LobbyCreate";
        [SerializeField] private string joinLobbySceneName = "LobbyJoin";
        [SerializeField] private string gameSceneName = "(Test)";

        [Header("Network")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private bool requireConnectedToStart = true;
        [SerializeField] private float lobbyInfoRefreshInterval = 1.0f;
        [SerializeField] private float lobbyPreviewDelay = 0.35f;

        [Header("Fade")]
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private float fadeDuration = 1.0f;
        [SerializeField] private bool forceKeyboardArrowNavigation = true;
        [SerializeField] private bool forceKeyboardSubmit = true;

        private Button createLobbyButton;
        private Button joinLobbyButton;
        private Button startGameButton;
        private Button exitButton;
        private InputField matchIdInput;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI lobbyInfoText;
        private RectTransform knownLobbiesPanelRoot;
        private TextMeshProUGUI knownLobbiesEmptyText;

        private UiSceneMode sceneMode;
        private string currentMatchId;
        private string currentPlayerId;
        private bool isConnected;
        private bool createRequestInFlight;
        private bool joinRequestInFlight;
        private bool matchStartRequestInFlight;
        private bool eventsBound;
        private Transform uiRoot;
        private Transform titleVisual;
        private TMP_FontAsset uiFontAsset;
        private Coroutine currentLobbyInfoPolling;
        private Coroutine previewLobbyInfoPolling;
        private Coroutine lobbyListPolling;
        private bool sceneTransitionInProgress;
        private bool lobbyListRequestInFlight;
        private float lastSubmitFallbackTime = -10f;
        private InputSystemUIInputModule uiInputModule;
        private InputActionAsset runtimeUiPointerActionsAsset;
        private InputActionReference runtimeUiPointActionReference;
        private InputActionReference runtimeUiLeftClickActionReference;
        private Coroutine sceneLoadFallbackCoroutine;
        private string selectedKnownLobbyMatchId;
        private long currentMatchStartedAtUnix;
        private readonly List<KnownLobbySnapshot> knownLobbies = new List<KnownLobbySnapshot>();
        private readonly List<Button> knownLobbyButtons = new List<Button>();

        private void Start()
        {
            DetermineSceneMode();
            EnsureNetworkManager();
            LoadKnownLobbies();
            EnsureUi();
            BindNetworkEvents();
            SyncStateFromNetworkManager();
            EnterSceneMode();
            RefreshUiState();
            FocusDefaultButtonIfNeeded();
            EnsureUiInputModuleBindings();
        }

        private void Update()
        {
            EnsureSelectionStillValid();

            HandleKnownLobbyNumberSelection();

            if (forceKeyboardArrowNavigation && ShouldUseKeyboardNavigationFallback())
            {
                HandleKeyboardNavigationFallback();
            }

            if (forceKeyboardSubmit && (ShouldUseKeyboardSubmitFallback() || IsEnterSubmitPressedThisFrame()))
            {
                HandleKeyboardSubmitFallback();
            }

            SyncKnownLobbySelectionFromEventSystem();
            // switch (sceneMode)
            // {
            //     case UiSceneMode.LobbyCreate:
            //         selectObject[0].SetActive(true);
            //         selectObject[1].SetActive(false);
            //         selectObject[2].SetActive(false);
            //         break;
            //     case UiSceneMode.LobbyJoin:
            //         selectObject[0].SetActive(false);
            //         selectObject[1].SetActive(true);
            //         selectObject[2].SetActive(false);
            //          break;
            //     case UiSceneMode.TitleMenu:
            //         selectObject[0].SetActive(false);
            //         selectObject[1].SetActive(false);
            //         selectObject[2].SetActive(true);
            //         break;
            // }
        }

        private void OnDestroy()
        {
            StopLobbyInfoPolling();
            UnbindNetworkEvents();

            if (matchIdInput != null)
            {
                matchIdInput.onValueChanged.RemoveListener(OnMatchIdInputChanged);
            }

            if (sceneLoadFallbackCoroutine != null)
            {
                StopCoroutine(sceneLoadFallbackCoroutine);
                sceneLoadFallbackCoroutine = null;
            }

            DisposeFallbackUiActions();
        }

        private void DetermineSceneMode()
        {
            string activeName = SceneManager.GetActiveScene().name;
            if (string.Equals(activeName, createLobbySceneName, StringComparison.Ordinal))
            {
                sceneMode = UiSceneMode.LobbyCreate;
                return;
            }

            if (string.Equals(activeName, joinLobbySceneName, StringComparison.Ordinal))
            {
                sceneMode = UiSceneMode.LobbyJoin;
                return;
            }

            sceneMode = UiSceneMode.TitleMenu;
        }

        private void EnsureNetworkManager()
        {
            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>();
            }

            if (networkManager == null)
            {
                var go = new GameObject("NetworkManager");
                networkManager = go.AddComponent<NetworkManager>();
            }

            if (networkManager != null)
            {
                DontDestroyOnLoad(networkManager.gameObject);
            }
        }

        private void LoadKnownLobbies()
        {
            knownLobbies.Clear();
            if (PlayerPrefs.HasKey(KnownLobbiesPrefsKey))
            {
                PlayerPrefs.DeleteKey(KnownLobbiesPrefsKey);
                PlayerPrefs.Save();
            }
        }

        private void SaveKnownLobbies()
        {
            SortKnownLobbies();
        }

        private void SortKnownLobbies()
        {
            knownLobbies.Sort((left, right) =>
            {
                if (left == null && right == null) return 0;
                if (left == null) return 1;
                if (right == null) return -1;

                int reachability = right.isReachable.CompareTo(left.isReachable);
                if (reachability != 0)
                {
                    return reachability;
                }

                int updatedOrder = right.updatedAtUnix.CompareTo(left.updatedAtUnix);
                if (updatedOrder != 0)
                {
                    return updatedOrder;
                }

                return string.Compare(left.matchId, right.matchId, StringComparison.Ordinal);
            });
        }

        private KnownLobbySnapshot FindKnownLobby(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return null;
            }

            string trimmed = matchId.Trim();
            for (int i = 0; i < knownLobbies.Count; i++)
            {
                KnownLobbySnapshot candidate = knownLobbies[i];
                if (candidate != null && string.Equals(candidate.matchId, trimmed, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RememberKnownLobby(string matchId, MatchmakingClient.MatchInfo info = null)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return;
            }

            string trimmed = matchId.Trim();
            KnownLobbySnapshot snapshot = FindKnownLobby(trimmed);
            if (snapshot == null)
            {
                snapshot = new KnownLobbySnapshot { matchId = trimmed };
                knownLobbies.Add(snapshot);
            }

            snapshot.matchId = trimmed;
            snapshot.isReachable = true;
            snapshot.updatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (info != null)
            {
                snapshot.playerCount = info.players != null ? info.players.Length : 0;
                snapshot.maxPlayers = info.max_players;
                snapshot.startedAtUnix = info.started_at_unix;
            }

            SaveKnownLobbies();
            RefreshKnownLobbiesListUi();
        }

        private void EnsureUi()
        {
            // Legacy focus keeper conflicts with explicit navigation in this controller.
            var legacyKeepFocus = GetComponent<keepfocus>();
            if (legacyKeepFocus != null)
            {
                legacyKeepFocus.enabled = false;
            }

            GameObject rootGo = GameObject.Find(titleCanvasName);
            if (rootGo == null)
            {
                Debug.LogError($"TitleLobbyUI: Canvas '{titleCanvasName}' not found.");
                return;
            }

            uiRoot = rootGo.transform;
            titleVisual = uiRoot.Find("title");
            createLobbyButton = FindButton(createButtonName);
            joinLobbyButton = FindButton(joinButtonName);
            startGameButton = FindButton(startButtonName);
            exitButton = FindButton(exitButtonName);

            if (createLobbyButton != null)
            {
                createLobbyButton.onClick = new Button.ButtonClickedEvent();
                createLobbyButton.onClick.AddListener(OnPrimaryButtonClicked);
                DisableLegacyButtonBehaviours(createLobbyButton.gameObject);
                NormalizeButtonColors(createLobbyButton, alphaNormal: 0f);
            }

            if (joinLobbyButton != null)
            {
                joinLobbyButton.onClick = new Button.ButtonClickedEvent();
                joinLobbyButton.onClick.AddListener(OnSecondaryButtonClicked);
                DisableLegacyButtonBehaviours(joinLobbyButton.gameObject);
                NormalizeButtonColors(joinLobbyButton, alphaNormal: 0f);
            }

            if (startGameButton != null)
            {
                startGameButton.onClick = new Button.ButtonClickedEvent();
                startGameButton.onClick.AddListener(OnStartGameClicked);
                DisableLegacyButtonBehaviours(startGameButton.gameObject);
                NormalizeButtonColors(startGameButton, alphaNormal: 0f);
            }

            if (exitButton != null)
            {
                exitButton.onClick = new Button.ButtonClickedEvent();
                exitButton.onClick.AddListener(OnExitClicked);
                DisableLegacyButtonBehaviours(exitButton.gameObject);
                NormalizeButtonColors(exitButton, alphaNormal: 0f);
            }

            matchIdInput = EnsureMatchIdInput();
            if (matchIdInput != null)
            {
                matchIdInput.onValueChanged.RemoveListener(OnMatchIdInputChanged);
                matchIdInput.onValueChanged.AddListener(OnMatchIdInputChanged);
            }

            statusText = EnsureStatusText();
            lobbyInfoText = EnsureLobbyInfoText();
            EnsureKnownLobbiesPanel();

            ConfigureModeLabelsAndVisibility();
            RefreshKnownLobbiesListUi();
            ConfigureButtonNavigation();
        }

        private void ConfigureModeLabelsAndVisibility()
        {
            if (createLobbyButton != null)
            {
                ConfigureButtonLabel(createLobbyButton, sceneMode == UiSceneMode.LobbyJoin ? "参加" : "ロビー作成");
            }

            if (joinLobbyButton != null)
            {
                string secondaryLabel = sceneMode == UiSceneMode.TitleMenu ? "ロビー参加" : "タイトルへ戻る";
                ConfigureButtonLabel(joinLobbyButton, secondaryLabel);
            }

            if (startGameButton != null)
            {
                ConfigureButtonLabel(startGameButton, "ゲーム開始");
                startGameButton.gameObject.SetActive(sceneMode == UiSceneMode.LobbyCreate);
            }

            if (exitButton != null)
            {
                ConfigureButtonLabel(exitButton, "終了");
                exitButton.gameObject.SetActive(sceneMode == UiSceneMode.TitleMenu);
            }

            if (matchIdInput != null)
            {
                matchIdInput.gameObject.SetActive(sceneMode == UiSceneMode.LobbyCreate);
            }

            if (titleVisual != null)
            {
                // titleVisual.gameObject.SetActive(sceneMode != UiSceneMode.LobbyJoin);
            }
        }

        private Button FindButton(string objectName)
        {
            if (uiRoot == null || string.IsNullOrEmpty(objectName)) return null;
            Transform t = uiRoot.Find(objectName);
            if (t == null) return null;
            return t.GetComponent<Button>();
        }

        private TMP_FontAsset ResolveUiFont()
        {
            if (uiFontAsset != null)
            {
                return uiFontAsset;
            }

            if (createLobbyButton != null)
            {
                TMP_Text label = createLobbyButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.font != null)
                {
                    uiFontAsset = label.font;
                    return uiFontAsset;
                }
            }

            uiFontAsset = TMP_Settings.defaultFontAsset;
            return uiFontAsset;
        }

        private static void DisableLegacyButtonBehaviours(GameObject buttonObject)
        {
            if (buttonObject == null) return;

            var sceneChange = buttonObject.GetComponent<scenechange>();
            if (sceneChange != null)
            {
                sceneChange.enabled = false;
            }

            var manualSwitch = buttonObject.GetComponent<manual>();
            if (manualSwitch != null)
            {
                manualSwitch.enabled = false;
            }

            var exitLegacy = buttonObject.GetComponent<exit>();
            if (exitLegacy != null)
            {
                exitLegacy.enabled = false;
            }
        }

        private static void ConfigureButtonLabel(Button button, string text)
        {
            if (button == null) return;
            TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = text;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 16f;
                tmp.fontSizeMax = 40f;
                tmp.alignment = TextAlignmentOptions.Center;

                RectTransform textRt = tmp.rectTransform;
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.anchoredPosition = Vector2.zero;
                textRt.offsetMin = new Vector2(8f, 4f);
                textRt.offsetMax = new Vector2(-8f, -4f);
            }
        }

        private static void NormalizeButtonColors(Selectable selectable, float alphaNormal)
        {
            if (selectable == null)
            {
                return;
            }

            ColorBlock colors = selectable.colors;
            colors.normalColor = new Color(1f, 1f, 1f, Mathf.Clamp01(alphaNormal));
            colors.highlightedColor = new Color(0.78f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.58f, 1f, 1f, 1f);
            colors.selectedColor = new Color(0.78f, 1f, 1f, 1f);
            colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.45f);
            colors.colorMultiplier = 1f;
            selectable.colors = colors;
        }

        private InputField EnsureMatchIdInput()
        {
            if (uiRoot == null) return null;

            Transform existing = uiRoot.Find("LobbyMatchIdInput");
            if (existing != null)
            {
                InputField existingField = existing.GetComponent<InputField>();
                if (existingField != null)
                {
                    ConfigureMatchInputLayout(existingField);
                    ConfigureMatchInputVisual(existingField);
                    return existingField;
                }
            }

            var inputGo = new GameObject("LobbyMatchIdInput");
            inputGo.transform.SetParent(uiRoot, false);

            var rt = inputGo.AddComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = inputGo.AddComponent<Image>();
            bg.color = Color.clear;

            var input = inputGo.AddComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 64;
            input.text = "";

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(inputGo.transform, false);
            var placeholderRt = placeholderGo.AddComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = new Vector2(12f, 6f);
            placeholderRt.offsetMax = new Vector2(-12f, -6f);
            var placeholderText = placeholderGo.AddComponent<Text>();
            placeholderText.font = font;
            placeholderText.fontSize = 18;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            placeholderText.raycastTarget = false;
            placeholderText.text = "Lobby ID";

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 6f);
            textRt.offsetMax = new Vector2(-12f, -6f);
            var inputText = textGo.AddComponent<Text>();
            inputText.font = font;
            inputText.fontSize = 18;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.color = Color.black;
            inputText.raycastTarget = false;
            inputText.supportRichText = false;

            input.placeholder = placeholderText;
            input.textComponent = inputText;
            ConfigureMatchInputLayout(input);
            ConfigureMatchInputVisual(input);

            return input;
        }

        private TextMeshProUGUI EnsureStatusText()
        {
            if (uiRoot == null) return null;

            Transform existing = uiRoot.Find("LobbyStatus");
            if (existing != null)
            {
                var found = existing.GetComponent<TextMeshProUGUI>();
                if (found != null)
                {
                    found.font = ResolveUiFont();
                    ConfigureStatusTextAppearance(found);
                    ConfigureStatusTextLayout(found);
                    return found;
                }
            }

            var textGo = new GameObject("LobbyStatus");
            textGo.transform.SetParent(uiRoot, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.font = ResolveUiFont();
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.black;
            tmp.raycastTarget = false;
            ConfigureStatusTextAppearance(tmp);
            tmp.text = "";
            ConfigureStatusTextLayout(tmp);
            return tmp;
        }

        private TextMeshProUGUI EnsureLobbyInfoText()
        {
            if (uiRoot == null) return null;

            Transform existing = uiRoot.Find("LobbyInfo");
            if (existing != null)
            {
                var found = existing.GetComponent<TextMeshProUGUI>();
                if (found != null)
                {
                    found.font = ResolveUiFont();
                    ConfigureLobbyInfoTextAppearance(found);
                    ConfigureLobbyInfoTextLayout(found);
                    return found;
                }
            }

            var textGo = new GameObject("LobbyInfo");
            textGo.transform.SetParent(uiRoot, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.font = ResolveUiFont();
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.black;
            tmp.raycastTarget = false;
            ConfigureLobbyInfoTextAppearance(tmp);
            tmp.text = "";
            ConfigureLobbyInfoTextLayout(tmp);
            return tmp;
        }

        private void EnsureKnownLobbiesPanel()
        {
            if (uiRoot == null)
            {
                return;
            }

            if (knownLobbiesPanelRoot != null)
            {
                return;
            }

            Transform existing = uiRoot.Find("KnownLobbiesPanel");
            if (existing != null)
            {
                knownLobbiesPanelRoot = existing.GetComponent<RectTransform>();
                knownLobbiesEmptyText = existing.Find("EmptyText")?.GetComponent<TextMeshProUGUI>();
                knownLobbyButtons.Clear();
                Button[] existingButtons = existing.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < existingButtons.Length; i++)
                {
                    knownLobbyButtons.Add(existingButtons[i]);
                }
                ConfigureKnownLobbiesPanelLayout(knownLobbiesPanelRoot);
                return;
            }

            var panelGo = new GameObject("KnownLobbiesPanel");
            panelGo.transform.SetParent(uiRoot, false);
            knownLobbiesPanelRoot = panelGo.AddComponent<RectTransform>();
            ConfigureKnownLobbiesPanelLayout(knownLobbiesPanelRoot);

            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.28f);

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateKnownLobbiesHeader();
            knownLobbiesEmptyText = CreateKnownLobbiesEmptyText();
        }

        private void ConfigureKnownLobbiesPanelLayout(RectTransform panelRoot)
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.anchorMin = new Vector2(0.08f, 0.20f);
            panelRoot.anchorMax = new Vector2(0.48f, 0.78f);
            panelRoot.offsetMin = Vector2.zero;
            panelRoot.offsetMax = Vector2.zero;
        }

        private void CreateKnownLobbiesHeader()
        {
            if (knownLobbiesPanelRoot == null)
            {
                return;
            }

            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(knownLobbiesPanelRoot, false);
            var headerLayout = headerGo.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 30f;

            var header = headerGo.AddComponent<TextMeshProUGUI>();
            header.font = ResolveUiFont();
            header.fontSize = 20f;
            header.color = Color.black;
            header.alignment = TextAlignmentOptions.Left;
            header.text = "ロビー一覧";
        }

        private TextMeshProUGUI CreateKnownLobbiesEmptyText()
        {
            if (knownLobbiesPanelRoot == null)
            {
                return null;
            }

            var emptyGo = new GameObject("EmptyText");
            emptyGo.transform.SetParent(knownLobbiesPanelRoot, false);
            var layout = emptyGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 44f;

            var emptyText = emptyGo.AddComponent<TextMeshProUGUI>();
            emptyText.font = ResolveUiFont();
            emptyText.fontSize = 16f;
            emptyText.color = new Color(0f, 0f, 0f, 0.75f);
            emptyText.alignment = TextAlignmentOptions.TopLeft;
            emptyText.textWrappingMode = TextWrappingModes.Normal;
            emptyText.text = "参加できるロビーがありません。";
            return emptyText;
        }

        private Button EnsureKnownLobbyButton(int index)
        {
            while (knownLobbyButtons.Count <= index)
            {
                knownLobbyButtons.Add(CreateKnownLobbyButton(knownLobbyButtons.Count));
            }

            return knownLobbyButtons[index];
        }

        private Button CreateKnownLobbyButton(int index)
        {
            var buttonGo = new GameObject($"KnownLobbyButton_{index + 1}");
            buttonGo.transform.SetParent(knownLobbiesPanelRoot, false);

            var rect = buttonGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 56f);

            var layout = buttonGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 56f;

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.12f);

            var button = buttonGo.AddComponent<Button>();
            NormalizeButtonColors(button, alphaNormal: 0.12f);
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font = ResolveUiFont();
            label.fontSize = 16f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = string.Empty;
            label.raycastTarget = false;

            return button;
        }

        private static void ConfigureMatchInputLayout(InputField input)
        {
            if (input == null) return;
            RectTransform rt = input.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.08f, 0.08f);
                rt.anchorMax = new Vector2(0.42f, 0.16f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        private static void ConfigureMatchInputVisual(InputField input)
        {
            if (input == null) return;

            Image background = input.GetComponent<Image>();
            if (background != null)
            {
                background.color = Color.clear;
            }

            if (input.placeholder is Graphic placeholderGraphic)
            {
                placeholderGraphic.raycastTarget = false;
                if (placeholderGraphic is Text placeholderText)
                {
                    placeholderText.text = "Lobby ID";
                }
            }

            if (input.textComponent != null)
            {
                input.textComponent.raycastTarget = false;
            }

            input.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private static void ConfigureStatusTextLayout(TextMeshProUGUI text)
        {
            if (text == null) return;
            RectTransform rt = text.rectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.08f, 0.14f);
                rt.anchorMax = new Vector2(0.48f, 0.19f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        private static void ConfigureLobbyInfoTextLayout(TextMeshProUGUI text)
        {
            if (text == null) return;
            RectTransform rt = text.rectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.08f, 0.08f);
                rt.anchorMax = new Vector2(0.48f, 0.13f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        private static void ConfigureStatusTextAppearance(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.fontSize = 17f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.maxVisibleLines = 1;
            text.enableAutoSizing = false;
        }

        private static void ConfigureLobbyInfoTextAppearance(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.maxVisibleLines = 1;
            text.enableAutoSizing = false;
        }

        private void RefreshKnownLobbiesListUi()
        {
            if (knownLobbiesPanelRoot == null)
            {
                return;
            }

            bool showPanel = sceneMode == UiSceneMode.LobbyJoin;
            knownLobbiesPanelRoot.gameObject.SetActive(showPanel);
            if (!showPanel)
            {
                return;
            }

            SortKnownLobbies();

            List<KnownLobbySnapshot> displayLobbies = BuildDisplayKnownLobbies();
            int displayCount = Mathf.Min(displayLobbies.Count, MaxKnownLobbies);
            if (sceneMode == UiSceneMode.LobbyJoin)
            {
                EnsureSelectedKnownLobby(displayLobbies, displayCount);
            }

            if (knownLobbiesEmptyText != null)
            {
                knownLobbiesEmptyText.gameObject.SetActive(displayCount == 0);
            }

            for (int i = 0; i < MaxKnownLobbies; i++)
            {
                Button button = EnsureKnownLobbyButton(i);
                bool active = i < displayCount;
                button.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                KnownLobbySnapshot snapshot = displayLobbies[i];
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = BuildKnownLobbyButtonLabel(snapshot, i);
                }

                string matchId = snapshot.matchId;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => OnKnownLobbySelected(matchId));

                bool selectableLobby = IsKnownLobbySelectable(snapshot);
                button.interactable = !joinRequestInFlight && !isConnected && selectableLobby;

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    bool isSelected = string.Equals(selectedKnownLobbyMatchId, matchId, StringComparison.Ordinal);
                    image.color = isSelected
                        ? new Color(0.78f, 1f, 1f, 0.30f)
                        : new Color(1f, 1f, 1f, 0.12f);
                }
            }

            UpdateSelectedLobbyInfoText();
            UpdateJoinModePrimaryButtonInteractable();
            ConfigureButtonNavigation();
            FocusDefaultButtonIfNeeded();
        }

        private List<KnownLobbySnapshot> BuildDisplayKnownLobbies()
        {
            var display = new List<KnownLobbySnapshot>(knownLobbies.Count);
            for (int i = 0; i < knownLobbies.Count; i++)
            {
                KnownLobbySnapshot snapshot = knownLobbies[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.matchId))
                {
                    continue;
                }

                if (sceneMode == UiSceneMode.LobbyJoin && !snapshot.isReachable)
                {
                    continue;
                }

                display.Add(snapshot);
            }

            return display;
        }

        private static string BuildKnownLobbyButtonLabel(KnownLobbySnapshot snapshot, int displayIndex)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.matchId))
            {
                return string.Empty;
            }

            string occupancy = snapshot.maxPlayers > 0
                ? $"{snapshot.playerCount}/{snapshot.maxPlayers}"
                : $"{snapshot.playerCount}/?";

            string state;
            if (!snapshot.isReachable)
            {
                state = "見つかりません";
            }
            else if (snapshot.startedAtUnix > 0)
            {
                state = "開始済み";
            }
            else if (snapshot.maxPlayers > 0 && snapshot.playerCount >= snapshot.maxPlayers)
            {
                state = "満員";
            }
            else
            {
                state = "参加可能";
            }

            int lobbyNumber = Mathf.Clamp(displayIndex + 1, 1, MaxKnownLobbies);
            return $"{lobbyNumber}. {snapshot.matchId}   {occupancy}   {state}";
        }

        private void OnKnownLobbySelected(string matchId)
        {
            SelectKnownLobby(matchId);
        }

        private bool IsKnownLobbySelectable(KnownLobbySnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.matchId))
            {
                return false;
            }

            if (!snapshot.isReachable)
            {
                return false;
            }

            if (snapshot.startedAtUnix > 0)
            {
                return false;
            }

            return snapshot.maxPlayers <= 0 || snapshot.playerCount < snapshot.maxPlayers;
        }

        private void EnsureSelectedKnownLobby(List<KnownLobbySnapshot> displayLobbies, int displayCount)
        {
            if (displayLobbies == null || displayCount <= 0)
            {
                selectedKnownLobbyMatchId = string.Empty;
                return;
            }

            for (int i = 0; i < displayCount; i++)
            {
                KnownLobbySnapshot snapshot = displayLobbies[i];
                if (snapshot == null)
                {
                    continue;
                }

                if (string.Equals(snapshot.matchId, selectedKnownLobbyMatchId, StringComparison.Ordinal) && IsKnownLobbySelectable(snapshot))
                {
                    return;
                }
            }

            selectedKnownLobbyMatchId = string.Empty;
            for (int i = 0; i < displayCount; i++)
            {
                KnownLobbySnapshot snapshot = displayLobbies[i];
                if (IsKnownLobbySelectable(snapshot))
                {
                    selectedKnownLobbyMatchId = snapshot.matchId;
                    return;
                }
            }
        }

        private void UpdateSelectedLobbyInfoText()
        {
            if (sceneMode != UiSceneMode.LobbyJoin)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedKnownLobbyMatchId))
            {
                SetLobbyInfo("1-6キーでロビーを選んで「参加」を押してください。");
                return;
            }

            KnownLobbySnapshot snapshot = FindKnownLobby(selectedKnownLobbyMatchId);
            if (snapshot == null)
            {
                SetLobbyInfo("1-6キーでロビーを選んで「参加」を押してください。");
                return;
            }

            if (snapshot.startedAtUnix > 0)
            {
                SetLobbyInfo($"選択中: {snapshot.matchId} / 開始済み");
                return;
            }

            string occupancy = snapshot.maxPlayers > 0
                ? $"{snapshot.playerCount}/{snapshot.maxPlayers}"
                : $"{snapshot.playerCount}/?";
            SetLobbyInfo($"選択中: {snapshot.matchId} / 人数 {occupancy}");
        }

        private void SyncKnownLobbySelectionFromEventSystem()
        {
            if (sceneMode != UiSceneMode.LobbyJoin)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                return;
            }

            List<KnownLobbySnapshot> displayLobbies = BuildDisplayKnownLobbies();
            for (int i = 0; i < knownLobbyButtons.Count && i < displayLobbies.Count; i++)
            {
                Button button = knownLobbyButtons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (button.gameObject != eventSystem.currentSelectedGameObject)
                {
                    continue;
                }

                string matchId = displayLobbies[i]?.matchId;
                if (!string.IsNullOrWhiteSpace(matchId) && !string.Equals(selectedKnownLobbyMatchId, matchId, StringComparison.Ordinal))
                {
                    SelectKnownLobby(matchId);
                }
                return;
            }
        }

        private void SelectKnownLobby(string matchId, bool focusJoinAction = false)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return;
            }

            string trimmed = matchId.Trim();
            bool selectionChanged = !string.Equals(selectedKnownLobbyMatchId, trimmed, StringComparison.Ordinal);
            selectedKnownLobbyMatchId = trimmed;
            RefreshKnownLobbiesListUi();

            if (focusJoinAction)
            {
                FocusJoinActionButton();
            }

            if (selectionChanged)
            {
                QueueJoinPreview();
            }
        }

        private void FocusJoinActionButton()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || sceneMode != UiSceneMode.LobbyJoin)
            {
                return;
            }

            if (IsSelectableUsable(createLobbyButton))
            {
                eventSystem.SetSelectedGameObject(createLobbyButton.gameObject);
                return;
            }

            if (IsSelectableUsable(joinLobbyButton))
            {
                eventSystem.SetSelectedGameObject(joinLobbyButton.gameObject);
            }
        }

        private bool HasSelectableKnownLobbySelection()
        {
            return IsKnownLobbySelectable(FindKnownLobby(selectedKnownLobbyMatchId));
        }

        private void UpdateJoinModePrimaryButtonInteractable()
        {
            if (createLobbyButton == null || sceneMode != UiSceneMode.LobbyJoin)
            {
                return;
            }

            createLobbyButton.interactable = !isConnected && !joinRequestInFlight && HasSelectableKnownLobbySelection();
        }

        private void HandleKnownLobbyNumberSelection()
        {
            if (sceneMode != UiSceneMode.LobbyJoin || isConnected || joinRequestInFlight)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            List<KnownLobbySnapshot> displayLobbies = BuildDisplayKnownLobbies();
            int displayCount = Mathf.Min(displayLobbies.Count, MaxKnownLobbies);
            for (int i = 0; i < displayCount; i++)
            {
                if (!WasKnownLobbyShortcutPressed(keyboard, i))
                {
                    continue;
                }

                KnownLobbySnapshot snapshot = displayLobbies[i];
                if (IsKnownLobbySelectable(snapshot))
                {
                    SelectKnownLobby(snapshot.matchId, focusJoinAction: true);
                }
                return;
            }
        }

        private static bool WasKnownLobbyShortcutPressed(Keyboard keyboard, int index)
        {
            return index switch
            {
                0 => WasShortcutPressed(keyboard, Key.Digit1, Key.Numpad1),
                1 => WasShortcutPressed(keyboard, Key.Digit2, Key.Numpad2),
                2 => WasShortcutPressed(keyboard, Key.Digit3, Key.Numpad3),
                3 => WasShortcutPressed(keyboard, Key.Digit4, Key.Numpad4),
                4 => WasShortcutPressed(keyboard, Key.Digit5, Key.Numpad5),
                5 => WasShortcutPressed(keyboard, Key.Digit6, Key.Numpad6),
                _ => false,
            };
        }

        private static bool WasShortcutPressed(Keyboard keyboard, Key digitKey, Key numpadKey)
        {
            if (keyboard == null)
            {
                return false;
            }

            return keyboard[digitKey].wasPressedThisFrame || keyboard[numpadKey].wasPressedThisFrame;
        }

        private void RefreshKnownLobbiesPreview()
        {
            if (sceneMode != UiSceneMode.LobbyJoin || networkManager == null)
            {
                return;
            }

            if (lobbyListRequestInFlight)
            {
                return;
            }

            lobbyListRequestInFlight = true;
            networkManager.ListMatches(
                response =>
                {
                    lobbyListRequestInFlight = false;
                    ReplaceKnownLobbiesWithServerList(response);
                },
                error =>
                {
                    lobbyListRequestInFlight = false;
                    if (sceneMode == UiSceneMode.LobbyJoin && !string.IsNullOrWhiteSpace(error))
                    {
                        SetLobbyInfo($"ロビー一覧取得失敗: {error}");
                    }
                }
            );
        }

        private void ReplaceKnownLobbiesWithServerList(MatchmakingClient.ListMatchesResponse response)
        {
            knownLobbies.Clear();

            MatchmakingClient.MatchSummary[] matches = response?.matches ?? Array.Empty<MatchmakingClient.MatchSummary>();
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int i = 0; i < matches.Length; i++)
            {
                MatchmakingClient.MatchSummary summary = matches[i];
                if (summary == null || string.IsNullOrWhiteSpace(summary.match_id))
                {
                    continue;
                }

                knownLobbies.Add(new KnownLobbySnapshot
                {
                    matchId = summary.match_id.Trim(),
                    playerCount = Mathf.Max(0, summary.player_count),
                    maxPlayers = summary.max_players,
                    startedAtUnix = summary.started_at_unix,
                    isReachable = true,
                    updatedAtUnix = nowUnix,
                });
            }

            RefreshKnownLobbiesListUi();
            QueueJoinPreview();
        }

        private void ConfigureButtonNavigation()
        {
            var active = new List<Selectable>(4);

            if (sceneMode == UiSceneMode.LobbyJoin)
            {
                AddActiveSelectable(active, createLobbyButton);
                AddActiveSelectable(active, joinLobbyButton);
            }
            else
            {
                AddActiveSelectable(active, createLobbyButton);
                AddActiveSelectable(active, joinLobbyButton);
                AddActiveSelectable(active, startGameButton);
                AddActiveSelectable(active, exitButton);
            }

            if (active.Count == 0)
            {
                return;
            }

            for (int i = 0; i < active.Count; i++)
            {
                Selectable current = active[i];
                Selectable up = active[(i - 1 + active.Count) % active.Count];
                Selectable down = active[(i + 1) % active.Count];

                Navigation nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = up;
                nav.selectOnDown = down;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                current.navigation = nav;
            }
        }

        private static void AddActiveSelectable(List<Selectable> destination, Selectable selectable)
        {
            if (destination == null || selectable == null)
            {
                return;
            }

            if (!selectable.gameObject.activeInHierarchy)
            {
                return;
            }

            destination.Add(selectable);
        }

        private void FocusDefaultButtonIfNeeded()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (eventSystem.currentSelectedGameObject != null)
            {
                return;
            }

            if (sceneMode == UiSceneMode.LobbyJoin)
            {
                if (IsSelectableUsable(createLobbyButton))
                {
                    eventSystem.SetSelectedGameObject(createLobbyButton.gameObject);
                    return;
                }

                if (IsSelectableUsable(joinLobbyButton))
                {
                    eventSystem.SetSelectedGameObject(joinLobbyButton.gameObject);
                    return;
                }
            }

            if (IsSelectableUsable(createLobbyButton))
            {
                eventSystem.SetSelectedGameObject(createLobbyButton.gameObject);
                return;
            }

            if (IsSelectableUsable(joinLobbyButton))
            {
                eventSystem.SetSelectedGameObject(joinLobbyButton.gameObject);
                return;
            }

            if (IsSelectableUsable(startGameButton))
            {
                eventSystem.SetSelectedGameObject(startGameButton.gameObject);
                return;
            }

            if (IsSelectableUsable(exitButton))
            {
                eventSystem.SetSelectedGameObject(exitButton.gameObject);
            }
        }

        private void EnsureUiInputModuleBindings()
        {
            CacheUiInputModule();
            if (uiInputModule == null)
            {
                return;
            }

            if (uiInputModule.point == null)
            {
                EnsureRuntimeUiPointerActions();
                uiInputModule.point = runtimeUiPointActionReference;
            }

            if (uiInputModule.leftClick == null)
            {
                EnsureRuntimeUiPointerActions();
                uiInputModule.leftClick = runtimeUiLeftClickActionReference;
            }
        }

        private bool ShouldUseKeyboardNavigationFallback()
        {
            CacheUiInputModule();
            return uiInputModule == null
                || !uiInputModule.isActiveAndEnabled
                || uiInputModule.move == null
                || uiInputModule.move.action == null;
        }

        private bool ShouldUseKeyboardSubmitFallback()
        {
            CacheUiInputModule();
            return uiInputModule == null
                || !uiInputModule.isActiveAndEnabled
                || uiInputModule.submit == null
                || uiInputModule.submit.action == null;
        }

        private static bool IsEnterSubmitPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }

        private void CacheUiInputModule()
        {
            if (uiInputModule != null)
            {
                return;
            }

            uiInputModule = GetComponent<InputSystemUIInputModule>();
            if (uiInputModule == null && EventSystem.current != null)
            {
                uiInputModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            }
        }

        private void EnsureRuntimeUiPointerActions()
        {
            if (runtimeUiPointerActionsAsset != null)
            {
                return;
            }

            runtimeUiPointerActionsAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            runtimeUiPointerActionsAsset.name = "RuntimeUIPointerActions";

            var pointerMap = new InputActionMap("UIRuntimeFallback");
            InputAction pointAction = pointerMap.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position");
            InputAction leftClickAction = pointerMap.AddAction("LeftClick", InputActionType.PassThrough, "<Mouse>/leftButton");
            runtimeUiPointerActionsAsset.AddActionMap(pointerMap);
            runtimeUiPointerActionsAsset.Enable();

            runtimeUiPointActionReference = InputActionReference.Create(pointAction);
            runtimeUiLeftClickActionReference = InputActionReference.Create(leftClickAction);
        }

        private void DisposeFallbackUiActions()
        {
            CacheUiInputModule();

            if (uiInputModule != null && uiInputModule.point == runtimeUiPointActionReference)
            {
                uiInputModule.point = null;
            }

            if (uiInputModule != null && uiInputModule.leftClick == runtimeUiLeftClickActionReference)
            {
                uiInputModule.leftClick = null;
            }

            if (runtimeUiPointerActionsAsset != null)
            {
                runtimeUiPointerActionsAsset.Disable();
            }

            if (runtimeUiPointActionReference != null)
            {
                DestroyRuntimeObject(runtimeUiPointActionReference);
                runtimeUiPointActionReference = null;
            }

            if (runtimeUiLeftClickActionReference != null)
            {
                DestroyRuntimeObject(runtimeUiLeftClickActionReference);
                runtimeUiLeftClickActionReference = null;
            }

            if (runtimeUiPointerActionsAsset != null)
            {
                DestroyRuntimeObject(runtimeUiPointerActionsAsset);
                runtimeUiPointerActionsAsset = null;
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(obj);
                return;
            }
#endif
            Destroy(obj);
        }

        private void EnsureSelectionStillValid()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
            {
                FocusDefaultButtonIfNeeded();
                return;
            }

            var selectable = selected.GetComponent<Selectable>();
            if (selectable == null || !selected.activeInHierarchy || !selectable.IsInteractable())
            {
                FocusDefaultButtonIfNeeded();
            }
        }

        private void HandleKeyboardNavigationFallback()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool moveUp = keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame;
            bool moveDown = keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame;
            if (!moveUp && !moveDown)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (eventSystem.currentSelectedGameObject != null && eventSystem.currentSelectedGameObject.GetComponent<InputField>() != null)
            {
                return;
            }

            List<Selectable> navigable = BuildNavigableButtons();
            if (navigable.Count == 0)
            {
                return;
            }

            int currentIndex = -1;
            GameObject selected = eventSystem.currentSelectedGameObject;
            for (int i = 0; i < navigable.Count; i++)
            {
                if (navigable[i] != null && navigable[i].gameObject == selected)
                {
                    currentIndex = i;
                    Debug.Log($"Current selected index: {currentIndex}");
                    for (int j = 0; j < 3; j++) {
                        selectObject[j].SetActive(j == i);
                    }
                    break;
                }
            }

            if (currentIndex < 0)
            {
                eventSystem.SetSelectedGameObject(navigable[0].gameObject);
                return;
            }

            int direction = moveUp ? -1 : 1;
            int nextIndex = (currentIndex + direction + navigable.Count) % navigable.Count;
            eventSystem.SetSelectedGameObject(navigable[nextIndex].gameObject);
            // Debug.Log(nextIndex);
            // for (int i = 0; i < 3; i++) {
            //     selectObject[i].SetActive(i == nextIndex);
            // }
        }

        private void HandleKeyboardSubmitFallback()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool submitPressed =
                keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame;

            if (!submitPressed)
            {
                return;
            }

            if (Time.unscaledTime - lastSubmitFallbackTime < 0.12f)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
            {
                FocusDefaultButtonIfNeeded();
                selected = eventSystem.currentSelectedGameObject;
            }

            if (selected == null || selected.GetComponent<InputField>() != null)
            {
                return;
            }

            Button selectedButton = selected.GetComponent<Button>();
            if (selectedButton == null || !selectedButton.IsInteractable())
            {
                return;
            }

            lastSubmitFallbackTime = Time.unscaledTime;
            selectedButton.onClick?.Invoke();
        }

        private List<Selectable> BuildNavigableButtons()
        {
            var list = new List<Selectable>(4);

            if (sceneMode == UiSceneMode.LobbyJoin)
            {
                AddSelectableIfNavigable(list, createLobbyButton);
                AddSelectableIfNavigable(list, joinLobbyButton);
            }
            else
            {
                AddSelectableIfNavigable(list, createLobbyButton);
                AddSelectableIfNavigable(list, joinLobbyButton);
                AddSelectableIfNavigable(list, startGameButton);
                AddSelectableIfNavigable(list, exitButton);
            }
            return list;
        }

        private static void AddSelectableIfNavigable(List<Selectable> destination, Selectable selectable)
        {
            if (destination == null || selectable == null)
            {
                return;
            }

            if (!IsSelectableUsable(selectable))
            {
                return;
            }

            destination.Add(selectable);
        }

        private static bool IsSelectableUsable(Selectable selectable)
        {
            return selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable();
        }

        private void BindNetworkEvents()
        {
            if (networkManager == null || eventsBound) return;

            networkManager.OnMatchCreated += OnMatchCreated;
            networkManager.OnMatchJoined += OnMatchJoined;
            networkManager.OnGameConnected += OnGameConnected;
            networkManager.OnGameDisconnected += OnGameDisconnected;
            networkManager.OnMatchStarted += OnMatchStarted;
            networkManager.OnNetworkError += OnNetworkError;
            eventsBound = true;
        }

        private void UnbindNetworkEvents()
        {
            if (networkManager == null || !eventsBound) return;

            networkManager.OnMatchCreated -= OnMatchCreated;
            networkManager.OnMatchJoined -= OnMatchJoined;
            networkManager.OnGameConnected -= OnGameConnected;
            networkManager.OnGameDisconnected -= OnGameDisconnected;
            networkManager.OnMatchStarted -= OnMatchStarted;
            networkManager.OnNetworkError -= OnNetworkError;
            eventsBound = false;
        }

        private void SyncStateFromNetworkManager()
        {
            if (networkManager == null)
            {
                currentMatchId = null;
                currentPlayerId = null;
                isConnected = false;
                return;
            }

            currentMatchId = networkManager.CurrentMatchId;
            currentPlayerId = networkManager.CurrentPlayerId;
            isConnected = networkManager.IsConnected;

            if (matchIdInput != null && !string.IsNullOrWhiteSpace(currentMatchId))
            {
                matchIdInput.text = currentMatchId;
            }
        }

        private void EnterSceneMode()
        {
            switch (sceneMode)
            {
                case UiSceneMode.TitleMenu:
                    EnterTitleMode();
                    break;
                case UiSceneMode.LobbyCreate:
                    EnterCreateMode();
                    break;
                case UiSceneMode.LobbyJoin:
                    EnterJoinMode();
                    break;
                default:
                    EnterTitleMode();
                    break;
            }
        }

        private void EnterTitleMode()
        {
            StopLobbyInfoPolling();
            SetStatus("ロビー作成かロビー参加を選択してください。");
            SetLobbyInfo(string.Empty);
            RefreshKnownLobbiesListUi();
        }

        private void EnterCreateMode()
        {
            RefreshKnownLobbiesListUi();

            if (!string.IsNullOrWhiteSpace(currentMatchId))
            {
                SetStatus($"ロビー待機中: {currentMatchId}");
                SetLobbyInfo($"ロビー {currentMatchId} / 人数を取得中...");
                StartCurrentLobbyInfoPolling(currentMatchId);
                return;
            }

            SetStatus("ロビー作成ボタンで新しいロビーを作ります。");
            SetLobbyInfo("作成後にロビーIDが表示されます。");
        }

        private void EnterJoinMode()
        {
            knownLobbies.Clear();
            selectedKnownLobbyMatchId = string.Empty;
            SetStatus("1-6キーでロビーを選んで「参加」してください。");
            SetLobbyInfo("ロビー一覧を取得中...");
            RefreshKnownLobbiesListUi();
            RefreshKnownLobbiesPreview();
            StartLobbyListPolling();
        }

        private void OnMatchIdInputChanged(string _)
        {
            if (sceneMode == UiSceneMode.LobbyJoin)
            {
                return;
            }

            RefreshUiState();
        }

        private void QueueJoinPreview()
        {
            if (previewLobbyInfoPolling != null)
            {
                StopCoroutine(previewLobbyInfoPolling);
                previewLobbyInfoPolling = null;
            }

            if (sceneMode != UiSceneMode.LobbyJoin || isConnected)
            {
                return;
            }

            string targetId = selectedKnownLobbyMatchId != null ? selectedKnownLobbyMatchId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(targetId))
            {
                SetLobbyInfo("1-6キーでロビーを選んで「参加」を押してください。");
                return;
            }

            previewLobbyInfoPolling = StartCoroutine(PreviewLobbyInfoAfterDelay(targetId));
        }

        private IEnumerator PreviewLobbyInfoAfterDelay(string targetMatchId)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, lobbyPreviewDelay));
            RequestMatchInfo(targetMatchId, isCurrentLobby: false);
            previewLobbyInfoPolling = null;
        }

        private void StartCurrentLobbyInfoPolling(string targetMatchId)
        {
            StopCurrentLobbyInfoPolling();

            if (string.IsNullOrWhiteSpace(targetMatchId))
            {
                return;
            }

            currentLobbyInfoPolling = StartCoroutine(PollCurrentLobbyInfo(targetMatchId.Trim()));
        }

        private void StopCurrentLobbyInfoPolling()
        {
            if (currentLobbyInfoPolling != null)
            {
                StopCoroutine(currentLobbyInfoPolling);
                currentLobbyInfoPolling = null;
            }
        }

        private void StopLobbyInfoPolling()
        {
            StopCurrentLobbyInfoPolling();
            StopLobbyListPolling();

            if (previewLobbyInfoPolling != null)
            {
                StopCoroutine(previewLobbyInfoPolling);
                previewLobbyInfoPolling = null;
            }
        }

        private void StartLobbyListPolling()
        {
            StopLobbyListPolling();

            if (sceneMode != UiSceneMode.LobbyJoin)
            {
                return;
            }

            lobbyListPolling = StartCoroutine(PollLobbyList());
        }

        private void StopLobbyListPolling()
        {
            lobbyListRequestInFlight = false;

            if (lobbyListPolling != null)
            {
                StopCoroutine(lobbyListPolling);
                lobbyListPolling = null;
            }
        }

        private IEnumerator PollLobbyList()
        {
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.3f, lobbyInfoRefreshInterval));
            while (enabled && gameObject.activeInHierarchy && sceneMode == UiSceneMode.LobbyJoin)
            {
                RefreshKnownLobbiesPreview();
                yield return wait;
            }

            lobbyListPolling = null;
        }

        private IEnumerator PollCurrentLobbyInfo(string targetMatchId)
        {
            float pollInterval = Mathf.Clamp(lobbyInfoRefreshInterval, 0.1f, ActiveLobbyStartPollInterval);
            WaitForSeconds wait = new WaitForSeconds(pollInterval);
            while (enabled && gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(targetMatchId))
            {
                RequestMatchInfo(targetMatchId, isCurrentLobby: true);
                yield return wait;
            }
        }

        private void RequestMatchInfo(string targetMatchId, bool isCurrentLobby)
        {
            if (networkManager == null || string.IsNullOrWhiteSpace(targetMatchId))
            {
                return;
            }

            networkManager.GetMatchInfo(
                targetMatchId,
                info =>
                {
                    int players = info != null && info.players != null ? info.players.Length : 0;
                    int maxPlayers = info != null ? info.max_players : 0;
                    RememberKnownLobby(targetMatchId, info);
                    if (info != null && info.started_at_unix > 0)
                    {
                        HandleMatchStarted(targetMatchId, info.started_at_unix);
                        return;
                    }

                    if (isCurrentLobby || string.Equals(selectedKnownLobbyMatchId, targetMatchId, StringComparison.Ordinal))
                    {
                        SetLobbyInfo($"選択中: {targetMatchId} / 人数 {players} / {maxPlayers}");
                    }
                },
                _ =>
                {
                    if (!isCurrentLobby)
                    {
                        SetLobbyInfo("選択したロビー情報を取得できません。");
                    }
                    else
                    {
                        SetLobbyInfo($"ロビー {targetMatchId} / 人数取得失敗");
                    }
                }
            );
        }

        private void OnPrimaryButtonClicked()
        {
            switch (sceneMode)
            {
                case UiSceneMode.TitleMenu:
                    LoadScene(createLobbySceneName);
                    break;
                case UiSceneMode.LobbyCreate:
                    OnCreateLobbyClicked();
                    break;
                case UiSceneMode.LobbyJoin:
                    OnJoinLobbyClicked();
                    break;
            }
        }

        private void OnSecondaryButtonClicked()
        {
            switch (sceneMode)
            {
                case UiSceneMode.TitleMenu:
                    LoadScene(joinLobbySceneName);
                    break;
                case UiSceneMode.LobbyCreate:
                case UiSceneMode.LobbyJoin:
                    ReturnToTitle();
                    break;
            }
        }

        private void OnCreateLobbyClicked()
        {
            if (networkManager == null)
            {
                SetStatus("NetworkManager が見つかりません。");
                return;
            }

            if (createRequestInFlight)
            {
                return;
            }

            createRequestInFlight = true;
            SetStatus("ロビーを作成中...");
            RefreshUiState();

            networkManager.CreateAndJoinMatch(
                onSuccess: () =>
                {
                    createRequestInFlight = false;
                    SetStatus("ロビー作成完了。サーバー接続中...");
                    RefreshUiState();
                },
                onError: error =>
                {
                    createRequestInFlight = false;
                    SetStatus($"ロビー作成失敗: {error}");
                    RefreshUiState();
                }
            );
        }

        private void OnJoinLobbyClicked()
        {
            if (networkManager == null)
            {
                SetStatus("NetworkManager が見つかりません。");
                return;
            }

            if (joinRequestInFlight)
            {
                return;
            }

            string matchId = sceneMode == UiSceneMode.LobbyJoin
                ? (selectedKnownLobbyMatchId != null ? selectedKnownLobbyMatchId.Trim() : string.Empty)
                : (matchIdInput != null ? matchIdInput.text.Trim() : string.Empty);
            if (string.IsNullOrEmpty(matchId))
            {
                SetStatus(sceneMode == UiSceneMode.LobbyJoin ? "1-6キーでロビーを選択してください。" : "ロビーIDを入力してください。");
                return;
            }

            joinRequestInFlight = true;
            SetStatus($"参加中... ({matchId})");
            RefreshUiState();

            networkManager.JoinMatch(
                matchId,
                onSuccess: () =>
                {
                    joinRequestInFlight = false;
                    SetStatus("参加成功。サーバー接続中...");
                    RefreshUiState();
                },
                onError: error =>
                {
                    joinRequestInFlight = false;
                    SetStatus($"参加失敗: {error}");
                    RefreshUiState();
                }
            );
        }

        private void OnStartGameClicked()
        {
            if (sceneMode != UiSceneMode.LobbyCreate)
            {
                return;
            }

            if (networkManager == null || string.IsNullOrWhiteSpace(currentMatchId))
            {
                SetStatus("開始できるロビー情報がありません。");
                return;
            }

            if (matchStartRequestInFlight)
            {
                return;
            }

            bool mustBeConnected = requireConnectedToStart || sceneMode != UiSceneMode.TitleMenu;
            if (mustBeConnected && !isConnected)
            {
                SetStatus("先にロビーへ接続してください。");
                return;
            }

            matchStartRequestInFlight = true;
            SetStatus("ゲーム開始を通知しています...");
            RefreshUiState();
            networkManager.StartMatch(
                currentMatchId,
                onSuccess: _ =>
                {
                    matchStartRequestInFlight = false;
                    SetStatus("ゲーム開始通知を送信しました。");
                    RefreshUiState();
                },
                onError: error =>
                {
                    matchStartRequestInFlight = false;
                    SetStatus($"ゲーム開始失敗: {error}");
                    RefreshUiState();
                }
            );
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ReturnToTitle()
        {
            StopLobbyInfoPolling();

            if (sceneMode == UiSceneMode.LobbyCreate && networkManager != null && !string.IsNullOrWhiteSpace(currentMatchId))
            {
                string matchIdToDelete = currentMatchId.Trim();
                SetStatus("ロビーを閉じています...");
                networkManager.DeleteMatch(
                    matchIdToDelete,
                    CompleteReturnToTitle,
                    error =>
                    {
                        Debug.LogWarning($"Failed to delete match '{matchIdToDelete}': {error}");
                        CompleteReturnToTitle();
                    }
                );
                return;
            }

            CompleteReturnToTitle();
        }

        private void CompleteReturnToTitle()
        {
            StopLobbyInfoPolling();

            if (networkManager != null)
            {
                networkManager.Disconnect();
            }

            currentMatchId = null;
            currentPlayerId = null;
            selectedKnownLobbyMatchId = string.Empty;
            currentMatchStartedAtUnix = 0;
            matchStartRequestInFlight = false;
            isConnected = false;
            LoadScene(titleSceneName);
        }

        private void OnMatchCreated(string matchId)
        {
            currentMatchId = matchId;
            currentMatchStartedAtUnix = 0;
            matchStartRequestInFlight = false;
            RememberKnownLobby(matchId);
            if (matchIdInput != null)
            {
                matchIdInput.text = matchId;
            }

            SetStatus($"ロビー作成: {matchId}");
            SetLobbyInfo($"ロビー {matchId} を作成しました。人数を取得中...");
            RefreshUiState();
        }

        private void OnMatchJoined(string matchId, string playerId)
        {
            currentMatchId = matchId;
            currentPlayerId = playerId;
            currentMatchStartedAtUnix = 0;
            matchStartRequestInFlight = false;
            RememberKnownLobby(matchId);
            selectedKnownLobbyMatchId = matchId;
            if (matchIdInput != null && sceneMode == UiSceneMode.LobbyCreate)
            {
                matchIdInput.text = matchId;
            }

            SetStatus($"ロビー参加: {matchId} / Player: {playerId}");
            StartCurrentLobbyInfoPolling(matchId);
            RefreshUiState();
        }

        private void OnGameConnected()
        {
            isConnected = true;
            SetStatus($"接続完了: {currentMatchId}");
            StartCurrentLobbyInfoPolling(currentMatchId);
            RefreshUiState();
        }

        private void OnGameDisconnected()
        {
            isConnected = false;
            StopCurrentLobbyInfoPolling();
            matchStartRequestInFlight = false;

            if (sceneMode == UiSceneMode.LobbyJoin)
            {
                RefreshKnownLobbiesListUi();
                RefreshKnownLobbiesPreview();
                QueueJoinPreview();
            }

            SetStatus("ゲームサーバーから切断されました。");
            RefreshUiState();
        }

        private void OnMatchStarted(string matchId, long startedAtUnix)
        {
            HandleMatchStarted(matchId, startedAtUnix);
        }

        private void HandleMatchStarted(string matchId, long startedAtUnix)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(currentMatchId) && !string.Equals(currentMatchId, matchId, StringComparison.Ordinal))
            {
                return;
            }

            currentMatchId = matchId;
            currentMatchStartedAtUnix = startedAtUnix;
            matchStartRequestInFlight = false;

            if (sceneMode == UiSceneMode.TitleMenu || sceneTransitionInProgress)
            {
                return;
            }

            if (string.Equals(SceneManager.GetActiveScene().name, gameSceneName, StringComparison.Ordinal))
            {
                return;
            }

            SetStatus("対戦を開始します...");
            LoadSceneImmediate(gameSceneName);
        }

        private void OnNetworkError(string error)
        {
            createRequestInFlight = false;
            joinRequestInFlight = false;
            matchStartRequestInFlight = false;
            SetStatus($"ネットワークエラー: {error}");
            RefreshUiState();
        }

        private void RefreshUiState()
        {
            bool connected = networkManager != null && networkManager.IsConnected;
            isConnected = connected;

            if (createLobbyButton != null)
            {
                bool interactable = true;
                if (sceneMode == UiSceneMode.LobbyCreate)
                {
                    interactable = !connected && !createRequestInFlight;
                }
                else if (sceneMode == UiSceneMode.LobbyJoin)
                {
                    string matchId = !string.IsNullOrWhiteSpace(selectedKnownLobbyMatchId)
                        ? selectedKnownLobbyMatchId
                        : (matchIdInput != null ? matchIdInput.text : string.Empty);
                    interactable = !connected && !joinRequestInFlight && !string.IsNullOrWhiteSpace(matchId);
                }

                createLobbyButton.interactable = interactable;
            }

            if (joinLobbyButton != null)
            {
                bool interactable = !createRequestInFlight && !joinRequestInFlight && !matchStartRequestInFlight;
                joinLobbyButton.interactable = interactable;
            }

            if (startGameButton != null)
            {
                bool canStartInLobbyCreate = sceneMode == UiSceneMode.LobbyCreate && !matchStartRequestInFlight;
                bool mustBeConnected = requireConnectedToStart || sceneMode == UiSceneMode.LobbyCreate;
                startGameButton.interactable = canStartInLobbyCreate && (!mustBeConnected || connected);
            }

            if (matchIdInput != null)
            {
                bool inJoinMode = sceneMode == UiSceneMode.LobbyJoin;
                bool inCreateMode = sceneMode == UiSceneMode.LobbyCreate;
                matchIdInput.interactable = false;
                matchIdInput.readOnly = !inCreateMode || connected;

                if (inCreateMode && !string.IsNullOrWhiteSpace(currentMatchId))
                {
                    matchIdInput.text = currentMatchId;
                }
            }

            RefreshKnownLobbiesListUi();
            ConfigureButtonNavigation();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.enabled = !string.IsNullOrEmpty(message);
            }
        }

        private void SetLobbyInfo(string message)
        {
            if (lobbyInfoText != null)
            {
                lobbyInfoText.text = message;
                lobbyInfoText.enabled = !string.IsNullOrEmpty(message);
            }
        }

        private void LoadScene(string targetScene)
        {
            if (string.IsNullOrWhiteSpace(targetScene))
            {
                Debug.LogWarning("LoadScene target is empty.");
                return;
            }

            if (string.Equals(SceneManager.GetActiveScene().name, targetScene, StringComparison.Ordinal))
            {
                return;
            }

            if (sceneTransitionInProgress)
            {
                return;
            }

            sceneTransitionInProgress = true;

            if (sceneLoadFallbackCoroutine != null)
            {
                StopCoroutine(sceneLoadFallbackCoroutine);
                sceneLoadFallbackCoroutine = null;
            }

            if (networkManager != null)
            {
                DontDestroyOnLoad(networkManager.gameObject);
            }

            try
            {
                Initiate.Fade(targetScene, fadeColor, fadeDuration);
                sceneLoadFallbackCoroutine = StartCoroutine(EnsureSceneLoadCompletes(targetScene));
            }
            catch (Exception e)
            {
                sceneTransitionInProgress = false;
                Debug.LogWarning($"Fade load failed, fallback to SceneManager.LoadScene: {e.Message}");
                SceneManager.LoadScene(targetScene);
            }
        }

        private void LoadSceneImmediate(string targetScene)
        {
            if (string.IsNullOrWhiteSpace(targetScene))
            {
                Debug.LogWarning("LoadSceneImmediate target is empty.");
                return;
            }

            if (string.Equals(SceneManager.GetActiveScene().name, targetScene, StringComparison.Ordinal))
            {
                return;
            }

            if (sceneLoadFallbackCoroutine != null)
            {
                StopCoroutine(sceneLoadFallbackCoroutine);
                sceneLoadFallbackCoroutine = null;
            }

            sceneTransitionInProgress = false;

            if (networkManager != null)
            {
                DontDestroyOnLoad(networkManager.gameObject);
            }

            SceneManager.LoadScene(targetScene);
        }

        private IEnumerator EnsureSceneLoadCompletes(string targetScene)
        {
            float timeout = Mathf.Max(0.75f, fadeDuration + 0.75f);
            yield return new WaitForSecondsRealtime(timeout);

            sceneLoadFallbackCoroutine = null;

            if (!sceneTransitionInProgress)
            {
                yield break;
            }

            if (string.Equals(SceneManager.GetActiveScene().name, targetScene, StringComparison.Ordinal))
            {
                sceneTransitionInProgress = false;
                yield break;
            }

            sceneTransitionInProgress = false;
            Debug.LogWarning($"Scene transition to '{targetScene}' timed out. Falling back to direct load.");
            SceneManager.LoadScene(targetScene);
        }
    }
}
