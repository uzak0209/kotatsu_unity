using UnityEngine;
using UnityEngine.EventSystems;

public class keepfocus : MonoBehaviour
{
    private GameObject lastSelected;

    void Update()
    {
        // 現在の選択オブジェクトを確認
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            lastSelected = EventSystem.current.currentSelectedGameObject;
        }
        else
        {
            // もしフォーカスが外れたら、直前のオブジェクトに無理やり戻す
            EventSystem.current.SetSelectedGameObject(lastSelected);
        }
    }
}
// このスクリプトはCanvasGroupのBlocksRaycastsをオフにすること前提である