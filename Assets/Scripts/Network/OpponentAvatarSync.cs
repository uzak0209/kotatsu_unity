using System.Collections.Generic;
using UnityEngine;

namespace Kotatsu.Network
{
    public class OpponentAvatarSync : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<OpponentAvatarSync>() != null) return;
            if (FindAnyObjectByType<PlayerController>() == null) return;
            var go = new GameObject("OpponentAvatarSync");
            go.AddComponent<OpponentAvatarSync>();
        }

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Smoothing")]
        [SerializeField] private float smoothSharpness = 16f;
        [SerializeField] private float snapDistance = 2.5f;
        [SerializeField] private float maxExtrapolationSeconds = 0.2f;

        [Header("Avatar")]
        [SerializeField] private int sortingOrder = 5;
        [SerializeField] private float stalePlayerTimeout = 10f;

        private class RemoteAvatar
        {
            public string playerId;
            public GameObject gameObject;
            public Vector2 netPosition;
            public Vector2 netVelocity;
            public float lastPacketTime;
            public bool hasInitialPosition;
        }

        private readonly Dictionary<string, RemoteAvatar> avatarsByPlayerId = new Dictionary<string, RemoteAvatar>();
        private bool subscribed;
        private float reconnectTimer;

        private void Awake()
        {
            if (FindAnyObjectByType<PlayerController>() == null)
            {
                Destroy(gameObject);
                return;
            }

            TryBindNetworkManager();
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

            float now = Time.time;
            List<string> staleKeys = null;

            foreach (KeyValuePair<string, RemoteAvatar> kv in avatarsByPlayerId)
            {
                RemoteAvatar avatar = kv.Value;
                if (avatar == null || avatar.gameObject == null) continue;

                float sinceLastPacket = now - avatar.lastPacketTime;
                if (sinceLastPacket > stalePlayerTimeout)
                {
                    if (staleKeys == null) staleKeys = new List<string>();
                    staleKeys.Add(kv.Key);
                    continue;
                }

                if (!avatar.hasInitialPosition) continue;

                float extrapolation = Mathf.Clamp(sinceLastPacket, 0f, maxExtrapolationSeconds);
                Vector2 predicted = avatar.netPosition + avatar.netVelocity * extrapolation;

                Transform tr = avatar.gameObject.transform;
                Vector2 current = tr.position;
                if (Vector2.Distance(current, predicted) > snapDistance)
                {
                    tr.position = new Vector3(predicted.x, predicted.y, 0f);
                    continue;
                }

                float t = 1f - Mathf.Exp(-smoothSharpness * Time.deltaTime);
                Vector2 smoothed = Vector2.Lerp(current, predicted, t);
                tr.position = new Vector3(smoothed.x, smoothed.y, 0f);
            }

            if (staleKeys == null) return;
            for (int i = 0; i < staleKeys.Count; i++)
            {
                RemoveAvatar(staleKeys[i]);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ClearAvatars();
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
            ClearAvatars();
        }

        private void OnGameDisconnected()
        {
            ClearAvatars();
        }

        private void OnMatchJoined(string matchId, string playerId)
        {
            ClearAvatars();
        }

        private void OnPlayerPositionUpdated(string playerId, float x, float y, float vx, float vy)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            string selfId = networkManager != null ? networkManager.CurrentPlayerId : null;
            if (!string.IsNullOrEmpty(selfId) && playerId == selfId) return;

            RemoteAvatar avatar = GetOrCreateAvatar(playerId);
            if (avatar == null) return;

            avatar.netPosition = new Vector2(x, y);
            avatar.netVelocity = new Vector2(vx, vy);
            avatar.lastPacketTime = Time.time;

            if (!avatar.hasInitialPosition)
            {
                avatar.gameObject.transform.position = new Vector3(x, y, 0f);
                avatar.hasInitialPosition = true;
            }
        }

        private RemoteAvatar GetOrCreateAvatar(string playerId)
        {
            if (avatarsByPlayerId.TryGetValue(playerId, out RemoteAvatar existing))
            {
                return existing;
            }

            var go = new GameObject($"Opponent_{playerId}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;

            if (go.GetComponent<SketchCharacterLook>() == null)
            {
                go.AddComponent<SketchCharacterLook>();
            }

            var created = new RemoteAvatar
            {
                playerId = playerId,
                gameObject = go,
                lastPacketTime = Time.time,
                hasInitialPosition = false
            };
            avatarsByPlayerId[playerId] = created;

            return created;
        }

        private void RemoveAvatar(string playerId)
        {
            if (!avatarsByPlayerId.TryGetValue(playerId, out RemoteAvatar avatar)) return;
            avatarsByPlayerId.Remove(playerId);

            if (avatar != null && avatar.gameObject != null)
            {
                Destroy(avatar.gameObject);
            }
        }

        private void ClearAvatars()
        {
            foreach (KeyValuePair<string, RemoteAvatar> kv in avatarsByPlayerId)
            {
                RemoteAvatar avatar = kv.Value;
                if (avatar != null && avatar.gameObject != null)
                {
                    Destroy(avatar.gameObject);
                }
            }
            avatarsByPlayerId.Clear();
        }
    }
}
