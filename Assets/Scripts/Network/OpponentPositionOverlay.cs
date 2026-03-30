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
        private static readonly int[] CharacterToLocationColorMap = { 1, 2, 0, 3 };

        private class PlayerTrackerRow
        {
            public string playerId;
            public RectTransform root;
            public Image background;
            public RectTransform trackRect;
            public TextMeshProUGUI headerText;
            public TextMeshProUGUI stageText;
            public readonly List<RouteStopVisual> routeStops = new List<RouteStopVisual>();
        }

        private class RouteStopVisual
        {
            public RectTransform root;
            public Image slotImage;
            public Outline slotOutline;
            public Image iconImage;
        }

        private struct StageLocationPresentation
        {
            public bool hasSprite;
            public LocationVisualKind visualKind;
            public string label;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<OpponentPositionOverlay>() != null) return;

            GameObject go = new GameObject("OpponentPositionOverlay");
            go.AddComponent<OpponentPositionOverlay>();
        }

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private PlayerController localPlayerController;
        [SerializeField] private LocationSpriteDatabase locationSpriteDatabase;
        [SerializeField] private Color panelBackground = new Color(0.08f, 0.10f, 0.15f, 0.76f);
        [SerializeField] private Color selfPanelBackground = new Color(0.16f, 0.20f, 0.28f, 0.92f);
        [SerializeField] private Color trackColor = new Color(0.99f, 0.73f, 0.45f, 0.96f);
        [SerializeField] private Color trackOutlineColor = new Color(0.95f, 0.46f, 0.44f, 0.95f);
        [SerializeField] private Color checkpointColor = new Color(1f, 0.94f, 0.84f, 0.90f);
        [SerializeField] private Color currentSlotColor = new Color(1f, 0.98f, 0.86f, 1f);
        [SerializeField] private Color currentSlotOutlineColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color textColor = new Color(0.96f, 0.97f, 1f, 1f);
        [SerializeField] private Color subTextColor = new Color(0.80f, 0.86f, 0.98f, 1f);

        private readonly Dictionary<string, PlayerTrackerRow> rowsByPlayerId = new Dictionary<string, PlayerTrackerRow>();
        private RectTransform panelRoot;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI statusText;
        private bool subscribed;
        private float reconnectTimer;
        private float refreshTimer;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (locationSpriteDatabase == null)
            {
                locationSpriteDatabase = Resources.Load<LocationSpriteDatabase>("LocationSpriteDatabase");
            }

            if (localPlayerController == null)
            {
                localPlayerController = FindFirstObjectByType<PlayerController>();
            }

            EnsurePanel();
            TryBindNetworkManager();
            RefreshTracker();
        }

        private void Update()
        {
            if (panelRoot == null || titleText == null || statusText == null)
            {
                EnsurePanel();
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

            if (localPlayerController == null)
            {
                localPlayerController = FindFirstObjectByType<PlayerController>();
            }

            refreshTimer += Time.deltaTime;
            if (refreshTimer >= 0.25f)
            {
                refreshTimer = 0f;
                RefreshTracker();
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
                titleText = panelRoot.Find("Header")?.GetComponent<TextMeshProUGUI>();
                statusText = panelRoot.Find("Status")?.GetComponent<TextMeshProUGUI>();
                if (titleText == null)
                {
                    titleText = CreateText("Header", panelRoot, 24f, FontStyles.Bold, textColor);
                    titleText.alignment = TextAlignmentOptions.Left;
                }
                titleText.text = string.Empty;
                titleText.gameObject.SetActive(false);

                if (statusText == null)
                {
                    statusText = CreateText("Status", panelRoot, 16f, FontStyles.Normal, subTextColor);
                    statusText.alignment = TextAlignmentOptions.Left;
                }
                statusText.text = "Loading map...";
                return;
            }

            GameObject panelGo = new GameObject(TrackerPanelName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(canvas.transform, false);
            panelRoot = panelGo.GetComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0f, 1f);
            panelRoot.anchorMax = new Vector2(0f, 1f);
            panelRoot.pivot = new Vector2(0f, 1f);
            panelRoot.anchoredPosition = new Vector2(18f, -18f);
            panelRoot.sizeDelta = new Vector2(312f, 0f);

            Image bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.22f);

            VerticalLayoutGroup layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            titleText = CreateText("Header", panelRoot, 24f, FontStyles.Bold, textColor);
            titleText.text = string.Empty;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.gameObject.SetActive(false);

            statusText = CreateText("Status", panelRoot, 13f, FontStyles.Normal, subTextColor);
            statusText.text = "Loading map...";
            statusText.alignment = TextAlignmentOptions.Left;
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
            if (panelRoot == null || titleText == null || statusText == null)
            {
                return;
            }

            bool hasLocalPlayer = FindAnyObjectByType<PlayerController>() != null;
            panelRoot.gameObject.SetActive(hasLocalPlayer);
            if (!hasLocalPlayer)
            {
                ClearRows();
                return;
            }

            titleText.gameObject.SetActive(false);
            statusText.gameObject.SetActive(true);

            if (networkManager == null)
            {
                statusText.text = "Loading map...";
                ClearRows();
                return;
            }

            if (!networkManager.IsConnected)
            {
                statusText.text = "Loading map...";
                ClearRows();
                return;
            }

            if (!networkManager.HasMatchConfiguration)
            {
                statusText.text = "Loading map...";
                ClearRows();
                return;
            }

            statusText.gameObject.SetActive(false);

            MatchPlayerState[] players = networkManager.CurrentMatchPlayers;
            HashSet<string> keepIds = new HashSet<string>();

            for (int i = 0; i < players.Length; i++)
            {
                MatchPlayerState playerState = players[i];
                if (playerState == null || string.IsNullOrWhiteSpace(playerState.player_id))
                {
                    continue;
                }

                keepIds.Add(playerState.player_id);
                EnsureRow(playerState);
                UpdateSingleRow(playerState.player_id);

                if (rowsByPlayerId.TryGetValue(playerState.player_id, out PlayerTrackerRow row) && row.root != null)
                {
                    int siblingIndex = string.Equals(playerState.player_id, networkManager.CurrentPlayerId, System.StringComparison.Ordinal)
                        ? 1
                        : keepIds.Count;
                    row.root.SetSiblingIndex(siblingIndex);
                }
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
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 0f);

            Image background = rowGo.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);

            VerticalLayoutGroup layout = rowGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = rowGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            GameObject trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
            trackGo.transform.SetParent(rowRect, false);
            RectTransform trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.sizeDelta = new Vector2(280f, 40f);

            Image trackImage = trackGo.GetComponent<Image>();
            trackImage.color = trackColor;
            trackImage.raycastTarget = false;

            Outline outline = trackGo.GetComponent<Outline>();
            outline.effectColor = trackOutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            LayoutElement trackLayout = trackGo.GetComponent<LayoutElement>();
            trackLayout.preferredWidth = 280f;
            trackLayout.preferredHeight = 40f;
            trackLayout.minHeight = 40f;

            PlayerTrackerRow row = new PlayerTrackerRow
            {
                playerId = playerState.player_id,
                root = rowRect,
                background = background,
                trackRect = trackRect
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

            int currentStageIndex = networkManager.GetPlayerCurrentStageIndex(playerId, playerState.current_stage_index);
            bool isSelf = string.Equals(playerState.player_id, networkManager.CurrentPlayerId, System.StringComparison.Ordinal);
            row.background.color = isSelf ? new Color(selfPanelBackground.r, selfPanelBackground.g, selfPanelBackground.b, 0.12f) : new Color(panelBackground.r, panelBackground.g, panelBackground.b, 0.08f);

            int displayColorIndex = ResolveDisplayColorIndex(playerState);
            UpdateRouteIcons(row, playerState, currentStageIndex, displayColorIndex);
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
            if (row.root != null)
            {
                Destroy(row.root.gameObject);
            }
        }

        private static StageLocationPresentation ResolveStageLocation(MatchPlayerState playerState, int currentStageIndex)
        {
            if (currentStageIndex <= 0)
            {
                return new StageLocationPresentation
                {
                    hasSprite = true,
                    visualKind = LocationVisualKind.Home,
                    label = "Home"
                };
            }

            if (playerState != null &&
                playerState.stage_order != null &&
                currentStageIndex - 1 >= 0 &&
                currentStageIndex - 1 < playerState.stage_order.Length)
            {
                return ResolveStageOrderValue(playerState.stage_order[currentStageIndex - 1]);
            }

            if (playerState != null && playerState.stage_order != null && currentStageIndex > playerState.stage_order.Length)
            {
                return new StageLocationPresentation
                {
                    hasSprite = false,
                    visualKind = LocationVisualKind.Mountain,
                    label = "Goal"
                };
            }

            return new StageLocationPresentation
            {
                hasSprite = false,
                visualKind = LocationVisualKind.Home,
                label = $"Stage {currentStageIndex}"
            };
        }

        private void UpdateRouteIcons(PlayerTrackerRow row, MatchPlayerState playerState, int currentStageIndex, int displayColorIndex)
        {
            if (row.trackRect == null || locationSpriteDatabase == null)
            {
                return;
            }

            List<StageLocationPresentation> routeStages = BuildRouteStages(playerState);
            int routeStopCount = Mathf.Max(routeStages.Count, 1);

            while (row.routeStops.Count < routeStopCount)
            {
                GameObject stopGo = new GameObject($"RouteStop_{row.routeStops.Count}", typeof(RectTransform), typeof(Image), typeof(Outline));
                stopGo.transform.SetParent(row.trackRect, false);

                RectTransform stopRect = stopGo.GetComponent<RectTransform>();
                stopRect.anchorMin = new Vector2(0.5f, 0.5f);
                stopRect.anchorMax = new Vector2(0.5f, 0.5f);
                stopRect.pivot = new Vector2(0.5f, 0.5f);
                stopRect.sizeDelta = new Vector2(14f, 14f);

                Image slotImage = stopGo.GetComponent<Image>();
                slotImage.color = checkpointColor;
                slotImage.raycastTarget = false;

                Outline slotOutline = stopGo.GetComponent<Outline>();
                slotOutline.effectColor = new Color(1f, 1f, 1f, 0f);
                slotOutline.effectDistance = new Vector2(2f, -2f);
                slotOutline.useGraphicAlpha = true;

                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(stopRect, false);
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(20f, 20f);

                Image iconImage = iconGo.GetComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;

                row.routeStops.Add(new RouteStopVisual
                {
                    root = stopRect,
                    slotImage = slotImage,
                    slotOutline = slotOutline,
                    iconImage = iconImage
                });
            }

            int currentStop = Mathf.Clamp(currentStageIndex, 0, routeStopCount - 1);

            for (int i = 0; i < row.routeStops.Count; i++)
            {
                bool active = i < routeStages.Count;
                RouteStopVisual routeStop = row.routeStops[i];
                routeStop.root.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float x = CalculateTrackX(row.trackRect, routeStopCount, i);
                routeStop.root.anchoredPosition = new Vector2(x, 0f);

                StageLocationPresentation routeStage = routeStages[i];
                Sprite sprite = routeStage.hasSprite
                    ? locationSpriteDatabase.GetSprite(displayColorIndex, routeStage.visualKind)
                    : null;
                bool isCurrent = i == currentStop;
                bool isReached = i <= currentStop;
                Color themeColor = GetTrackerThemeColor(displayColorIndex);
                Color reachedSlotColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.35f);
                Color currentSlotThemeColor = Color.Lerp(themeColor, Color.white, 0.18f);

                routeStop.slotImage.color = isCurrent
                    ? currentSlotThemeColor
                    : isReached
                        ? new Color(reachedSlotColor.r, reachedSlotColor.g, reachedSlotColor.b, 0.95f)
                        : new Color(reachedSlotColor.r, reachedSlotColor.g, reachedSlotColor.b, 0.45f);
                routeStop.slotOutline.effectColor = isCurrent ? themeColor : new Color(1f, 1f, 1f, 0f);
                routeStop.root.sizeDelta = isCurrent ? new Vector2(22f, 22f) : new Vector2(16f, 16f);

                routeStop.iconImage.sprite = sprite;
                routeStop.iconImage.enabled = sprite != null;
                routeStop.iconImage.color = isCurrent
                    ? Color.white
                    : isReached
                        ? new Color(1f, 1f, 1f, 0.94f)
                        : new Color(1f, 1f, 1f, 0.50f);
                routeStop.iconImage.rectTransform.sizeDelta = isCurrent ? new Vector2(28f, 28f) : new Vector2(20f, 20f);

                if (isCurrent)
                {
                    routeStop.root.SetAsLastSibling();
                }
            }
        }

        private int ResolveDisplayColorIndex(MatchPlayerState playerState)
        {
            bool isSelf = networkManager != null &&
                          playerState != null &&
                          string.Equals(playerState.player_id, networkManager.CurrentPlayerId, System.StringComparison.Ordinal);

            int characterIndex;
            if (isSelf && localPlayerController != null)
            {
                characterIndex = Mathf.Clamp(localPlayerController.selectedCharacterIndex, 0, 3);
            }
            else
            {
                characterIndex = Mathf.Clamp(playerState != null ? playerState.color_index : 0, 0, 3);
            }

            if (characterIndex < CharacterToLocationColorMap.Length)
            {
                return CharacterToLocationColorMap[characterIndex];
            }

            return characterIndex;
        }

        private static Color GetTrackerThemeColor(int colorIndex)
        {
            return Mathf.Clamp(colorIndex, 0, 3) switch
            {
                1 => new Color(0.73f, 0.97f, 0.47f, 1f),
                2 => new Color(0.45f, 0.87f, 0.97f, 1f),
                3 => new Color(0.72f, 0.52f, 0.96f, 1f),
                _ => new Color(0.98f, 0.51f, 0.51f, 1f)
            };
        }

        private static List<StageLocationPresentation> BuildRouteStages(MatchPlayerState playerState)
        {
            List<StageLocationPresentation> stages = new List<StageLocationPresentation>();
            if (playerState == null)
            {
                return stages;
            }

            stages.Add(new StageLocationPresentation
            {
                hasSprite = true,
                visualKind = LocationVisualKind.Home,
                label = "Home"
            });

            if (playerState.stage_order != null)
            {
                for (int i = 0; i < playerState.stage_order.Length; i++)
                {
                    stages.Add(ResolveStageOrderValue(playerState.stage_order[i]));
                }
            }

            return stages;
        }

        private static float CalculateTrackX(RectTransform trackRect, int routeStopCount, int stopIndex)
        {
            float trackWidth = Mathf.Max(trackRect.rect.width, 0f);
            float horizontalPadding = 16f;
            float usableWidth = Mathf.Max(trackWidth - (horizontalPadding * 2f), 0f);
            float leftEdge = -trackWidth * 0.5f + horizontalPadding;

            if (routeStopCount <= 1)
            {
                return leftEdge + usableWidth * 0.5f;
            }

            float slotWidth = usableWidth / routeStopCount;
            return leftEdge + slotWidth * (stopIndex + 0.5f);
        }

        private static StageLocationPresentation ResolveStageOrderValue(int stageOrderValue)
        {
            return stageOrderValue switch
            {
                0 => new StageLocationPresentation
                {
                    hasSprite = true,
                    visualKind = LocationVisualKind.Straight,
                    label = "Straight"
                },
                1 => new StageLocationPresentation
                {
                    hasSprite = true,
                    visualKind = LocationVisualKind.Sasuke,
                    label = "SASUKE"
                },
                2 => new StageLocationPresentation
                {
                    hasSprite = true,
                    visualKind = LocationVisualKind.Animal,
                    label = "Animal"
                },
                4 => new StageLocationPresentation
                {
                    hasSprite = true,
                    visualKind = LocationVisualKind.Mountain,
                    label = "Mountain"
                },
                3 => new StageLocationPresentation
                {
                    hasSprite = false,
                    visualKind = LocationVisualKind.Mountain,
                    label = "Bonus"
                },
                _ => new StageLocationPresentation
                {
                    hasSprite = false,
                    visualKind = LocationVisualKind.Home,
                    label = $"Stage {stageOrderValue + 1}"
                }
            };
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
