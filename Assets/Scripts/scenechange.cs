using UnityEngine;

public class scenechange : MonoBehaviour
{
    public string nextSceneName = "Game";
    public Color fadeColor = Color.black;
    public float fadeDuration = 1f;
    private bool isActive = false;

    // ボタンのクリックイベントで呼び出すメソッド
    public void LoadNextScene()
    {
        if(!isActive)
        {
            isActive = true;
            Debug.Log(nextSceneName + "に遷移します");
            Initiate.Fade(nextSceneName, fadeColor, fadeDuration);
        }
    }
}