using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GDD 1.2: training level/button, star thresholds, UI defaults + pressed swaps.
/// </summary>
public static class Gdd12SceneSetup
{
    private const string ButtonDefaultPath = "Assets/Art/UI/Menus/Button_Default.png";
    private const string ButtonDefaultPressedPath = "Assets/Art/UI/Menus/Button_Default_pressed.png";
    private const string PanelBackgroundPath = "Assets/Art/UI/Menus/panel_background.png";
    private const string TrainingArtFolder = "Assets/Art/Levels/00_Training";

    [MenuItem("QDG2/Apply GDD 1.2")]
    public static void ApplyProjectScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Gdd05LocalizationSetup.Setup();

        string[] paths =
        {
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/MainMenu.unity"
        };

        foreach (string path in paths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            if (scene.name == "GameScene")
                changed = ApplyGameScene(scene);
            else if (scene.name == "MainMenu")
                changed = ApplyMainMenu(scene);

            changed |= ApplyUiDefaults(scene);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Gdd12SceneSetup] Applied GDD 1.2 to " + scene.name);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Gdd12SceneSetup] GDD 1.2 apply complete.");
    }

    private static bool ApplyGameScene(Scene scene)
    {
        bool changed = false;
        changed |= EnsureTrainingLevel(scene);
        changed |= EnsureLevelStarThresholds(scene);
        return changed;
    }

    private static bool ApplyMainMenu(Scene scene)
    {
        bool changed = false;
        changed |= EnsureTrainingButton(scene);
        changed |= EnsureStarsNetWorth(scene);
        changed |= EnsureNetWorthStarsLabel(scene);
        return changed;
    }

    private static bool EnsureTrainingLevel(Scene scene)
    {
        Transform levelSystem = FindDeepRoot(scene, "LevelSystem");
        if (levelSystem == null)
        {
            Debug.LogWarning("[Gdd12SceneSetup] LevelSystem missing in GameScene");
            return false;
        }

        Transform existing = levelSystem.Find("Training");
        LevelDefinition level;
        bool created = false;
        if (existing == null)
        {
            var go = new GameObject("Training");
            go.transform.SetParent(levelSystem, false);
            go.transform.SetAsFirstSibling();
            level = go.AddComponent<LevelDefinition>();
            created = true;
        }
        else
        {
            level = existing.GetComponent<LevelDefinition>();
            if (level == null)
                level = existing.gameObject.AddComponent<LevelDefinition>();
        }

        level.cityId = "training";
        level.displayNameEn = "Training";
        level.displayNameRu = "Обучение";
        level.bellDelayRange = new Vector2(1.5f, 1.5f);
        level.star1Threshold = 100;
        level.star2Threshold = 200;
        level.star3Threshold = 300;
        level.backgroundHorizontal = LoadSprite(TrainingArtFolder, "00_training_hor");
        level.backgroundVertical = LoadSprite(TrainingArtFolder, "00_training_vert");

        RoundDefinition round = null;
        if (level.rounds != null && level.rounds.Length > 0)
            round = level.rounds[0];
        if (round == null)
        {
            Transform roundTf = level.transform.Find("Round1_cowboy");
            if (roundTf == null)
            {
                var rgo = new GameObject("Round1_cowboy");
                rgo.transform.SetParent(level.transform, false);
                round = rgo.AddComponent<RoundDefinition>();
            }
            else
            {
                round = roundTf.GetComponent<RoundDefinition>() ?? roundTf.gameObject.AddComponent<RoundDefinition>();
            }
        }

        round.enemyId = "cowboy";
        round.enemyNameEn = "Cowboy";
        round.enemyNameRu = "Ковбой";
        round.enemyReactionTime = 99f;
        round.enemyMissChance = 1f;
        round.enemySprite = LoadSprite(TrainingArtFolder, "00_training_cowboy");
        round.enemyShotSprite = null;
        level.rounds = new[] { round };

        // Keep training in the levels array (optional index 3+) without breaking 0..2 cities.
        var system = levelSystem.GetComponent<LevelSystem>();
        if (system != null)
        {
            var so = new SerializedObject(system);
            var prop = so.FindProperty("levels");
            bool found = false;
            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == level)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                prop.arraySize += 1;
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = level;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ScoreSystem defaults → 100/200/300
        var score = levelSystem.GetComponent<ScoreSystem>();
        if (score != null)
        {
            var sso = new SerializedObject(score);
            sso.FindProperty("star1Threshold").intValue = 100;
            sso.FindProperty("star2Threshold").intValue = 200;
            sso.FindProperty("star3Threshold").intValue = 300;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(level);
        EditorUtility.SetDirty(round);
        return true;
    }

    private static bool EnsureLevelStarThresholds(Scene scene)
    {
        bool changed = false;
        var levels = Object.FindObjectsByType<LevelDefinition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < levels.Length; i++)
        {
            LevelDefinition level = levels[i];
            if (level == null || level.cityId == "training")
                continue;
            if (level.star1Threshold == 100 && level.star2Threshold == 200 && level.star3Threshold == 300)
                continue;
            // Only set if still at old ScoreSystem-like defaults or zero.
            if (level.star1Threshold == 0 || level.star1Threshold == 400)
            {
                level.star1Threshold = 100;
                level.star2Threshold = 200;
                level.star3Threshold = 300;
                EditorUtility.SetDirty(level);
                changed = true;
            }
        }

        return changed;
    }

    private static bool EnsureTrainingButton(Scene scene)
    {
        Transform canvas = FindDeepRoot(scene, "Canvas");
        if (canvas == null)
            return false;

        Transform parent = canvas.Find("HUD") ?? canvas.Find("SafeArea") ?? canvas;
        Transform begin = FindDeep(parent, "Begin") ?? FindDeep(parent, "Start");
        if (begin == null)
        {
            Debug.LogWarning("[Gdd12SceneSetup] Begin button not found");
            return false;
        }

        Transform existing = parent.Find("Training");
        if (existing == null)
            existing = FindDeep(parent, "Training");

        GameObject trainingGo;
        if (existing == null)
        {
            trainingGo = Object.Instantiate(begin.gameObject, begin.parent);
            trainingGo.name = "Training";
            var beginRt = begin as RectTransform;
            var rt = trainingGo.transform as RectTransform;
            if (beginRt != null && rt != null)
                rt.anchoredPosition = beginRt.anchoredPosition + new Vector2(0f, -90f);
        }
        else
        {
            trainingGo = existing.gameObject;
        }

        var button = trainingGo.GetComponent<Button>() ?? trainingGo.AddComponent<Button>();
        var label = trainingGo.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = LocalizationTables.Get(LocalizationTables.Keys.MenuTraining);

        var menu = Object.FindFirstObjectByType<MainMenuController>();
        if (menu != null)
        {
            var so = new SerializedObject(menu);
            var prop = so.FindProperty("trainingButton");
            if (prop != null)
            {
                prop.objectReferenceValue = button;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorUtility.SetDirty(trainingGo);
        return true;
    }

    private static bool EnsureStarsNetWorth(Scene scene)
    {
        Transform existing = FindDeepRoot(scene, "StarsNetWorth");
        if (existing != null)
        {
            var snw = existing.GetComponent<StarsNetWorth>() ?? existing.gameObject.AddComponent<StarsNetWorth>();
            snw.star1Threshold = 100;
            snw.star2Threshold = 200;
            snw.star3Threshold = 300;
            EditorUtility.SetDirty(snw);
            return true;
        }

        var go = new GameObject("StarsNetWorth");
        SceneManager.MoveGameObjectToScene(go, scene);
        var component = go.AddComponent<StarsNetWorth>();
        component.star1Threshold = 100;
        component.star2Threshold = 200;
        component.star3Threshold = 300;
        return true;
    }

    private static bool EnsureNetWorthStarsLabel(Scene scene)
    {
        Transform worldMap = FindDeepRoot(scene, "WorldMap");
        if (worldMap == null)
            return false;

        Transform richest = worldMap.Find("Panel/RichestBlock");
        if (richest == null)
            return false;

        if (richest.Find("Stars") != null || richest.Find("NetWorthStars") != null)
            return false;

        var labelGo = new GameObject("NetWorthStars", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(richest, false);
        var rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 36f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "---";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 22f;
        tmp.color = Color.black;
        return true;
    }

    private static bool ApplyUiDefaults(Scene scene)
    {
        Sprite buttonDefault = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonDefaultPath);
        Sprite buttonPressed = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonDefaultPressedPath);
        Sprite panelBg = AssetDatabase.LoadAssetAtPath<Sprite>(PanelBackgroundPath);
        if (buttonDefault == null)
            Debug.LogWarning("[Gdd12SceneSetup] Missing " + ButtonDefaultPath);
        if (panelBg == null)
            Debug.LogWarning("[Gdd12SceneSetup] Missing " + PanelBackgroundPath);

        var pressedByBase = BuildPressedMap();
        bool changed = false;

        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.scene.IsValid() || button.gameObject.scene != scene)
                continue;

            var image = button.targetGraphic as Image;
            if (image == null)
                image = button.GetComponent<Image>();
            if (image == null)
                continue;

            bool needsDefault = image.sprite == null
                                || image.sprite.name == "UISprite"
                                || image.sprite.name == "Background"
                                || image.sprite.name == "Knob";
            if (needsDefault && buttonDefault != null)
            {
                image.sprite = buttonDefault;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                changed = true;
            }

            Sprite pressed = null;
            if (image.sprite != null && pressedByBase.TryGetValue(image.sprite.name, out Sprite mapped))
                pressed = mapped;
            else if (needsDefault)
                pressed = buttonPressed;

            if (pressed != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = button.spriteState;
                state.pressedSprite = pressed;
                button.spriteState = state;
                changed = true;
            }

            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < labels.Length; j++)
            {
                TMP_Text label = labels[j];
                if (label == null)
                    continue;
                if (!label.enableAutoSizing || label.fontSizeMin != 12f || label.fontSizeMax != 48f)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 12f;
                    label.fontSizeMax = 48f;
                    changed = true;
                }
            }

            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(image);
        }

        Image[] images = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || !image.gameObject.scene.IsValid() || image.gameObject.scene != scene)
                continue;
            if (image.GetComponent<Button>() != null)
                continue;
            if (image.name == "Overlay" || image.name == "Background" || image.name == "HitSprite")
                continue;
            if (image.transform.parent != null && image.transform.parent.name.Contains("Crossfire"))
                continue;

            bool looksLikePanel = image.name == "Panel"
                                  || image.name.EndsWith("Panel")
                                  || image.name.Contains("Menu")
                                  || image.name.Contains("Overlay") == false && image.GetComponentInParent<Button>() == null
                                     && (image.name == "RoundWin" || image.name == "RoundLose" || image.name == "LevelComplete"
                                         || image.name == "RoundStart" || image.name == "RoundStartPanel"
                                         || image.name == "AreYouSureAbort" || image.name == "AuthPrompt"
                                         || image.name == "WorldMap" || image.name == "CityLeaderboard"
                                         || image.name == "LeaderboardsOverlay");

            // Only assign panel art when sprite is missing / built-in.
            bool needsPanel = image.sprite == null
                              || image.sprite.name == "UISprite"
                              || image.sprite.name == "Background";
            if (!needsPanel || panelBg == null)
                continue;

            // Heuristic: center panels / named panels, not tiny icons.
            RectTransform rt = image.rectTransform;
            bool largeEnough = rt != null && (rt.rect.width >= 120f || rt.rect.height >= 80f
                                              || (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one));
            if (!looksLikePanel && !largeEnough)
                continue;
            if (image.name == "Overlay")
                continue;

            image.sprite = panelBg;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            EditorUtility.SetDirty(image);
            changed = true;
        }

        return changed;
    }

    private static Dictionary<string, Sprite> BuildPressedMap()
    {
        var map = new Dictionary<string, Sprite>();
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art" });
        var sprites = new Dictionary<string, Sprite>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                continue;
            sprites[sprite.name] = sprite;
        }

        foreach (var kv in sprites)
        {
            string name = kv.Key;
            if (name.EndsWith("_pressed"))
                continue;
            string pressedName = name + "_pressed";
            if (sprites.TryGetValue(pressedName, out Sprite pressed))
                map[name] = pressed;
        }

        return map;
    }

    private static Sprite LoadSprite(string folder, string assetName)
    {
        string path = folder + "/" + assetName + ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning("[Gdd12SceneSetup] Missing sprite: " + path);
        return sprite;
    }

    private static Transform FindDeepRoot(Scene scene, string name)
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
