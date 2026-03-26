using UnityEngine;

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
        if (!isActiveAndEnabled) return;

        if(!isActive)
        {
            isActive = true;
            if (audioSource != null && buttonSE != null)
            {
                audioSource.PlayOneShot(buttonSE);
            }
            Initiate.Fade(nextSceneName, fadeColor, fadeDuration);
        }
    }
}
