using UnityEngine;
using UnityEngine.SceneManagement;

public class scenechange : MonoBehaviour
{
    public string nextSceneName = "Game";
    public Color fadeColor = Color.black;
    public float fadeDuration = 2f;
    public AudioClip buttonSE;
    public AudioSource audioSource;
    private bool isActive = false;

    // ボタンのクリックイベントで呼び出すメソッド
    public void LoadNextScene()
    {
        if(!isActive)
        {
            string resolvedSceneName = ResolveSceneName(nextSceneName);
            if (string.IsNullOrEmpty(resolvedSceneName))
            {
                Debug.LogError($"Scene '{nextSceneName}' was not found in build settings.");
                return;
            }

            isActive = true;
            if (audioSource != null && buttonSE != null)
            {
                audioSource.PlayOneShot(buttonSE);
            }

            Debug.Log(resolvedSceneName + "に遷移します");
            Initiate.Fade(resolvedSceneName, fadeColor, fadeDuration);
        }
    }

    private static string ResolveSceneName(string requestedSceneName)
    {
        if (string.IsNullOrWhiteSpace(requestedSceneName))
        {
            return string.Empty;
        }

        string trimmed = requestedSceneName.Trim();
        if (Application.CanStreamedLevelBeLoaded(trimmed))
        {
            return trimmed;
        }

        string normalizedRequested = NormalizeSceneKey(trimmed);
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(scenePath))
            {
                continue;
            }

            string candidateName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (NormalizeSceneKey(candidateName) == normalizedRequested)
            {
                return candidateName;
            }
        }

        return string.Empty;
    }

    private static string NormalizeSceneKey(string sceneName)
    {
        return sceneName.Replace("(", string.Empty)
                        .Replace(")", string.Empty)
                        .Replace(" ", string.Empty)
                        .ToLowerInvariant();
    }
}
