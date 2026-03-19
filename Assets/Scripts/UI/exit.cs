using UnityEngine;

public class exit : MonoBehaviour
{

    public void QuitGame()
    {
        ExitLogic();
    }

    private void ExitLogic()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}