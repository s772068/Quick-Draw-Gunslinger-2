using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the pause menu into the scene so it can be laid out in the editor.
/// Play mode hides it until pause.
/// </summary>
public static class SceneUiFactory
{
    public static GameObject EnsurePauseMenu(Transform hud)
    {
        if (hud == null)
            return null;

        Transform existing = hud.Find("PauseMenu");
        if (existing != null)
            return existing.gameObject;

        Sprite panelSprite = null;
        TMP_FontAsset font = null;
        Transform finish = hud.Find("RoundFinish");
        if (finish != null)
        {
            var image = finish.GetComponent<Image>();
            if (image != null)
                panelSprite = image.sprite;
            var label = finish.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                font = label.font;
        }

        var root = new GameObject("PauseMenu", typeof(RectTransform));
        root.transform.SetParent(hud, false);
        Stretch(root.GetComponent<RectTransform>());

        var overlay = CreateImage("Overlay", root.transform, null, new Color(0f, 0f, 0f, 128f / 255f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var panel = CreateImage("Panel", root.transform, panelSprite, Color.white);
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(460f, 280f);
        panelRt.anchoredPosition = Vector2.zero;
        if (panelSprite != null)
            panel.type = Image.Type.Sliced;

        CreateMenuButton(panel.transform, "PauseContinue", new Vector2(0f, 46f), font, panelSprite);
        CreateMenuButton(panel.transform, "PauseExit", new Vector2(0f, -46f), font, panelSprite);
        root.transform.SetAsLastSibling();
        return root;
    }

    public static GameObject EnsureAuthPrompt(Transform roundFinish)
    {
        if (roundFinish == null)
            return null;

        Transform existing = roundFinish.Find("AuthPrompt");
        if (existing != null)
            return existing.gameObject;

        Sprite panelSprite = null;
        TMP_FontAsset font = null;
        var finishImage = roundFinish.GetComponent<Image>();
        if (finishImage != null)
            panelSprite = finishImage.sprite;
        var sample = roundFinish.GetComponentInChildren<TMP_Text>(true);
        if (sample != null)
            font = sample.font;

        var root = new GameObject("AuthPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(roundFinish, false);
        var image = root.GetComponent<Image>();
        image.sprite = panelSprite;
        image.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520f, 260f);
        rt.anchoredPosition = Vector2.zero;

        var textGo = new GameObject("AuthText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(root.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.sizeDelta = new Vector2(-24f, 90f);
        textRt.anchoredPosition = new Vector2(0f, -16f);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24;
        text.color = Color.black;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        CreateMenuButton(root.transform, "LoginBtn", new Vector2(0f, -20f), font, panelSprite);
        CreateMenuButton(root.transform, "SkipBtn", new Vector2(0f, -100f), font, panelSprite);
        return root;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static void CreateMenuButton(Transform parent, string name, Vector2 pos, TMP_FontAsset font, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 64f);
        rt.anchoredPosition = pos;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.85f, 0.75f, 0.55f, 1f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 26;
        text.color = Color.black;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
