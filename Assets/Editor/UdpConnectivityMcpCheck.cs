using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kotatsu.Network;
using UnityEditor;
using UnityEngine;

namespace Kotatsu.EditorTools
{
    public static class UdpConnectivityMcpCheck
    {
        private const string MatchmakingBaseUrl = "http://kotatsu.ruxel.net:8080";
        private const string UdpHost = "kotatsu.ruxel.net";
        private const int UdpPort = 4433;
        private const string DisplayName = "McpUdpProbe";
        private const string TokenFileName = "udp_connectivity_token.txt";

        [Serializable]
        private class CreateMatchResponse
        {
            public string match_id;
        }

        [Serializable]
        private class JoinMatchResponse
        {
            public string token;
            public string udp_url;
        }

        [MenuItem("Tools/Network/Run UDP Check")]
        public static void RunFromMenu()
        {
            _ = RunAsync();
        }

        private static async Task RunAsync()
        {
            string resultPath = GetResultPath();
            WriteResult(resultPath, $"RUNNING {DateTime.UtcNow:O}");

            try
            {
                string token = TryReadTokenFromFile();
                if (string.IsNullOrEmpty(token))
                {
                    token = await FetchTokenAsync();
                }

                var joinOkTcs = new TaskCompletionSource<JoinOkMessage>();
                var errorTcs = new TaskCompletionSource<string>();

                var client = new GameNetworkClient();
                client.Initialize();

                client.OnConnected += () => client.SendJoin(token);
                client.OnJoinOk += msg => joinOkTcs.TrySetResult(msg);
                client.OnError += msg => errorTcs.TrySetResult($"{msg.code}: {msg.message}");
                client.OnRawError += raw => errorTcs.TrySetResult(raw);

                client.Connect(UdpHost, UdpPort);

                Task completed = await Task.WhenAny(joinOkTcs.Task, errorTcs.Task, Task.Delay(5000));
                client.Disconnect();

                if (completed == joinOkTcs.Task)
                {
                    JoinOkMessage ok = joinOkTcs.Task.Result;
                    WriteResult(resultPath, $"PASS join_ok match={ok.match_id} player={ok.player_id} time={DateTime.UtcNow:O}");
                    Debug.Log($"[UDP MCP CHECK] PASS: join_ok from {UdpHost}:{UdpPort}");
                    return;
                }

                if (completed == errorTcs.Task)
                {
                    string err = errorTcs.Task.Result;
                    WriteResult(resultPath, $"FAIL server_error={err} time={DateTime.UtcNow:O}");
                    Debug.LogError($"[UDP MCP CHECK] FAIL: {err}");
                    return;
                }

                WriteResult(resultPath, $"FAIL timeout_waiting_join_ok time={DateTime.UtcNow:O}");
                Debug.LogError($"[UDP MCP CHECK] FAIL: timeout waiting join_ok from {UdpHost}:{UdpPort}");
            }
            catch (Exception e)
            {
                WriteResult(resultPath, $"FAIL exception={e.GetType().Name}:{e.Message} time={DateTime.UtcNow:O}");
                Debug.LogError($"[UDP MCP CHECK] FAIL: {e}");
            }
        }

        private static async Task<string> FetchTokenAsync()
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            using var createReq = new StringContent("{}", Encoding.UTF8, "application/json");
            HttpResponseMessage createRes = await http.PostAsync($"{MatchmakingBaseUrl}/v1/matches", createReq);
            string createBody = await createRes.Content.ReadAsStringAsync();
            if (!createRes.IsSuccessStatusCode)
            {
                throw new Exception($"create_match_failed status={(int)createRes.StatusCode} body={createBody}");
            }

            CreateMatchResponse create = JsonUtility.FromJson<CreateMatchResponse>(createBody);
            if (create == null || string.IsNullOrEmpty(create.match_id))
            {
                throw new Exception("create_match_parse_failed");
            }

            string joinJson = JsonUtility.ToJson(new MatchmakingClient.JoinMatchRequest { display_name = DisplayName });
            using var joinReq = new StringContent(joinJson, Encoding.UTF8, "application/json");
            HttpResponseMessage joinRes = await http.PostAsync($"{MatchmakingBaseUrl}/v1/matches/{create.match_id}/join", joinReq);
            string joinBody = await joinRes.Content.ReadAsStringAsync();
            if (!joinRes.IsSuccessStatusCode)
            {
                throw new Exception($"join_match_failed status={(int)joinRes.StatusCode} body={joinBody}");
            }

            JoinMatchResponse join = JsonUtility.FromJson<JoinMatchResponse>(joinBody);
            if (join == null || string.IsNullOrEmpty(join.token))
            {
                throw new Exception("join_match_parse_failed");
            }

            return join.token;
        }

        private static string GetResultPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Temp", "udp_connectivity_result.txt");
        }

        private static string TryReadTokenFromFile()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string tokenPath = Path.Combine(projectRoot, "Temp", TokenFileName);
            if (!File.Exists(tokenPath))
            {
                return null;
            }

            string token = File.ReadAllText(tokenPath).Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }

        private static void WriteResult(string path, string text)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, text + Environment.NewLine);
        }
    }
}
