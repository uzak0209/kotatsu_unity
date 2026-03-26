using UnityEngine;
using UnityEngine.UI;

public static class HudCanvasUtility
{
    public static Canvas GetOrCreateHudCanvas()
    {
        GameObject existingUi = GameObject.Find("ui");
        if (existingUi != null)
        {
            Canvas existingCanvas = existingUi.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                return existingCanvas;
            }
        }

        Canvas anyCanvas = Object.FindAnyObjectByType<Canvas>();
        if (anyCanvas != null)
        {
            return anyCanvas;
        }

        var canvasGo = new GameObject("ui");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
