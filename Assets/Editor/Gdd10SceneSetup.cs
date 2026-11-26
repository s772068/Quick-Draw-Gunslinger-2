using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GDD 1.0: panels, restyle, LevelSystem hierarchy with art refs.
/// </summary>
public static class Gdd10SceneSetup
{
    public static void ApplyLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
            ApplyScene(SceneManager.GetSceneAt(i));
    }

    public static void ApplyProjectScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string[] paths =
        {
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/MainMenu.unity"
        };

        foreach (string path in paths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ApplyScene(scene);
        }
    }

    public static void RebuildGeneratedPanels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Always bake sprites/coords into both project scenes so Scene view matches Play Mode.
        string[] paths =
        {
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/MainMenu.unity"
        };

        foreach (string path in paths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (scene.name == "GameScene")
            {
                Transform hud = FindDeepRoot(scene, "HUD");
                if (hud != null)
                {
                    DestroyChild(hud, "RoundWin");
                    DestroyChild(hud, "RoundLose");
                    DestroyChild(hud, "LevelComplete");
                    DestroyChild(hud, "AreYouSureAbort");
                }
            }
            else if (scene.name == "MainMenu")
            {
                Transform canvas = FindDeepRoot(scene, "Canvas");
                Transform parent = canvas != null ? (canvas.Find("HUD") ?? canvas.Find("SafeArea") ?? canvas) : null;
                if (parent != null)
                {
                    DestroyChild(parent, "WorldMap");
                    DestroyChild(parent, "CityLeaderboard");
                    DestroyChild(parent, "LeaderboardsOverlay");
                }
            }

            ApplyScene(scene);
        }

        Debug.Log("[Gdd10SceneSetup] Rebuilt generated panels with authored sprites/coords in scenes.");
    }

    public static void BatchApply()
    {
        ApplyProjectScenes();
        EditorApplication.Exit(0);
    }

    private static void ApplyScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        bool changed = false;
        if (scene.name == "GameScene")
            changed = ApplyGameScene(scene);
        else if (scene.name == "MainMenu")
            changed = ApplyMainMenu(scene);

        if (!changed)
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Gdd10SceneSetup] Applied GDD 1.0 to {scene.name}");
    }

    private static bool ApplyGameScene(UnityEngine.SceneManagement.Scene scene)
    {
        Transform hud = FindDeepRoot(scene, "HUD");
        if (hud == null)
        {
            Debug.LogWarning("[Gdd10SceneSetup] HUD not found in GameScene");
            return false;
        }

        bool changed = false;

        Transform roundStart = hud.Find("RoundStartPanel") ?? hud.Find("RoundStart");
        if (roundStart != null)
        {
            int before = roundStart.childCount;
            SceneUiFactory.EnsureRoundStartFields(roundStart);
            if (roundStart.childCount != before)
                changed = true;
        }

        changed |= EnsureNew(hud, "RoundWin", () => SceneUiFactory.EnsureRoundWin(hud));
        changed |= EnsureNew(hud, "RoundLose", () => SceneUiFactory.EnsureRoundLose(hud));
        changed |= EnsureNew(hud, "LevelComplete", () => SceneUiFactory.EnsureLevelComplete(hud));
        changed |= EnsureNew(hud, "AreYouSureAbort", () => SceneUiFactory.EnsureAreYouSureAbort(hud));

        if (hud.Find("PauseMenu") == null)
        {
            SceneUiFactory.EnsurePauseMenu(hud);
            changed = true;
        }

        changed |= DestroyChild(hud, "RoundFinish");
        changed |= DestroyChild(hud, "SeriesSummary");

        SceneUiFactory.RestyleGeneratedPanels(hud);
        changed = true;

        changed |= EnsureLevelSystem(scene);
        return changed;
    }

    private static bool ApplyMainMenu(UnityEngine.SceneManagement.Scene scene)
    {
        Transform canvas = FindDeepRoot(scene, "Canvas");
        if (canvas == null)
            return false;

        Transform parent = canvas.Find("HUD") ?? canvas.Find("SafeArea") ?? canvas;
        bool changed = false;
        changed |= EnsureNew(parent, "WorldMap", () => SceneUiFactory.EnsureWorldMap(parent));
        changed |= EnsureNew(parent, "CityLeaderboard", () => SceneUiFactory.EnsureCityLeaderboard(parent));
        changed |= EnsureNew(parent, "LeaderboardsOverlay", () => SceneUiFactory.EnsureLeaderboardsOverlay(parent));

        Transform oldBoards = FindDeepRoot(scene, "LeaderboardsPanel");
        if (oldBoards != null)
        {
            Object.DestroyImmediate(oldBoards.gameObject);
            changed = true;
        }

        SceneUiFactory.RestyleGeneratedPanels(parent);
        changed = true;
        return changed;
    }

    private static bool EnsureLevelSystem(UnityEngine.SceneManagement.Scene scene)
    {
        if (FindDeepRoot(scene, "LevelSystem") != null)
            return false;

        var root = new GameObject("LevelSystem");
        SceneManager.MoveGameObjectToScene(root, scene);
        var system = root.AddComponent<LevelSystem>();
        root.AddComponent<ScoreSystem>();

        var levels = new LevelDefinition[3];
        levels[0] = CreateLevel(root.transform, "DryCreek", "drycreek", "Dry Creek", "Сухой Ручей",
            "Assets/Art/Levels/01_drycreek", "01_drycreek");
        levels[1] = CreateLevel(root.transform, "DeadWell", "deadwell", "Dead Well", "Мёртвый Колодец",
            "Assets/Art/Levels/02_dead_well", "02_Dead Well");
        levels[2] = CreateLevel(root.transform, "RavenRock", "ravenrock", "Raven Rock", "Воронья Скала",
            "Assets/Art/Levels/03_ravenrock", "03_ravenrock");

        // Wire LevelSystem.levels via SerializedObject
        var so = new SerializedObject(system);
        var prop = so.FindProperty("levels");
        prop.arraySize = 3;
        for (int i = 0; i < 3; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static LevelDefinition CreateLevel(
        Transform parent, string goName, string cityId, string en, string ru, string folder, string prefix)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var level = go.AddComponent<LevelDefinition>();
        level.cityId = cityId;
        level.displayNameEn = en;
        level.displayNameRu = ru;
        level.backgroundHorizontal = LoadSprite(folder, prefix.Contains("Dead") ? "02_Dead Well_town_hor" :
            prefix.Contains("raven") || prefix.Contains("Raven") ? "03_town_RavenRock_hor" : "01_drycreek_town_hor");
        // Fix raven/dead paths explicitly:
        if (cityId == "drycreek")
        {
            level.backgroundHorizontal = LoadSprite(folder, "01_drycreek_town_hor");
            level.backgroundVertical = LoadSprite(folder, "01_drycreek_town_vert");
        }
        else if (cityId == "deadwell")
        {
            level.backgroundHorizontal = LoadSprite(folder, "02_Dead Well_town_hor");
            level.backgroundVertical = LoadSprite(folder, "02_Dead Well_town_vert");
        }
        else
        {
            level.backgroundHorizontal = LoadSprite(folder, "03_town_RavenRock_hor");
            level.backgroundVertical = LoadSprite(folder, "03_town_RavenRock_vert");
        }

        string[] ids = { "bungler", "cowboy", "deputy", "sheriff", "gunslinger" };
        string[] enNames = { "Bungler", "Cowboy", "Deputy", "Sheriff", "Gunslinger" };
        string[] ruNames = { "Недотёпа", "Ковбой", "Помошник шерифа", "Шериф", "Стрелок" };
        float[] ert = { 2.6f, 2.3f, 2.0f, 1.7f, 1.4f };
        float[] miss = { 0.35f, 0.3f, 0.25f, 0.2f, 0.15f };
        level.bellDelayRange = new Vector2(0.8f, 1.4f);

        var rounds = new RoundDefinition[5];
        for (int i = 0; i < 5; i++)
        {
            var rgo = new GameObject($"Round{i + 1}_{ids[i]}");
            rgo.transform.SetParent(go.transform, false);
            var round = rgo.AddComponent<RoundDefinition>();
            round.enemyId = ids[i];
            round.enemyNameEn = enNames[i];
            round.enemyNameRu = ruNames[i];
            round.enemyReactionTime = ert[i];
            round.enemyMissChance = miss[i];
            round.enemySprite = LoadEnemySprite(folder, cityId, ids[i]);
            round.enemyShotSprite = LoadEnemySprite(folder, cityId, ids[i] + "_shot");
            rounds[i] = round;
        }

        level.rounds = rounds;
        return level;
    }

    private static Sprite LoadEnemySprite(string folder, string cityId, string enemyId)
    {
        if (cityId == "drycreek")
            return LoadSprite(folder, $"01_drycreek_{enemyId}");
        if (cityId == "deadwell")
            return LoadSprite(folder, $"02_Dead Well_{enemyId}");
        return LoadSprite(folder, $"03_ravenrock_{enemyId}");
    }

    private static Sprite LoadSprite(string folder, string assetName)
    {
        string path = $"{folder}/{assetName}.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[Gdd10SceneSetup] Missing sprite: {path}");
        return sprite;
    }

    private static bool EnsureNew(Transform parent, string name, System.Func<GameObject> create)
    {
        if (parent.Find(name) != null)
            return false;
        return create() != null;
    }

    private static bool DestroyChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
            return false;
        Object.DestroyImmediate(t.gameObject);
        return true;
    }

    private static Transform FindDeepRoot(UnityEngine.SceneManagement.Scene scene, string name)
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
