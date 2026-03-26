using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kotatsu.Network
{
    public class OpponentPositionOverlay : MonoBehaviour
    {
        private const string OverlayCanvasName = "OpponentHudCanvas";
        private const string OverlayTextName = "OpponentPositionText";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<OpponentPositionOverlay>() != null) return;
            if (FindAnyObjectByType<PlayerController>() == null) return;
            var go = new GameObject("OpponentPositionOverlay");
            go.AddComponent<OpponentPositionOverlay>();
        }

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private Color labelColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color valueColor = new Color(0.55f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color selfColor = new Color(1f, 0.88f, 0.58f, 1f);

        private readonly Dictionary<string, Vector2> latestPositions = new Dictionary<string, Vector2>();
        private TextMeshProUGUI hudText;
        private bool subscribed;
        private float reconnectTimer;

        private void Awake()
        {
            if (FindAnyObjectByType<PlayerController>() == null)
            {
                Destroy(gameObject);
                return;
            }

            EnsureHudText();
            TryBindNetworkManager();
            RefreshText();
        }

        private void Update()
        {
            // Scene start order differs per scene; retry binding gently.
            if (networkManager == null || !subscribed)
            {
                reconnectTimer += Time.deltaTime;
                if (reconnectTimer >= 1f)
                {
                    reconnectTimer = 0f;
                    TryBindNetworkManager();
                    RefreshText();
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void EnsureHudText()
        {
            Canvas canvas = GetOrCreateOverlayCanvas();

            Transform existing = canvas.transform.Find(OverlayTextName);
            if (existing != null)
            {
                hudText = existing.GetComponent<TextMeshProUGUI>();
                ConfigureHudText(hudText);
                return;
            }

            var textGo = new GameObject(OverlayTextName);
            textGo.transform.SetParent(canvas.transform, false);
            hudText = textGo.AddComponent<TextMeshProUGUI>();
            ConfigureHudText(hudText);
        }

        private static Canvas GetOrCreateOverlayCanvas()
        {
            var canvasGo = GameObject.Find(OverlayCanvasName);
            if (canvasGo == null)
            {
                canvasGo = new GameObject(OverlayCanvasName);
            }

            var canvas = canvasGo.GetComponent<Canvas>();
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

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        private void ConfigureHudText(TextMeshProUGUI text)
        {
            if (text == null) return;

            text.font = ResolveFontAsset();
            if (text.font != null)
            {
                text.fontSharedMaterial = text.font.material;
            }
            text.fontSize = 22f;
            text.color = labelColor;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = "";

            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f, -170f);
            rt.sizeDelta = new Vector2(560f, 220f);
        }

        private static TMP_FontAsset ResolveFontAsset()
        {
            if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private void TryBindNetworkManager()
        {
            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>();
            }

            if (networkManager == null || subscribed) return;

            networkManager.OnGameConnected += OnGameConnected;
            networkManager.OnGameDisconnected += OnGameDisconnected;
            networkManager.OnMatchJoined += OnMatchJoined;
            networkManager.OnPlayerPositionUpdated += OnPlayerPositionUpdated;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || networkManager == null) return;

            networkManager.OnGameConnected -= OnGameConnected;
            networkManager.OnGameDisconnected -= OnGameDisconnected;
            networkManager.OnMatchJoined -= OnMatchJoined;
            networkManager.OnPlayerPositionUpdated -= OnPlayerPositionUpdated;
            subscribed = false;
        }

        private void OnGameConnected()
        {
            latestPositions.Clear();
            RefreshText();
        }

        private void OnGameDisconnected()
        {
            latestPositions.Clear();
            RefreshText();
        }

        private void OnMatchJoined(string matchId, string playerId)
        {
            RefreshText();
        }

        private void OnPlayerPositionUpdated(string playerId, float x, float y, float vx, float vy)
        {
            latestPositions[playerId] = new Vector2(x, y);
            RefreshText();
        }

        private void RefreshText()
        {
            if (hudText == null) return;

            if (networkManager == null || !networkManager.IsConnected)
            {
                hudText.text = "";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<color=#{ColorToHex(labelColor)}>Opponent Positions</color>");

            string selfId = networkManager.CurrentPlayerId;
            bool hasOpponent = false;

            foreach (KeyValuePair<string, Vector2> kv in latestPositions)
            {
                bool isSelf = !string.IsNullOrEmpty(selfId) && kv.Key == selfId;
                Color lineCol = isSelf ? selfColor : valueColor;
                string label = isSelf ? $"You ({kv.Key})" : kv.Key;
                if (!isSelf) hasOpponent = true;

                sb.AppendLine($"<color=#{ColorToHex(lineCol)}>{label}: x={kv.Value.x:F2}, y={kv.Value.y:F2}</color>");
            }

            if (!hasOpponent)
            {
                sb.Append($"<color=#{ColorToHex(labelColor)}>waiting opponent position...</color>");
            }

            hudText.text = sb.ToString();
            hudText.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
        }

        private static string ColorToHex(Color c)
        {
            Color32 c32 = c;
            return $"{c32.r:X2}{c32.g:X2}{c32.b:X2}";
        }
    }
}
