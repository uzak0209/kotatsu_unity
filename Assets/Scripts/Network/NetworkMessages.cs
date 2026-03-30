using System;
using UnityEngine;

namespace Kotatsu.Network
{
    public interface INetworkMessage
    {
        string t { get; }
    }

    [Serializable]
    public class MatchPlayerState
    {
        public string player_id;
        public string display_name;
        public int color_index;
        public int[] stage_order;
        public int current_stage_index;
    }

    #region Client to Server Messages

    [Serializable]
    public class JoinMessage : INetworkMessage
    {
        public string t = "join";
        public string token;
        string INetworkMessage.t => t;
    }

    [Serializable]
    public class ParamChangeMessage : INetworkMessage
    {
        public string t = "param_change";
        public int seq;
        public string param;
        public string direction;
        string INetworkMessage.t => t;
    }

    [Serializable]
    public class PosMessage : INetworkMessage
    {
        public string t = "pos";
        public int seq;
        public float x;
        public float y;
        public float vx;
        public float vy;
        string INetworkMessage.t => t;
    }

    [Serializable]
    public class StageProgressMessage : INetworkMessage
    {
        public string t = "stage_progress";
        public int current_stage_index;
        string INetworkMessage.t => t;
    }

    #endregion

    #region Server to Client Messages

    [Serializable]
    public class JoinOkMessage : INetworkMessage
    {
        public string t = "join_ok";
        public string match_id;
        public string player_id;
        public ParamValues @params;
        public long server_time_ms;
        string INetworkMessage.t => t;

        [Serializable]
        public class ParamValues
        {
            public int gravity;
            public int friction;
            public int speed;
        }
    }

    [Serializable]
    public class MatchStartedMessage : INetworkMessage
    {
        public string t = "match_started";
        public string match_id;
        public long started_at_unix;
        public MatchPlayerState[] players;
        public long server_time_ms;
        string INetworkMessage.t => t;
    }

    [Serializable]
    public class ParamAppliedMessage : INetworkMessage
    {
        public string t = "param_applied";
        public string from_player_id;
        public int seq;
        public ParamValues @params;
        public long next_param_change_at_unix;
        public long server_time_ms;
        string INetworkMessage.t => t;

        [Serializable]
        public class ParamValues
        {
            public int gravity;
            public int friction;
            public int speed;
        }
    }

    [Serializable]
    public class ErrorMessage : INetworkMessage
    {
        public string t = "error";
        public string code;
        public string message;
        string INetworkMessage.t => t;
    }

    [Serializable]
    public class PosBroadcastMessage : INetworkMessage
    {
        public string t = "pos";
        public string player_id;
        public int seq;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public long server_time_ms;
        string INetworkMessage.t => t;
    }

    [Serializable]
    public class StageProgressBroadcastMessage : INetworkMessage
    {
        public string t = "stage_progress";
        public string player_id;
        public int current_stage_index;
        public long server_time_ms;
        string INetworkMessage.t => t;
    }

    #endregion

    [Serializable]
    public class BaseMessage
    {
        public string t;
    }

    public static class MessageParser
    {
        public static INetworkMessage Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            BaseMessage baseMsg = JsonUtility.FromJson<BaseMessage>(json);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.t))
            {
                return null;
            }

            switch (baseMsg.t)
            {
                case "join_ok":
                    return JsonUtility.FromJson<JoinOkMessage>(json);
                case "match_started":
                    return JsonUtility.FromJson<MatchStartedMessage>(json);
                case "param_applied":
                    return JsonUtility.FromJson<ParamAppliedMessage>(json);
                case "error":
                    return JsonUtility.FromJson<ErrorMessage>(json);
                case "pos":
                    return JsonUtility.FromJson<PosBroadcastMessage>(json);
                case "stage_progress":
                    return JsonUtility.FromJson<StageProgressBroadcastMessage>(json);
                default:
                    Debug.LogWarning($"Unknown message type: {baseMsg.t}");
                    return null;
            }
        }
    }
}
