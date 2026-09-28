using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Converts UI.Text → TMP + LiberationSans SDF everywhere, keeps Zantroke on Title only.
/// </summary>
public static class Gdd05UiFonts
{
    private const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string ZantrokePath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Zantroke SDF.asset";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/GameScene.unity",
    };

    [MenuItem("QDG2/Apply LiberationSans SDF UI Fonts")]
    public static void Apply()
    {
        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);
        var zantroke = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ZantrokePath);
        if (liberation == null)
        {
            EditorUtility.DisplayDialog("UI Fonts", "LiberationSans SDF not found.", "OK");
            return;
        }

        int converted = 0;
        int retargeted = 0;

        string previous = SceneManager.GetActiveScene().path;
        foreach (string scenePath in ScenePaths)
        {
            if (!System.IO.File.Exists(scenePath))
                continue;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ConvertScene(liberation, zantroke, ref converted, ref retargeted);
            RebindRoundController();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(previous) && System.IO.File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        Debug.Log($"UI fonts: converted Text→TMP={converted}, TMP retargeted={retargeted}. Title kept on Zantroke.");
        EditorUtility.DisplayDialog(
            "UI Fonts",
            $"Done.\nText → TMP: {converted}\nTMP font set: {retargeted}\nTitle stays Zantroke SDF.",
            "OK");
    }

    private static void ConvertScene(TMP_FontAsset liberation, TMP_FontAsset zantroke, ref int converted, ref int retargeted)
    {
        // Convert legacy UI.Text first (except under Title).
        Text[] texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            Text ui = texts[i];
            if (ui == null)
                continue;
            if (IsUnderTitle(ui.transform))
                continue;

            ConvertUiTextToTmp(ui, liberation);
            converted++;
        }

        TMP_Text[] tmps = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tmps.Length; i++)
        {
            TMP_Text tmp = tmps[i];
            if (tmp == null)
                continue;

            if (IsUnderTitle(tmp.transform))
            {
                if (zantroke != null && tmp.font != zantroke)
                {
                    tmp.font = zantroke;
                    tmp.fontSharedMaterial = zantroke.material;
                    EditorUtility.SetDirty(tmp);
                    retargeted++;
                }

                continue;
            }

            // Hints D/P/F and all other TMP → LiberationSans SDF
            if (tmp.font != liberation)
            {
                tmp.font = liberation;
                tmp.fontSharedMaterial = liberation.material;
                EditorUtility.SetDirty(tmp);
                retargeted++;
            }
        }
    }

    private static void RebindRoundController()
    {
        RoundController[] controllers = Object.FindObjectsByType<RoundController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            RoundController rc = controllers[i];
            if (rc == null)
                continue;

            var so = new SerializedObject(rc);
            SerializedProperty resultProp = so.FindProperty("roundResultText");
            if (resultProp != null)
            {
                // Prefer object named like result / Win panel text.
                TMP_Text found = null;
                var all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int t = 0; t < all.Length; t++)
                {
                    if (all[t] == null)
                        continue;
                    string n = all[t].gameObject.name;
                    if (n == "RoundResult" || n == "Result" || n == "RoundResultText")
                    {
                        found = all[t];
                        break;
                    }
                }

                // Fallback: text under RoundFinish panel.
                if (found == null)
                {
                    var finish = GameObject.Find("RoundFinish");
                    if (finish == null)
                        finish = GameObject.Find("RoundFinishPanel");
                    if (finish != null)
                        found = finish.GetComponentInChildren<TMP_Text>(true);
                }

                if (found != null)
                {
                    resultProp.objectReferenceValue = found;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rc);
                }
            }
        }

        // MainMenu Begin label → TMP field
        MainMenuController[] menus = Object.FindObjectsByType<MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            MainMenuController menu = menus[i];
            if (menu == null)
                continue;

            var so = new SerializedObject(menu);
            SerializedProperty beginTmp = so.FindProperty("beginLabelTmp");
            var begin = GameObject.Find("Begin");
            if (begin != null)
            {
                var tmp = begin.GetComponentInChildren<TMP_Text>(true);
                if (beginTmp != null && tmp != null)
                    beginTmp.objectReferenceValue = tmp;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(menu);
            }
        }
    }

    private static bool IsUnderTitle(Transform t)
    {
        while (t != null)
        {
            if (t.name == "Title")
                return true;
            t = t.parent;
        }

        return false;
    }

    private static void ConvertUiTextToTmp(Text ui, TMP_FontAsset font)
    {
        GameObject go = ui.gameObject;
        string text = ui.text;
        Color color = ui.color;
        int fontSize = ui.fontSize;
        bool raycast = ui.raycastTarget;
        FontStyle style = ui.fontStyle;
        TextAnchor align = ui.alignment;

        Object.DestroyImmediate(ui, true);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = go.AddComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.font = font;
        tmp.fontSharedMaterial = font.material;
        tmp.raycastTarget = raycast;
        tmp.enableAutoSizing = false;
        tmp.alignment = MapAlignment(align);
        tmp.fontStyle = MapStyle(style);

        EditorUtility.SetDirty(tmp);
        EditorUtility.SetDirty(go);
    }

    private static TextAlignmentOptions MapAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static FontStyles MapStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold: return FontStyles.Bold;
            case FontStyle.Italic: return FontStyles.Italic;
            case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
            default: return FontStyles.Normal;
        }
    }
}
