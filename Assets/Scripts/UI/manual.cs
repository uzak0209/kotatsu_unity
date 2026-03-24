using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class manual : MonoBehaviour
{
    public GameObject canvasToEnable; // 切り替え先のキャンバス
    public GameObject canvasToDisable; // 切り替え元のキャンバス
    public GameObject firstButton;
    public void SwitchMenu()
    {
        // キャンバスの表示・非表示
        if (canvasToDisable != null) canvasToDisable.gameObject.SetActive(false);
        if (canvasToEnable != null) canvasToEnable.gameObject.SetActive(true);
        //EventSystemの選択を更新
        EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
    }
}