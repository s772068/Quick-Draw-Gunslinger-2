#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Applies GDD 0.3 scene wiring: centered touch buttons + portrait + WebGL profile.
/// Menu: QDG2 → Apply GDD 0.3 Setup
/// Batch: -executeMethod Gdd03Setup.Apply
/// </summary>
public static class Gdd03Setup
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string TouchDir = "Assets/Art/touchbtns";
    private const string BuildProfileDir = "Assets/Settings/Build Profiles";
    private const string WebGlProfilePath = BuildProfileDir + "/WebGL.asset";
    private const string AppliedFlagKey = "QDG2.Gdd03Setup.Applied";
    private const int LocalHostPort = 8080;

    private static HttpListener _httpListener;
    private static Thread _httpThread;
    private static string _httpRoot;
    private static Process _httpProcess;

    [InitializeOnLoadMethod]
    private static void AutoApplyOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(AppliedFlagKey, false))
                return;

            // Only auto-wire if MainScene is already the active scene (don't force-open).
            var active = EditorSceneManager.GetActiveScene();
            if (!active.IsValid() || active.path != ScenePath)
                return;

            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            bool already = canvas != null
                && (canvas.transform.Find("HUD/TouchControls") != null
                    || canvas.transform.Find("TouchControls") != null);
            if (already)
            {
                SessionState.SetBool(AppliedFlagKey, true);
                return;
            }

            Apply();
            SessionState.SetBool(AppliedFlagKey, true);
        };
    }

    [MenuItem("QDG2/Apply GDD 0.3 Setup")]
    public static void ApplyFromMenu()
    {
        SessionState.SetBool(AppliedFlagKey, false);
        Apply();
        SessionState.SetBool(AppliedFlagKey, true);
    }

    public static void Apply()
    {
        SetPortraitAsDefault();
        EnsureWebGlBuildProfile();
        WireMainSceneTouchControls();
        AssetDatabase.SaveAssets();
        Debug.Log("[Gdd03Setup] GDD 0.3 setup applied.");
    }

    private static void SetPortraitAsDefault()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        Debug.Log("[Gdd03Setup] Default orientation → Portrait.");
    }

    private static void EnsureWebGlBuildProfile()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(BuildProfileDir))
            AssetDatabase.CreateFolder("Assets/Settings", "Build Profiles");

        Type buildProfileType = Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor.CoreModule");
        if (buildProfileType == null)
        {
            Debug.LogWarning("[Gdd03Setup] BuildProfile type not found. Create WebGL profile manually: File → Build Profiles.");
            return;
        }

        UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath(WebGlProfilePath, buildProfileType);
        if (existing != null)
        {
            TrySetActiveBuildProfile(existing);
            Debug.Log("[Gdd03Setup] WebGL Build Profile already exists; set active.");
            return;
        }

        // Prefer public factory if available.
        MethodInfo createMethod = buildProfileType.GetMethod(
            "CreateInstance",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(BuildTarget) },
            null);

        UnityEngine.Object profile = null;
        if (createMethod != null)
        {
            profile = createMethod.Invoke(null, new object[] { BuildTarget.WebGL }) as UnityEngine.Object;
        }

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance(buildProfileType);
            var so = new SerializedObject(profile);
            TrySetInt(so, "m_Platform", (int)BuildTarget.WebGL);
            TrySetString(so, "m_ModuleName", "WebGLSupport");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.CreateAsset(profile, WebGlProfilePath);
        AssetDatabase.SaveAssets();
        TrySetActiveBuildProfile(profile);
        Debug.Log("[Gdd03Setup] Created WebGL Build Profile at " + WebGlProfilePath);
    }

    private static void TrySetActiveBuildProfile(UnityEngine.Object profile)
    {
        Type buildProfileType = profile.GetType();
        MethodInfo setActive = buildProfileType.GetMethod(
            "SetActiveBuildProfile",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { buildProfileType },
            null);
        setActive?.Invoke(null, new object[] { profile });
    }

    private static void TrySetInt(SerializedObject so, string name, int value)
    {
        var p = so.FindProperty(name);
        if (p != null)
            p.intValue = value;
    }

    private static void TrySetString(SerializedObject so, string name, string value)
    {
        var p = so.FindProperty(name);
        if (p != null)
            p.stringValue = value;
    }

    private static void WireMainSceneTouchControls()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[Gdd03Setup] Missing scene: " + ScenePath);
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var controller = UnityEngine.Object.FindFirstObjectByType<RoundController>();
        if (controller == null)
        {
            Debug.LogError("[Gdd03Setup] RoundController not found in GameScene.");
            return;
        }

        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Gdd03Setup] Canvas not found.");
            return;
        }

        Transform hud = canvas.transform.Find("HUD");
        Transform parent = hud != null ? hud : canvas.transform;

        Transform touchRoot = parent.Find("TouchControls");
        if (touchRoot == null)
        {
            var go = new GameObject("TouchControls", typeof(RectTransform));
            touchRoot = go.transform;
            touchRoot.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(400f, 400f);
        }

        var draw = EnsureTouchButton(touchRoot, "DrawTouchBtn",
            LoadSprite(TouchDir + "/DrawTouchBtn1.png"),
            LoadSprite(TouchDir + "/DrawTouchBtn2.png"),
            LoadSprite(TouchDir + "/DrawTouchBtn3.png"));

        var pull = EnsureTouchButton(touchRoot, "PullTouchBtn",
            LoadSprite(TouchDir + "/PullTouchBtn1.png"),
            LoadSprite(TouchDir + "/PullTouchBtn2.png"),
            LoadSprite(TouchDir + "/PullTouchBtn3.png"));

        var fire = EnsureTouchButton(touchRoot, "FireTouchBtn",
            LoadSprite(TouchDir + "/FireTouchBtn1.png"),
            LoadSprite(TouchDir + "/FireTouchBtn2.png"),
            LoadSprite(TouchDir + "/FireTouchBtn3.png"));

        var so = new SerializedObject(controller);
        AssignTouch(so, "drawTouch", draw);
        AssignTouch(so, "pullTouch", pull);
        AssignTouch(so, "fireTouch", fire);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Gdd03Setup] TouchControls created at center and wired to RoundController.");
    }

    private static void AssignTouch(SerializedObject so, string propertyName, TouchButtonRefs refs)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null)
            return;

        prop.FindPropertyRelative("button").objectReferenceValue = refs.button;
        prop.FindPropertyRelative("image").objectReferenceValue = refs.image;
        prop.FindPropertyRelative("inactiveSprite").objectReferenceValue = refs.inactive;
        prop.FindPropertyRelative("activeSprite").objectReferenceValue = refs.active;
        prop.FindPropertyRelative("pressedSprite").objectReferenceValue = refs.pressed;
    }

    private struct TouchButtonRefs
    {
        public Button button;
        public Image image;
        public Sprite inactive;
        public Sprite active;
        public Sprite pressed;
    }

    private static TouchButtonRefs EnsureTouchButton(
        Transform parent,
        string name,
        Sprite inactive,
        Sprite active,
        Sprite pressed)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(160f, 160f);
        }
        else
        {
            go = existing.gameObject;
            if (go.GetComponent<Image>() == null)
                go.AddComponent<Image>();
            if (go.GetComponent<Button>() == null)
                go.AddComponent<Button>();
        }

        var image = go.GetComponent<Image>();
        image.sprite = inactive != null ? inactive : active;
        image.preserveAspect = true;
        image.raycastTarget = true;
        image.color = Color.white;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.interactable = false;

        return new TouchButtonRefs
        {
            button = button,
            image = image,
            inactive = inactive,
            active = active,
            pressed = pressed
        };
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning("[Gdd03Setup] Missing sprite: " + path);
        return sprite;
    }

    [MenuItem("QDG2/Build And Run WebGL (GDD 0.3)")]
    public static void BuildAndRunWebGl()
    {
        EnsureWebGlBuildProfile();

        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "WebGL"));
        Directory.CreateDirectory(outDir);

        var scenes = EditorBuildSettings.scenes;
        if (scenes == null || scenes.Length == 0)
        {
            Debug.LogError("[Gdd03Setup] No scenes in Build Settings.");
            return;
        }

        // Do NOT use AutoRunPlayer — Unity's built-in localhost server often fails
        // (Firefox: connection refused on random ports like 51517).
        var options = new BuildPlayerOptions
        {
            scenes = Array.ConvertAll(scenes, s => s.path),
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[Gdd03Setup] WebGL build result: " + report.summary.result + " → " + outDir);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            return;

        ServeAndOpen(outDir);
    }

    [MenuItem("QDG2/Open Last WebGL Build in Browser")]
    public static void OpenLastWebGlBuild()
    {
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "WebGL"));
        if (!File.Exists(Path.Combine(outDir, "index.html")))
        {
            Debug.LogError("[Gdd03Setup] No WebGL build at " + outDir + ". Run Build And Run first.");
            return;
        }

        ServeAndOpen(outDir);
    }

    private static void ServeAndOpen(string buildDir)
    {
        if (!StartEditorHttpServer(buildDir, LocalHostPort))
        {
            Debug.LogError("[Gdd03Setup] Failed to start local HTTP server on port " + LocalHostPort);
            return;
        }

        string url = "http://127.0.0.1:" + LocalHostPort + "/";
        Application.OpenURL(url);
        Debug.Log("[Gdd03Setup] Serving WebGL at " + url + " (keep Unity open). Root: " + buildDir);
    }

    private static bool StartEditorHttpServer(string root, int port)
    {
        StopLocalServer();

        _httpRoot = Path.GetFullPath(root);
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            _httpListener.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Gdd03Setup] HttpListener.Start failed: " + ex.Message);
            _httpListener = null;
            return false;
        }

        _httpThread = new Thread(HttpServerLoop)
        {
            IsBackground = true,
            Name = "QDG2-WebGL-Http"
        };
        _httpThread.Start();
        return true;
    }

    private static void HttpServerLoop()
    {
        HttpListener listener = _httpListener;
        string root = _httpRoot;
        if (listener == null || root == null)
            return;

        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            try
            {
                HandleHttpRequest(ctx, root);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Gdd03Setup] HTTP request error: " + ex.Message);
                try { ctx.Response.Abort(); } catch { /* ignore */ }
            }
        }
    }

    private static void HandleHttpRequest(HttpListenerContext ctx, string root)
    {
        string rel = Uri.UnescapeDataString(ctx.Request.Url.AbsolutePath.TrimStart('/'));
        if (string.IsNullOrWhiteSpace(rel))
            rel = "index.html";

        string path = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
        string rootFull = Path.GetFullPath(root);
        if (!path.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.Close();
            return;
        }

        if (!File.Exists(path))
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        string name = path.Replace('\\', '/').ToLowerInvariant();

        ctx.Response.ContentType = GuessContentType(name);
        if (name.EndsWith(".br", StringComparison.Ordinal))
            ctx.Response.AddHeader("Content-Encoding", "br");
        else if (name.EndsWith(".gz", StringComparison.Ordinal))
            ctx.Response.AddHeader("Content-Encoding", "gzip");

        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    private static string GuessContentType(string pathLower)
    {
        if (pathLower.Contains(".html")) return "text/html; charset=utf-8";
        if (pathLower.Contains(".js")) return "application/javascript";
        if (pathLower.Contains(".wasm")) return "application/wasm";
        if (pathLower.Contains(".json")) return "application/json";
        if (pathLower.Contains(".css")) return "text/css";
        if (pathLower.Contains(".png")) return "image/png";
        if (pathLower.Contains(".jpg") || pathLower.Contains(".jpeg")) return "image/jpeg";
        if (pathLower.Contains(".svg")) return "image/svg+xml";
        if (pathLower.Contains(".data")) return "application/octet-stream";
        return "application/octet-stream";
    }

    private static void StopLocalServer()
    {
        HttpListener listener = _httpListener;
        _httpListener = null;

        if (listener != null)
        {
            try { listener.Stop(); } catch { /* ignore */ }
            try { listener.Close(); } catch { /* ignore */ }
        }

        Thread thread = _httpThread;
        _httpThread = null;
        if (thread != null && thread.IsAlive)
        {
            try { thread.Join(500); } catch { /* ignore */ }
        }

        if (_httpProcess != null)
        {
            try
            {
                if (!_httpProcess.HasExited)
                    _httpProcess.Kill();
                _httpProcess.Dispose();
            }
            catch { /* ignore */ }
            _httpProcess = null;
        }

        _httpRoot = null;
    }
}
#endif
