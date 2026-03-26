using UnityEngine;

public class scenechange : MonoBehaviour
{
    public string nextSceneName = "Game";
    public Color fadeColor = Color.black;
    public float fadeDuration = 2f;
    private bool isActive = false;

    // ボタンのクリックイベントで呼び出すメソッド
    public void LoadNextScene()
    {
        if (!isActiveAndEnabled) return;

        if(!isActive)
        {
            isActive = true;
            Initiate.Fade(nextSceneName, fadeColor, fadeDuration);
        }
    }
}
