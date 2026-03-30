using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kotatsu.Network
{
    public class OpponentPositionOverlay : MonoBehaviour
    {
        private const string OverlayCanvasName = "OpponentHudCanvas";
        private const string TrackerPanelName = "OpponentStageTracker";

        private class PlayerTrackerRow
        {
            public string playerId;
            public Image background;
            public TextMeshProUGUI headerText;
            public TextMeshProUGUI stageText;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<OpponentPositionOverlay>() != null) return;
            if (FindAnyObjectByType<PlayerController>() == null) return;

            GameObject go = new GameObject("OpponentPositionOverlay");
            go.AddComponent<OpponentPositionOverlay>();
        }

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private Color panelBackground = new Color(0.08f, 0.10f, 0.15f, 0.76f);
        [SerializeField] private Color textColor = new Color(0.96f, 0.97f, 1f, 1f);
        [SerializeField] private Color subTextColor = new Color(0.80f, 0.86f, 0.98f, 1f);

        private readonly Dictionary<string, PlayerTrackerRow> rowsByPlayerId = new Dictionary<string, PlayerTrackerRow>();
        private RectTransform panelRoot;
        private TextMeshProUGUI titleText;
        private bool subscribed;
        private float reconnectTimer;

        private void Awake()
        {
            if (FindAnyObjectByType<PlayerController>() == null)
            {
                Destroy(gameObject);
                return;
            }

            EnsurePanel();
            TryBindNetworkManager();
            RefreshTracker();
        }

        private void Update()
        {
            if (networkManager == null || !subscribed)
            {
                reconnectTimer += Time.deltaTime;
                if (reconnectTimer >= 1f)
                {
                    reconnectTimer = 0f;
                    TryBindNetworkManager();
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void EnsurePanel()
        {
            Canvas canvas = GetOrCreateOverlayCanvas();
            Transform existing = canvas.transform.Find(TrackerPanelName);
            if (existing != null)
            {
                panelRoot = existing as RectTransform;
                titleText = panelRoot.GetComponentInChildren<TextMeshProUGUI>();
                return;
            }

            GameObject panelGo = new GameObject(TrackerPanelName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(canvas.transform, false);
            panelRoot = panelGo.GetComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0f, 1f);
            panelRoot.anchorMax = new Vector2(0f, 1f);
            panelRoot.pivot = new Vector2(0f, 1f);
            panelRoot.anchoredPosition = new Vector2(24f, -24f);
            panelRoot.sizeDelta = new Vector2(360f, 0f);

            Image bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.22f);

            VerticalLayoutGroup layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            titleText = CreateText("Header", panelRoot, 24f, FontStyles.Bold, textColor);
            titleText.text = "Opponent Stages";
            titleText.alignment = TextAlignmentOptions.Left;
        }

        private static Canvas GetOrCreateOverlayCanvas()
        {
            GameObject canvasGo = GameObject.Find(OverlayCanvasName);
            if (canvasGo == null)
            {
                canvasGo = new GameObject(OverlayCanvasName);
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();

            Camera worldCam = Camera.main;
            if (worldCam == null)
            {
                worldCam = FindAnyObjectByType<Camera>();
            }

            canvas.renderMode = worldCam != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = worldCam;
            canvas.planeDistance = 100f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        private void TryBindNetworkManager()
        {
            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>();
            }

            if (networkManager == null || subscribed)
            {
                return;
            }

            networkManager.OnGameConnected += RefreshTracker;
            networkManager.OnGameDisconnected += RefreshTracker;
            networkManager.OnMatchJoined += OnMatchJoined;
            networkManager.OnMatchConfigurationUpdated += RefreshTracker;
            networkManager.OnPlayerStageProgressUpdated += OnPlayerStageProgressUpdated;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || networkManager == null)
            {
                return;
            }

            networkManager.OnGameConnected -= RefreshTracker;
            networkManager.OnGameDisconnected -= RefreshTracker;
            networkManager.OnMatchJoined -= OnMatchJoined;
            networkManager.OnMatchConfigurationUpdated -= RefreshTracker;
            networkManager.OnPlayerStageProgressUpdated -= OnPlayerStageProgressUpdated;
            subscribed = false;
        }

        private void OnMatchJoined(string matchId, string playerId)
        {
            RefreshTracker();
        }

        private void OnPlayerStageProgressUpdated(string playerId, int currentStageIndex)
        {
            UpdateSingleRow(playerId);
        }

        private void RefreshTracker()
        {
            if (panelRoot == null || titleText == null)
            {
                return;
            }

            bool shouldShow = networkManager != null && networkManager.IsConnected && networkManager.HasMatchConfiguration;
            panelRoot.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                ClearRows();
                return;
            }

            titleText.text = "Opponent Stages";

            MatchPlayerState[] players = networkManager.CurrentMatchPlayers;
            HashSet<string> keepIds = new HashSet<string>();

            for (int i = 0; i < players.Length; i++)
            {
                MatchPlayerState playerState = players[i];
                if (playerState == null || string.IsNullOrWhiteSpace(playerState.player_id))
                {
                    continue;
                }

                if (string.Equals(playerState.player_id, networkManager.CurrentPlayerId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                keepIds.Add(playerState.player_id);
                EnsureRow(playerState);
                UpdateSingleRow(playerState.player_id);
            }

            List<string> staleIds = null;
            foreach (KeyValuePair<string, PlayerTrackerRow> kv in rowsByPlayerId)
            {
                if (keepIds.Contains(kv.Key))
                {
                    continue;
                }

                if (staleIds == null) staleIds = new List<string>();
                staleIds.Add(kv.Key);
            }

            if (staleIds == null)
            {
                return;
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                RemoveRow(staleIds[i]);
            }
        }

        private void EnsureRow(MatchPlayerState playerState)
        {
            if (rowsByPlayerId.ContainsKey(playerState.player_id))
            {
                return;
            }

            GameObject rowGo = new GameObject($"Tracker_{playerState.player_id}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rowGo.transform.SetParent(panelRoot, false);

            Image background = rowGo.GetComponent<Image>();
            background.color = panelBackground;

            VerticalLayoutGroup layout = rowGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = rowGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 0f);

            TextMeshProUGUI header = CreateText("Header", rowRect, 20f, FontStyles.Bold, textColor);
            TextMeshProUGUI stage = CreateText("Stage", rowRect, 16f, FontStyles.Normal, subTextColor);

            PlayerTrackerRow row = new PlayerTrackerRow
            {
                playerId = playerState.player_id,
                background = background,
                headerText = header,
                stageText = stage
            };

            rowsByPlayerId[playerState.player_id] = row;
        }

        private void UpdateSingleRow(string playerId)
        {
            if (networkManager == null ||
                !networkManager.TryGetPlayerMatchState(playerId, out MatchPlayerState playerState) ||
                !rowsByPlayerId.TryGetValue(playerId, out PlayerTrackerRow row))
            {
                return;
            }

            row.background.color = panelBackground;
            row.headerText.text = $"{playerState.display_name} ({playerId})";
            int currentStageIndex = networkManager.GetPlayerCurrentStageIndex(playerId, playerState.current_stage_index);
            row.stageText.text = $"Stage Index: {currentStageIndex}";
        }

        private void ClearRows()
        {
            List<string> ids = new List<string>(rowsByPlayerId.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                RemoveRow(ids[i]);
            }
        }

        private void RemoveRow(string playerId)
        {
            if (!rowsByPlayerId.TryGetValue(playerId, out PlayerTrackerRow row))
            {
                return;
            }

            rowsByPlayerId.Remove(playerId);
            if (row.background != null)
            {
                Destroy(row.background.gameObject);
            }
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, float fontSize, FontStyles style, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = ResolveFontAsset();
            if (text.font != null)
            {
                text.fontSharedMaterial = text.font.material;
            }
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableWordWrapping = false;
            return text;
        }

        private static TMP_FontAsset ResolveFontAsset()
        {
            if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
    }
}
