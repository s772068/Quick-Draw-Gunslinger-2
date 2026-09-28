using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fixes Zantroke SDF source link + bakes EN/RU UI glyphs (GDD 0.5).
/// </summary>
public static class Gdd05FontFix
{
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Zantroke SDF.asset";
    private const string OtfPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Zantroke.otf";
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

    private const string BakeChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789: ,.!-?" +
        "Ковбои: быстрая рука 2" +
        "Начать игруНачать сериюЗановоПобедаПоражениеПродолжитьВыйти" +
        "Достань, взведи, стреляй" +
        "Quick Draw Gunslinger 2";

    [MenuItem("QDG2/Fix Zantroke Font (RU)")]
    public static void Fix()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(OtfPath);
        if (fontAsset == null || sourceFont == null)
        {
            Debug.LogError("Zantroke SDF or .otf missing.");
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(OtfPath);
        var so = new SerializedObject(fontAsset);
        so.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
        so.FindProperty("m_SourceFontFileGUID").stringValue = guid;
        var creation = so.FindProperty("m_CreationSettings");
        if (creation != null)
        {
            var g = creation.FindPropertyRelative("sourceFontFileGUID");
            if (g != null)
                g.stringValue = guid;
        }

        so.FindProperty("m_AtlasWidth").intValue = 2048;
        so.FindProperty("m_AtlasHeight").intValue = 2048;
        so.ApplyModifiedPropertiesWithoutUndo();

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        fontAsset.ClearFontAssetData(true);

        string missing;
        bool ok = fontAsset.TryAddCharacters(BakeChars, out missing);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();

        WireMainMenu(fontAsset);

        var sb = new StringBuilder();
        sb.AppendLine("Zantroke source GUID=" + guid);
        sb.AppendLine("TryAddCharacters ok=" + ok);
        sb.AppendLine("Missing=" + (string.IsNullOrEmpty(missing) ? "(none)" : missing));
        sb.AppendLine("Characters=" + fontAsset.characterTable.Count);
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog(
            "Zantroke Font Fix",
            ok
                ? "OK: RU/EN UI glyphs baked into Zantroke SDF."
                : "Partial: missing glyphs: " + missing,
            "OK");
    }

    private static void WireMainMenu(TMP_FontAsset fontAsset)
    {
        var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

        var title = GameObject.Find("Title");
        if (title != null)
        {
            var tmp = title.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                DestroySubMeshes(tmp);
                tmp.font = fontAsset;
                tmp.fontSharedMaterial = fontAsset.material;
                tmp.text = LocalizationTables.Get(LocalizationTables.Keys.GameTitle);
                tmp.ForceMeshUpdate(true);
                EditorUtility.SetDirty(tmp);
            }
        }

        var begin = GameObject.Find("Begin");
        if (begin != null)
        {
            var labelTf = begin.transform.Find("Label");
            if (labelTf != null)
                EnsureTmpLabel(labelTf.gameObject, fontAsset, LocalizationTables.Get(LocalizationTables.Keys.MenuBegin));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureTmpLabel(GameObject labelGo, TMP_FontAsset fontAsset, string text)
    {
        var ui = labelGo.GetComponent<Text>();
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Color c = ui != null ? ui.color : Color.black;
            float size = ui != null ? ui.fontSize : 48f;
            if (ui != null)
                Object.DestroyImmediate(ui, true);
            tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.color = c;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        DestroySubMeshes(tmp);
        tmp.font = fontAsset;
        tmp.fontSharedMaterial = fontAsset.material;
        tmp.text = text;
        tmp.ForceMeshUpdate(true);
        EditorUtility.SetDirty(tmp);
    }

    private static void DestroySubMeshes(TMP_Text tmp)
    {
        for (int i = tmp.transform.childCount - 1; i >= 0; i--)
        {
            var ch = tmp.transform.GetChild(i);
            if (ch != null && ch.name.Contains("SubMesh"))
                Object.DestroyImmediate(ch.gameObject);
        }
    }
}
