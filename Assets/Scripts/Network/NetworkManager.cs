using System;
using System.Net;
using UnityEngine;

namespace Kotatsu.Network
{
    public class NetworkManager : MonoBehaviour
    {
        private const string DefaultMatchmakingUrl = "http://kotatsu.ruxel.net:8080";
        private const string LegacyLocalMatchmakingUrl = "http://127.0.0.1:8080";

        [Header("Server Configuration")]
        [SerializeField] private string matchmakingUrl = DefaultMatchmakingUrl;
        [SerializeField] private string displayName = "Player";

        private MatchmakingClient matchmakingClient;
        private GameNetworkClient gameClient;

        // Current session state
        private string currentMatchId;
        private string currentPlayerId;
        private string currentToken;
        private string realtimeHost;
        private int realtimePort;

        // Events
        public event Action<string> OnMatchCreated;
        public event Action<string, string> OnMatchJoined; // matchId, playerId
        public event Action OnGameConnected;
        public event Action OnGameDisconnected;
        public event Action<string, long> OnMatchStarted;
        public event Action<string, int, int, int> OnPlayerParamsChanged; // playerId, gravity, friction, speed
        public event Action<string, float, float, float, float> OnPlayerPositionUpdated; // playerId, x, y, vx, vy
        public event Action<string> OnNetworkError;

        public bool IsConnected => gameClient != null && gameClient.IsConnected;
        public string CurrentMatchId => currentMatchId;
        public string CurrentPlayerId => currentPlayerId;

        private void Awake()
        {
            matchmakingUrl = NormalizeMatchmakingUrl(matchmakingUrl);

            // Initialize clients
            matchmakingClient = new MatchmakingClient(matchmakingUrl);
            gameClient = new GameNetworkClient();
            gameClient.Initialize();

            // Setup game client event handlers
            SetupGameClientEvents();
        }

        private static string NormalizeMatchmakingUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return DefaultMatchmakingUrl;
            }

            string trimmed = url.Trim();
            if (string.Equals(trimmed, LegacyLocalMatchmakingUrl, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultMatchmakingUrl;
            }

            return trimmed;
        }

        private void OnDestroy()
        {
            gameClient?.Disconnect();
        }

        private void SetupGameClientEvents()
        {
            gameClient.OnConnected += () =>
            {
                gameClient.SendJoin(currentToken);
            };

            gameClient.OnDisconnected += () =>
            {
                OnGameDisconnected?.Invoke();
            };

            gameClient.OnJoinOk += (msg) =>
            {
                OnGameConnected?.Invoke();
                if (msg?.@params != null)
                {
                    OnPlayerParamsChanged?.Invoke(msg.player_id, msg.@params.gravity, msg.@params.friction, msg.@params.speed);
                }
            };

            gameClient.OnMatchStarted += (msg) =>
            {
                if (msg == null || string.IsNullOrWhiteSpace(msg.match_id))
                {
                    return;
                }

                currentMatchId = msg.match_id;
                OnMatchStarted?.Invoke(msg.match_id, msg.started_at_unix);
            };

            gameClient.OnParamApplied += (msg) =>
            {
                if (msg?.@params != null)
                {
                    OnPlayerParamsChanged?.Invoke(msg.from_player_id, msg.@params.gravity, msg.@params.friction, msg.@params.speed);
                }
            };

            gameClient.OnPositionUpdate += (msg) =>
            {
                OnPlayerPositionUpdated?.Invoke(msg.player_id, msg.x, msg.y, msg.vx, msg.vy);
            };

            gameClient.OnError += (msg) =>
            {
                Debug.LogError($"Server error [{msg.code}]: {msg.message}");
                OnNetworkError?.Invoke($"{msg.code}: {msg.message}");
            };

            gameClient.OnRawError += (error) =>
            {
                Debug.LogError($"Network error: {error}");
                OnNetworkError?.Invoke(error);
            };
        }

        #region Public API

        // Create a new match
        public void CreateMatch(Action<string> onSuccess = null, Action<string> onError = null)
        {
            StartCoroutine(matchmakingClient.CreateMatch(
                response =>
                {
                    currentMatchId = response.match_id;
                    OnMatchCreated?.Invoke(currentMatchId);
                    onSuccess?.Invoke(currentMatchId);
                },
                error =>
                {
                    Debug.LogError($"Failed to create match: {error}");
                    OnNetworkError?.Invoke(error);
                    onError?.Invoke(error);
                }
            ));
        }

        // Join an existing match
        public void JoinMatch(string matchId, Action onSuccess = null, Action<string> onError = null)
        {
            StartCoroutine(matchmakingClient.JoinMatch(
                matchId,
                displayName,
                response =>
                {
                    currentMatchId = response.match_id;
                    currentPlayerId = response.player_id;
                    currentToken = response.token;

                    if (string.IsNullOrWhiteSpace(response.RealtimeUrl))
                    {
                        string errorMessage = "Join match response did not include a realtime server URL.";
                        Debug.LogError(errorMessage);
                        OnNetworkError?.Invoke(errorMessage);
                        onError?.Invoke(errorMessage);
                        return;
                    }

                    // Parse realtime URL (format: "udp://host:port", legacy "quic://host:port", or just "host:port")
                    ParseRealtimeUrl(response.RealtimeUrl);

                    OnMatchJoined?.Invoke(currentMatchId, currentPlayerId);

                    // Connect to game server
                    ConnectToGameServer();
                    onSuccess?.Invoke();
                },
                error =>
                {
                    Debug.LogError($"Failed to join match: {error}");
                    OnNetworkError?.Invoke(error);
                    onError?.Invoke(error);
                }
            ));
        }

        // Get current match information (players, capacity, etc.)
        public void GetMatchInfo(string matchId, Action<MatchmakingClient.MatchInfo> onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                onError?.Invoke("Match ID is empty.");
                return;
            }

            if (matchmakingClient == null)
            {
                onError?.Invoke("Matchmaking client is not initialized.");
                return;
            }

            StartCoroutine(matchmakingClient.GetMatchInfo(
                matchId.Trim(),
                onSuccess,
                onError
            ));
        }

        public void ListMatches(Action<MatchmakingClient.ListMatchesResponse> onSuccess = null, Action<string> onError = null)
        {
            if (matchmakingClient == null)
            {
                onError?.Invoke("Matchmaking client is not initialized.");
                return;
            }

            StartCoroutine(matchmakingClient.ListMatches(
                onSuccess,
                onError
            ));
        }

        public void DeleteMatch(string matchId, Action onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                onError?.Invoke("Match ID is empty.");
                return;
            }

            if (matchmakingClient == null)
            {
                onError?.Invoke("Matchmaking client is not initialized.");
                return;
            }

            StartCoroutine(matchmakingClient.DeleteMatch(
                matchId.Trim(),
                onSuccess,
                onError
            ));
        }

        public void StartMatch(string matchId, Action<MatchmakingClient.StartMatchResponse> onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                onError?.Invoke("Match ID is empty.");
                return;
            }

            if (matchmakingClient == null)
            {
                onError?.Invoke("Matchmaking client is not initialized.");
                return;
            }

            StartCoroutine(matchmakingClient.StartMatch(
                matchId.Trim(),
                response =>
                {
                    if (response != null && !string.IsNullOrWhiteSpace(response.match_id))
                    {
                        currentMatchId = response.match_id;
                        OnMatchStarted?.Invoke(response.match_id, response.started_at_unix);
                    }

                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"Failed to start match: {error}");
                    OnNetworkError?.Invoke(error);
                    onError?.Invoke(error);
                }
            ));
        }

        // Create and join a new match
        public void CreateAndJoinMatch(Action onSuccess = null, Action<string> onError = null)
        {
            CreateMatch(
                matchId => JoinMatch(matchId, onSuccess, onError),
                onError
            );
        }

        // Send parameter change request
        public void ChangeParameter(string param, string direction)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("Not connected to game server");
                return;
            }

            gameClient.SendParamChange(param, direction);
        }

        // Send position update
        public void UpdatePosition(float x, float y, float vx, float vy)
        {
            if (!IsConnected)
            {
                return; // Silently ignore if not connected
            }

            gameClient.SendPosition(x, y, vx, vy);
        }

        // Disconnect from current game
        public void Disconnect()
        {
            gameClient?.Disconnect();
            currentMatchId = null;
            currentPlayerId = null;
            currentToken = null;
        }

        #endregion

        #region Private Methods

        private void ParseRealtimeUrl(string url)
        {
            // Format: "udp://host:port", "quic://host:port", or "host:port"
            string cleaned = url.Replace("udp://", "").Replace("quic://", "").Replace("https://", "").Replace("http://", "");
            string[] parts = cleaned.Split(':');

            if (parts.Length >= 2)
            {
                realtimeHost = ResolveRealtimeHost(parts[0]);
                int.TryParse(parts[1], out realtimePort);
            }
            else
            {
                Debug.LogError($"Invalid realtime URL format: {url}");
                realtimeHost = "127.0.0.1";
                realtimePort = 4433;
            }
        }

        private string ResolveRealtimeHost(string rawHost)
        {
            if (string.IsNullOrWhiteSpace(rawHost))
            {
                return rawHost;
            }

            string candidate = rawHost.Trim();
            if (!TryExtractUrlHost(matchmakingUrl, out string matchmakingHost))
            {
                return candidate;
            }

            if (ShouldPreferLanHost(candidate, matchmakingHost))
            {
                return matchmakingHost;
            }

            return candidate;
        }

        private static bool TryExtractUrlHost(string url, out string host)
        {
            host = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                host = uri.Host;
                return !string.IsNullOrWhiteSpace(host);
            }

            string normalized = url.Contains("://", StringComparison.Ordinal) ? url : $"http://{url}";
            if (Uri.TryCreate(normalized, UriKind.Absolute, out uri))
            {
                host = uri.Host;
                return !string.IsNullOrWhiteSpace(host);
            }

            return false;
        }

        private static bool ShouldPreferLanHost(string realtimeHostCandidate, string matchmakingHost)
        {
            if (string.IsNullOrWhiteSpace(realtimeHostCandidate) || string.IsNullOrWhiteSpace(matchmakingHost))
            {
                return false;
            }

            if (string.Equals(realtimeHostCandidate, matchmakingHost, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool realtimeIsDnsName = Uri.CheckHostName(realtimeHostCandidate) == UriHostNameType.Dns;
            bool realtimeIsPrivate = IsPrivateOrLoopbackHost(realtimeHostCandidate);
            bool matchmakingIsPrivate = IsPrivateOrLoopbackHost(matchmakingHost);

            return realtimeIsDnsName && !realtimeIsPrivate && matchmakingIsPrivate;
        }

        private static bool IsPrivateOrLoopbackHost(string host)
        {
            if (!IPAddress.TryParse(host, out IPAddress address))
            {
                return false;
            }

            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                byte[] bytes = address.GetAddressBytes();
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168);
            }

            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return true;
            }

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                byte[] bytes = address.GetAddressBytes();
                return bytes.Length > 0 && (bytes[0] & 0xFE) == 0xFC; // fc00::/7 unique local
            }

            return false;
        }

        private void ConnectToGameServer()
        {
            if (string.IsNullOrEmpty(realtimeHost) || realtimePort == 0)
            {
                Debug.LogError("Invalid realtime server configuration");
                return;
            }

            gameClient.Connect(realtimeHost, realtimePort);
        }

        #endregion
    }
}
