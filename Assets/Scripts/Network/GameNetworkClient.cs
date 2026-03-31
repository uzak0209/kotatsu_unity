using System;
using UnityEngine;

namespace Kotatsu.Network
{
    public class GameNetworkClient
    {
        private SimpleUdpClient udpClient;
        private int posSeq;
        private int paramSeq;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<JoinOkMessage> OnJoinOk;
        public event Action<MatchStartedMessage> OnMatchStarted;
        public event Action<ParamAppliedMessage> OnParamApplied;
        public event Action<PosBroadcastMessage> OnPositionUpdate;
        public event Action<StageProgressBroadcastMessage> OnStageProgressUpdate;
        public event Action<ErrorMessage> OnError;
        public event Action<string> OnRawError;

        public bool IsConnected => udpClient != null && udpClient.IsConnected;

        public void Initialize()
        {
            udpClient = new SimpleUdpClient();
            udpClient.OnConnected += () => OnConnected?.Invoke();
            udpClient.OnDisconnected += () => OnDisconnected?.Invoke();
            udpClient.OnReliableMessage += HandleReliableMessage;
            udpClient.OnUnreliableMessage += HandleUnreliableMessage;
            udpClient.OnError += error =>
            {
                Debug.LogError($"UDP error: {error}");
                OnRawError?.Invoke(error);
            };
        }

        public void Connect(string host, int port)
        {
            if (udpClient == null)
            {
                Debug.LogError("GameNetworkClient not initialized. Call Initialize() first.");
                return;
            }

            udpClient.Connect(host, port);
        }

        public void Disconnect()
        {
            udpClient?.Disconnect();
        }

        public void SendJoin(string token)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("Not connected to server");
                return;
            }

            JoinMessage msg = new JoinMessage { token = token };
            udpClient.SendReliable(JsonUtility.ToJson(msg));
        }

        public void SendParamChange(string param, string direction)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("Not connected to server");
                return;
            }

            ParamChangeMessage msg = new ParamChangeMessage
            {
                seq = ++paramSeq,
                param = param,
                direction = direction
            };
            udpClient.SendReliable(JsonUtility.ToJson(msg));
        }

        public void SendPosition(float x, float y, float vx, float vy)
        {
            if (!IsConnected)
            {
                return;
            }

            PosMessage msg = new PosMessage
            {
                seq = ++posSeq,
                x = x,
                y = y,
                vx = vx,
                vy = vy
            };
            udpClient.SendUnreliable(JsonUtility.ToJson(msg));
        }

        public void SendStageProgress(int currentStageIndex)
        {
            if (!IsConnected)
            {
                return;
            }

            StageProgressMessage msg = new StageProgressMessage
            {
                current_stage_index = currentStageIndex
            };
            udpClient.SendUnreliable(JsonUtility.ToJson(msg));
        }

        private void HandleReliableMessage(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            INetworkMessage msg = MessageParser.Parse(json);
            if (msg == null)
            {
                Debug.LogWarning($"Failed to parse reliable message: {json}");
                return;
            }

            switch (msg.t)
            {
                case "join_ok":
                    OnJoinOk?.Invoke(msg as JoinOkMessage);
                    break;
                case "match_started":
                    OnMatchStarted?.Invoke(msg as MatchStartedMessage);
                    break;
                case "param_applied":
                    OnParamApplied?.Invoke(msg as ParamAppliedMessage);
                    break;
                case "error":
                    OnError?.Invoke(msg as ErrorMessage);
                    break;
                default:
                    Debug.LogWarning($"Unhandled reliable message type: {msg.t}");
                    break;
            }
        }

        private void HandleUnreliableMessage(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            INetworkMessage msg = MessageParser.Parse(json);
            if (msg == null)
            {
                return;
            }

            switch (msg.t)
            {
                case "pos":
                    OnPositionUpdate?.Invoke(msg as PosBroadcastMessage);
                    break;
                case "stage_progress":
                    OnStageProgressUpdate?.Invoke(msg as StageProgressBroadcastMessage);
                    break;
            }
        }
    }
}
