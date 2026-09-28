#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds MainScene greybox layout for GDD 0.1. Run via menu or batchmode:
/// -executeMethod MainSceneBuilder.Build
/// Auto-runs once if MainScene is missing (when Unity finishes compiling).
/// </summary>
public static class MainSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string EnemyPath = "Assets/Art/EnemyBody.png";
    private const string CrossfirePath = "Assets/Art/Crossfire.png";
    private const string GunPath = "Assets/Art/Gun.png";

    [InitializeOnLoadMethod]
    private static void AutoBuildIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (File.Exists(ScenePath))
                return;
            Build();
        };
    }

    [MenuItem("QDG2/Build MainScene (GDD 0.1)")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void Build()
    {
        EnsureFolders();
        ConfigureSprites();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.45f, 0.75f, 1f, 1f);
        camera.tag = "MainCamera";
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        var enemySprite = LoadSprite(EnemyPath);
        var crossfireSprite = LoadSprite(CrossfirePath);
        var gunSprite = LoadSprite(GunPath);

        // GDD: place at screen center for designer layout; slight offsets keep greybox playable.
        var enemyBody = CreateWorldSprite("EnemyBody", enemySprite, new Vector3(0f, 0.8f, 0f), new Vector3(1.2f, 1.6f, 1f), 0);

        var playerGun = new GameObject("PlayerGun");
        playerGun.SetActive(false);
        var gun = CreateWorldSprite("Gun", gunSprite, new Vector3(0f, -2.2f, 0f), new Vector3(1.4f, 0.7f, 1f), 1);
        gun.transform.SetParent(playerGun.transform, true);

        var crossfireStart = new GameObject("CrossfireStart");
        crossfireStart.transform.position = new Vector3(-3.5f, 0.8f, 0f);

        var crossfireTarget = new GameObject("CrossfireTarget");
        crossfireTarget.transform.position = enemyBody.transform.position;

        var crossfire = CreateWorldSprite("Crossfire", crossfireSprite, crossfireStart.transform.position, Vector3.one, 5);
        crossfire.SetActive(false);

        var canvasGo = CreateCanvas();
        CreateEventSystem();

        var roundStart = CreatePanel(canvasGo.transform, "RoundStart", new Vector2(0f, 0f), new Vector2(420f, 220f));
        var startButton = CreateButton(roundStart.transform, "Start", "Start", new Vector2(0f, 0f), new Vector2(200f, 64f));

        var bottomPanel = CreatePanel(canvasGo.transform, "BottomPanel", new Vector2(0f, -420f), new Vector2(900f, 140f));
        var signalButton = CreateButton(bottomPanel.transform, "Bell", "Bell", new Vector2(-300f, 0f), new Vector2(160f, 72f));
        var drawButton = CreateButton(bottomPanel.transform, "Draw", "Draw", new Vector2(-100f, 0f), new Vector2(160f, 72f));
        var pullButton = CreateButton(bottomPanel.transform, "Pull", "Pull", new Vector2(100f, 0f), new Vector2(160f, 72f));
        var fireButton = CreateButton(bottomPanel.transform, "Fire", "Fire", new Vector2(300f, 0f), new Vector2(160f, 72f));

        var roundFinish = CreatePanel(canvasGo.transform, "RoundFinish", new Vector2(0f, 0f), new Vector2(420f, 260f));
        var resultText = CreateText(roundFinish.transform, "RoundResult", "Win", new Vector2(0f, 50f), new Vector2(360f, 80f), 42);
        var restartButton = CreateButton(roundFinish.transform, "Restart", "Restart", new Vector2(0f, -60f), new Vector2(200f, 64f));
        roundFinish.SetActive(false);

        var controllerGo = new GameObject("RoundController");
        var controller = controllerGo.AddComponent<RoundController>();

        // Prefer RectTransform refs when present (UI layout). Fallback: add RectTransform via world objects won't apply;
        // this builder path remains greybox/world and mainly keeps menu compile-safe.
        var so = new SerializedObject(controller);
        so.FindProperty("enemyBody").objectReferenceValue = enemyBody.GetComponent<RectTransform>();
        so.FindProperty("enemyBodyImage").objectReferenceValue = null;
        so.FindProperty("crossfire").objectReferenceValue = crossfire.GetComponent<RectTransform>();
        so.FindProperty("crossfireStart").objectReferenceValue = crossfireStart.transform;
        so.FindProperty("crossfireTarget").objectReferenceValue = crossfireTarget.transform;
        so.FindProperty("playerGun").objectReferenceValue = playerGun;
        so.FindProperty("opaqueAlphaThreshold").floatValue = 0.1f;
        so.FindProperty("roundStartPanel").objectReferenceValue = roundStart;
        so.FindProperty("bottomPanel").objectReferenceValue = bottomPanel;
        so.FindProperty("roundFinishPanel").objectReferenceValue = roundFinish;
        so.FindProperty("roundResultText").objectReferenceValue = resultText;
        so.FindProperty("startButton").objectReferenceValue = startButton;
        so.FindProperty("bellButton").objectReferenceValue = signalButton;
        so.FindProperty("drawButton").objectReferenceValue = drawButton;
        so.FindProperty("pullButton").objectReferenceValue = pullButton;
        so.FindProperty("fireButton").objectReferenceValue = fireButton;
        so.FindProperty("restartButton").objectReferenceValue = restartButton;
        so.FindProperty("bellDelaySeconds").floatValue = 1f;
        so.FindProperty("aimDuration").floatValue = 1f;
        so.FindProperty("aimAfterMissDuration").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        EditorBuildSettings.scenes = scenes;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[QDG2] GameScene built at {ScenePath}");

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");
    }

    private static void ConfigureSprites()
    {
        ConfigureSprite(EnemyPath);
        ConfigureSprite(CrossfirePath);
        ConfigureSprite(GunPath);
        AssetDatabase.Refresh();
    }

    private static void ConfigureSprite(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            Debug.LogError($"[QDG2] Missing sprite at {assetPath}");
            return;
        }

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.isReadable = assetPath == EnemyPath;
        importer.SaveAndReimport();
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateWorldSprite(string name, Sprite sprite, Vector3 position, Vector3 scale, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    private static GameObject CreateCanvas()
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvasGo;
    }

    private static GameObject CreateEventSystem()
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        // Prefer Input System UI module when available.
        var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            es.AddComponent(inputModuleType);
        else
            es.AddComponent<StandaloneInputModule>();
        return es;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);
        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = Color.white;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        CreateText(go.transform, "Label", label, Vector2.zero, size, 28);
        return button;
    }

    private static Text CreateText(Transform parent, string name, string content, Vector2 anchoredPos, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var text = go.GetComponent<Text>();
        text.text = content;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return text;
    }
}
#endif
