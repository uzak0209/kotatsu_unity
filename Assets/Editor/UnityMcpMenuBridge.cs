using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class UnityMcpMenuBridge
{
    private const string OfficialMenuPath = "Window/MCP For Unity/Toggle MCP Window";

    [MenuItem("Tools/Unity MCP/Open Window")]
    public static void OpenUnityMcpWindow()
    {
        // First try the official menu path from the package.
        if (EditorApplication.ExecuteMenuItem(OfficialMenuPath))
        {
            Debug.Log("[Unity MCP] Opened via official menu path.");
            return;
        }

        // Fallback: call the package window API via reflection.
        try
        {
            Type windowType = Type.GetType("MCPForUnity.Editor.Windows.MCPForUnityEditorWindow, MCPForUnity.Editor");
            if (windowType == null)
            {
                Debug.LogError("[Unity MCP] MCPForUnity package type was not found. Check Package Manager and compile errors.");
                return;
            }

            MethodInfo showWindow = windowType.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static);
            if (showWindow == null)
            {
                Debug.LogError("[Unity MCP] ShowWindow() was not found on MCPForUnityEditorWindow.");
                return;
            }

            showWindow.Invoke(null, null);
            Debug.Log("[Unity MCP] Opened via reflection fallback.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Unity MCP] Failed to open window: {ex.Message}");
        }
    }

    [MenuItem("Tools/Unity MCP/Open Window", true)]
    public static bool ValidateOpenUnityMcpWindow()
    {
        // Keep the menu enabled even if package is missing, so users can see diagnostics.
        return true;
    }

    // Fallback aliases under Window for environments where top-level Tools is not visible.
    [MenuItem("Window/MCP For Unity/Open Window (Fallback)", priority = 50)]
    public static void OpenUnityMcpWindowFromWindowMenu()
    {
        OpenUnityMcpWindow();
    }

    [MenuItem("Window/MCP for Unity/Open Window (Alias)", priority = 51)]
    public static void OpenUnityMcpWindowFromLowercaseAlias()
    {
        OpenUnityMcpWindow();
    }
}
