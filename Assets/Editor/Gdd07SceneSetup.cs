using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drops PauseMenu into GameScene and the leaderboard list into MainMenu
/// so both can be laid out in the editor.
/// </summary>
[InitializeOnLoad]
public static class Gdd07SceneSetup
{
    static Gdd07SceneSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += ApplyLoadedScenes;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () => ApplyScene(scene);
    }

    [MenuItem("QDG2/Apply GDD 0.7 Scene Objects")]
    public static void ApplyLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
            ApplyScene(SceneManager.GetSceneAt(i));
    }

    private static void ApplyScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        bool changed = false;
        if (scene.name == "GameScene")
            changed = EnsurePause(scene);
        else if (scene.name == "MainMenu")
            changed = EnsureMenuBoard(scene);

        if (!changed)
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool EnsurePause(UnityEngine.SceneManagement.Scene scene)
    {
        Transform hud = FindRoot(scene, "HUD");
        if (hud == null)
            return false;
        if (hud.Find("PauseMenu") != null && hud.Find("RoundFinish") != null && hud.Find("RoundFinish/AuthPrompt") != null)
            return false;

        bool created = false;
        if (hud.Find("PauseMenu") == null)
        {
            GameObject pause = SceneUiFactory.EnsurePauseMenu(hud);
            if (pause != null)
            {
                pause.SetActive(true);
                created = true;
            }
        }

        Transform finish = hud.Find("RoundFinish");
        if (finish != null && finish.Find("AuthPrompt") == null)
        {
            GameObject auth = SceneUiFactory.EnsureAuthPrompt(finish);
            if (auth != null)
            {
                auth.SetActive(false);
                created = true;
            }
        }

        return created;
    }

    private static bool EnsureMenuBoard(UnityEngine.SceneManagement.Scene scene)
    {
        Transform panel = FindRoot(scene, "LeaderboardsPanel");
        if (panel == null)
            return false;

        var view = panel.GetComponent<MenuLeaderboardPanel>();
        bool created = view == null;
        if (view == null)
            view = panel.gameObject.AddComponent<MenuLeaderboardPanel>();

        int before = panel.Find("BoardList") == null ? 0 : panel.Find("BoardList").childCount;
        view.EnsureScaffold();
        int after = panel.Find("BoardList") == null ? 0 : panel.Find("BoardList").childCount;
        return created || after > before;
    }

    private static Transform FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeep(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name)
            return t;

        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindDeep(t.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
