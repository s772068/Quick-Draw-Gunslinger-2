#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Wires GDD 0.4: sprites, OrientationControls, MainMenu scene, build order.
/// Menu: QDG2 → Apply GDD 0.4 Setup
/// </summary>
public static class Gdd04Setup
{
    private const string MainScenePath = "Assets/Scenes/GameScene.unity";
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string Bell1Path = "Assets/Art/touchbtns/Bell_1.png";
    private const string Bell2Path = "Assets/Art/touchbtns/Bell_2.png";
    private const string SoundOnPath = "Assets/Art/SoundBtn1.png";
    private const string SoundOffPath = "Assets/Art/SoundBtn2.png";
    private const string MenuBtnPath = "Assets/Art/menuBtn.png";
    private const string GunExtendedPath = "Assets/Art/player_gun_extended.png";
    private const string BackVertPath = "Assets/Art/back_vert.png";
    private const string BackHorPath = "Assets/Art/back_hor.png";
    private const string MenuBackVertPath = "Assets/Art/back_menu_vert.png";
    private const string MenuBackHorPath = "Assets/Art/back_menu_hor.png";

    [MenuItem("QDG2/Apply GDD 0.4 Setup")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void Apply()
    {
        WireMainScene();
        EnsureMainMenuScene();
        SetBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Gdd04Setup] GDD 0.4 setup applied.");
    }

    private static void WireMainScene()
    {
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError("[Gdd04Setup] Missing " + MainScenePath);
            return;
        }

        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        var controller = Object.FindFirstObjectByType<RoundController>();
        if (controller == null)
        {
            Debug.LogError("[Gdd04Setup] RoundController not found.");
            return;
        }

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Gdd04Setup] Canvas not found.");
            return;
        }

        Transform hud = canvas.transform.Find("HUD");
        if (hud == null)
            hud = canvas.transform;

        if (hud.GetComponent<OrientationControls>() == null)
            hud.gameObject.AddComponent<OrientationControls>();

        Sprite bell1 = LoadSprite(Bell1Path);
        Sprite bell2 = LoadSprite(Bell2Path);
        Sprite soundOn = LoadSprite(SoundOnPath);
        Sprite soundOff = LoadSprite(SoundOffPath);
        Sprite menuBtn = LoadSprite(MenuBtnPath);
        Sprite gunExt = LoadSprite(GunExtendedPath);

        var so = new SerializedObject(controller);
        Assign(so, "bellSprite1", bell1);
        Assign(so, "bellSprite2", bell2);
        Assign(so, "soundOnSprite", soundOn);
        Assign(so, "soundOffSprite", soundOff);
        Assign(so, "playerGunExtendedSprite", gunExt);

        Transform top = hud.Find("TopPanel");
        if (top != null)
        {
            var menu = top.Find("Menu");
            var sound = top.Find("Sound");
            if (menu != null)
            {
                Assign(so, "menuButton", menu.GetComponent<Button>());
                var menuImg = menu.GetComponent<Image>();
                if (menuImg != null && menuBtn != null)
                    menuImg.sprite = menuBtn;
            }

            if (sound != null)
            {
                Assign(so, "soundButton", sound.GetComponent<Button>());
                Assign(so, "soundImage", sound.GetComponent<Image>());
                var soundImg = sound.GetComponent<Image>();
                if (soundImg != null && soundOn != null)
                    soundImg.sprite = soundOn;
            }

            Assign(so, "topPanel", top.gameObject);
        }

        var vert = hud.Find("VerticalControls");
        var hor = hud.Find("HorizontalControls");
        if (vert != null)
            Assign(so, "verticalControls", vert.gameObject);
        if (hor != null)
            Assign(so, "horizontalControls", hor.gameObject);

        var playerGun = GameObject.Find("PlayerGun");
        if (playerGun != null)
        {
            var gun = playerGun.transform.Find("Gun");
            if (gun != null)
            {
                var gunImg = gun.GetComponent<Image>();
                if (gunImg != null && gunExt != null)
                    gunImg.sprite = gunExt;
                Assign(so, "gunImage", gunImg);
            }

            var dir = playerGun.transform.Find("Direction");
            if (dir != null)
                Assign(so, "gunDirection", dir.GetComponent<RectTransform>());

            if (playerGun.GetComponent<PlayerGunAim>() == null)
                playerGun.AddComponent<PlayerGunAim>();
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Gdd04Setup] GameScene wired.");
    }

    private static void EnsureMainMenuScene()
    {
        Sprite soundOn = LoadSprite(SoundOnPath);
        Sprite soundOff = LoadSprite(SoundOffPath);
        Sprite backVert = LoadSprite(MenuBackVertPath);
        Sprite backHor = LoadSprite(MenuBackHorPath);
        if (backVert == null)
            backVert = LoadSprite(BackVertPath);
        if (backHor == null)
            backHor = LoadSprite(BackHorPath);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.1f, 0.08f, 1f);
        camera.tag = "MainCamera";
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            // Prefer Input System UI module (same as GameScene).
            var inputModType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModType != null)
                es.AddComponent(inputModType);
            else
                es.AddComponent<StandaloneInputModule>();
        }

        // Background with orientation swap (same art as game).
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = backVert != null ? backVert : backHor;
        bgImg.preserveAspect = false;
        bgImg.raycastTarget = false;
        var orientBg = bgGo.AddComponent<OrientationBackground>();
        var bgSo = new SerializedObject(orientBg);
        Assign(bgSo, "targetImage", bgImg);
        Assign(bgSo, "backVertSprite", backVert);
        Assign(bgSo, "backHorSprite", backHor);
        Assign(bgSo, "portraitSprite", backVert);
        Assign(bgSo, "landscapeSprite", backHor);
        bgSo.ApplyModifiedPropertiesWithoutUndo();

        // TopPanel Sound — same anchors as GameScene TopPanel Sound (top-left).
        var topGo = new GameObject("TopPanel", typeof(RectTransform));
        topGo.transform.SetParent(canvasGo.transform, false);
        var topRt = topGo.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = new Vector2(0f, -32f);
        topRt.sizeDelta = new Vector2(-64f, 100f);

        var soundGo = CreateImageButton(topGo.transform, "Sound", soundOn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(90f, 90f), new Vector2(0f, 0.5f));

        // Title
        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleGo.transform.SetParent(canvasGo.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -180f);
        titleRt.sizeDelta = new Vector2(900f, 160f);
        var title = titleGo.GetComponent<Text>();
        title.text = "Quick Draw Gunslinger 2";
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.fontSize = 48;
        title.raycastTarget = false;
        title.font = BuiltinFont();

        // Start
        var startGo = CreateImageButton(canvasGo.transform, "Begin", null,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(360f, 100f), new Vector2(0.5f, 0.5f));
        var startImg = startGo.GetComponent<Image>();
        startImg.color = new Color(0.85f, 0.7f, 0.35f, 1f);
        var startLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        startLabel.transform.SetParent(startGo.transform, false);
        StretchFull(startLabel.GetComponent<RectTransform>());
        var startText = startLabel.GetComponent<Text>();
        startText.text = "Start";
        startText.alignment = TextAnchor.MiddleCenter;
        startText.color = Color.black;
        startText.fontSize = 36;
        startText.raycastTarget = false;
        startText.font = BuiltinFont();

        var menuController = canvasGo.AddComponent<MainMenuController>();
        var menuSo = new SerializedObject(menuController);
        Assign(menuSo, "startButton", startGo.GetComponent<Button>());
        Assign(menuSo, "soundButton", soundGo.GetComponent<Button>());
        Assign(menuSo, "soundImage", soundGo.GetComponent<Image>());
        Assign(menuSo, "soundOnSprite", soundOn);
        Assign(menuSo, "soundOffSprite", soundOff);
        menuSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, MainMenuPath);
        Debug.Log("[Gdd04Setup] MainMenu scene created at " + MainMenuPath);
    }

    private static void SetBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuPath, true),
            new EditorBuildSettingsScene(MainScenePath, true)
        };
        Debug.Log("[Gdd04Setup] Build Settings: MainMenu → GameScene.");
    }

    private static GameObject CreateImageButton(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size,
        Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = Color.white;

        var button = go.GetComponent<Button>();
        button.targetGraphic = img;
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Font BuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning("[Gdd04Setup] Missing sprite: " + path);
        return sprite;
    }

    private static void Assign(SerializedObject so, string name, Object value)
    {
        var p = so.FindProperty(name);
        if (p != null)
            p.objectReferenceValue = value;
        else
            Debug.LogWarning("[Gdd04Setup] Missing property: " + name);
    }
}
#endif
