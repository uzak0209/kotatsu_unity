using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Kotatsu.Network
{
    public class MatchmakingClient
    {
        private readonly string baseUrl;

        public MatchmakingClient(string baseUrl)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        [Serializable]
        public class CreateMatchResponse
        {
            public string match_id;
            public int max_players;
        }

        [Serializable]
        public class JoinMatchRequest
        {
            public string display_name;
        }

        [Serializable]
        public class JoinMatchResponse
        {
            public string match_id;
            public string player_id;
            public string token;
            public string quic_url;
            public long token_expires_at_unix;
        }

        [Serializable]
        public class PlayerInfo
        {
            public string player_id;
            public string display_name;
            public int gravity;
            public int friction;
            public int speed;
            public long next_param_change_at_unix;
        }

        [Serializable]
        public class MatchInfo
        {
            public string match_id;
            public int max_players;
            public long started_at_unix;
            public PlayerInfo[] players;
        }

        [Serializable]
        public class MatchSummary
        {
            public string match_id;
            public int max_players;
            public int player_count;
            public long started_at_unix;
            public PlayerInfo[] players;
        }

        [Serializable]
        public class ListMatchesResponse
        {
            public MatchSummary[] matches;
        }

        [Serializable]
        public class ErrorResponse
        {
            public string error;
        }

        [Serializable]
        public class StartMatchResponse
        {
            public string match_id;
            public long started_at_unix;
        }

        public IEnumerator CreateMatch(Action<CreateMatchResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/v1/matches";
            string jsonBody = "{}";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        CreateMatchResponse response = JsonUtility.FromJson<CreateMatchResponse>(request.downloadHandler.text);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = TryParseError(request.downloadHandler.text) ?? request.error;
                    onError?.Invoke($"Create match failed: {errorMsg}");
                }
            }
        }

        public IEnumerator JoinMatch(string matchId, string displayName, Action<JoinMatchResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/v1/matches/{matchId}/join";
            JoinMatchRequest requestBody = new JoinMatchRequest { display_name = displayName };
            string jsonBody = JsonUtility.ToJson(requestBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        JoinMatchResponse response = JsonUtility.FromJson<JoinMatchResponse>(request.downloadHandler.text);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = TryParseError(request.downloadHandler.text) ?? request.error;
                    onError?.Invoke($"Join match failed: {errorMsg}");
                }
            }
        }

        public IEnumerator GetMatchInfo(string matchId, Action<MatchInfo> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/v1/matches/{matchId}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        MatchInfo response = JsonUtility.FromJson<MatchInfo>(request.downloadHandler.text);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = TryParseError(request.downloadHandler.text) ?? request.error;
                    onError?.Invoke($"Get match info failed: {errorMsg}");
                }
            }
        }

        public IEnumerator ListMatches(Action<ListMatchesResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/v1/matches";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        ListMatchesResponse response = JsonUtility.FromJson<ListMatchesResponse>(request.downloadHandler.text);
                        onSuccess?.Invoke(response ?? new ListMatchesResponse { matches = Array.Empty<MatchSummary>() });
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = TryParseError(request.downloadHandler.text) ?? request.error;
                    onError?.Invoke($"List matches failed: {errorMsg}");
                }
            }
        }

        public IEnumerator DeleteMatch(string matchId, Action onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/v1/matches/{matchId}";

            using (UnityWebRequest request = UnityWebRequest.Delete(url))
            {
                yield return request.SendWebRequest();
                string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

                if (request.result == UnityWebRequest.Result.Success || request.responseCode == 204)
                {
                    onSuccess?.Invoke();
                }
                else
                {
                    string errorMsg = TryParseError(responseText) ?? request.error;
                    onError?.Invoke($"Delete match failed: {errorMsg}");
                }
            }
        }

        public IEnumerator StartMatch(string matchId, Action<StartMatchResponse> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/v1/matches/{matchId}/start";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        StartMatchResponse response = JsonUtility.FromJson<StartMatchResponse>(request.downloadHandler.text);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = TryParseError(request.downloadHandler.text) ?? request.error;
                    onError?.Invoke($"Start match failed: {errorMsg}");
                }
            }
        }

        private string TryParseError(string responseText)
        {
            try
            {
                if (!string.IsNullOrEmpty(responseText))
                {
                    ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(responseText);
                    return error.error;
                }
            }
            catch
            {
                // Ignore parse errors
            }
            return null;
        }
    }
}
