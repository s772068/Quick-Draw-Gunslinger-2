using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Play Mode only: press S to save a PNG into docs/screenshots at the current Game View size.
/// </summary>
public static class PlayModeScreenshotHotkey
{
    private const string RelativeDir = "docs/screenshots";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying)
            return;

        var go = new GameObject("PlayModeScreenshotHotkey");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<PlayModeScreenshotHotkeyBehaviour>();
    }

    internal static void Capture()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string dir = Path.Combine(projectRoot, RelativeDir);
        Directory.CreateDirectory(dir);

        string fileName = $"shot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = Path.Combine(dir, fileName);

        // Captures at the end of the frame, using the current Game View resolution.
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[Screenshot] {path} ({Screen.width}x{Screen.height}). Set Game View size first for a fixed resolution.");
    }
}

public sealed class PlayModeScreenshotHotkeyBehaviour : MonoBehaviour
{
    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.sKey.wasPressedThisFrame)
            return;

        PlayModeScreenshotHotkey.Capture();
    }
}
