using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HideCommand : MonoBehaviour
{
    [Header("Canvas Settings")]
    public GameObject canvasToEnable;  // 切り替え先
    public GameObject canvasToDisable; // 切り替え元

    [Header("UI Focus")]
    public GameObject firstButton;     // 最初に選択するボタン

    [Header("Audio")]
    public AudioClip buttonSE;
    public AudioSource audioSource;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Check for M key
        if (keyboard.mKey.wasPressedThisFrame)
        {
            if (canvasToDisable != null && canvasToDisable.activeSelf)
            {
                SwitchMenu();
            }
        }

        // Check for O key
        if (keyboard.oKey.wasPressedThisFrame)
        {
            Initiate.Fade("(Test)", Color.black, 2.0f);
        }
    }

    public void SwitchMenu()
    {
        // 1. キャンバスの切り替え
        if (canvasToDisable != null) canvasToDisable.SetActive(false);
        
        if (canvasToEnable != null)
        {
            canvasToEnable.SetActive(true);

            // 2. SEの再生（Nullチェック付き）
            if (audioSource != null && buttonSE != null)
            {
                audioSource.PlayOneShot(buttonSE);
            }

            // 3. EventSystemの選択を更新
            if (firstButton != null)
            {
                // 一旦クリアしてからセットすると確実です
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstButton);
            }
        }
    }
}
