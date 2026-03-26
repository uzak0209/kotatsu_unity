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
        if(!isActive)
        {
            isActive = true;
            audioSource.PlayOneShot(buttonSE);
            Debug.Log(nextSceneName + "に遷移します");
            Initiate.Fade(nextSceneName, fadeColor, fadeDuration);
        }
    }
}