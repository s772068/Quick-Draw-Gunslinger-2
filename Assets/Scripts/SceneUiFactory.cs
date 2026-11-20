using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds HUD / menu panels into the scene so they can be laid out in the editor.
/// Ensure* never overwrites an existing object — designer layout is preserved.
/// New panels size themselves from content so buttons always fit inside.
/// </summary>
public static class SceneUiFactory
{
    private const float PadX = 28f;
    private const float PadTop = 28f;
    private const float PadBottom = 28f;
    private const float Gap = 14f;
    private const float DefaultBtnW = 300f;
    private const float DefaultBtnH = 75f;
    private const float CityRowHeight = 120f;
    private const float CityIconBtnSize = 72f;
    public static GameObject EnsureTrainingRoot(Transform hud)
    {
        if (hud == null)
            return null;

        Transform existing = hud.Find("TrainingRoot");
        if (existing != null)
            return existing.gameObject;

        var root = CreateStretchRoot(hud, "TrainingRoot");
        var modal = CreateStretchRoot(root.transform, "Modal");
        var style = CaptureStyle(hud);
        var panel = BeginPanel("Panel", modal.transform, style);
        TMP_Text body = AddLabel(panel, "Body", string.Empty, 26f, 140f);
        ApplyTextAutoSize(body);

        const float buttonWidth = 220f;
        const float buttonHeight = 70f;
        const float buttonGap = 20f;
        Vector2 size = new Vector2(buttonWidth, buttonHeight);
        AddSlot(panel, buttonWidth * 2f + buttonGap, buttonHeight, out Vector2 position);
        Vector2 halfSpacing = Vector2.right * ((buttonWidth + buttonGap) * 0.5f);
        CreateMenuButton(panel.Root.transform, "Ok", position, style, size);
        CreateMenuButton(panel.Root.transform, "Retry", position - halfSpacing, style, size).gameObject.SetActive(false);
        CreateMenuButton(panel.Root.transform, "Ready", position + halfSpacing, style, size).gameObject.SetActive(false);

        FinishPanel(panel);
        panel.Root.GetComponent<RectTransform>().anchoredPosition = Vector2.up * 80f;
        root.SetActive(false);
        return root;
    }

    public static GameObject EnsurePauseMenu(Transform hud)
    {
        if (hud == null)
            return null;

        Transform existing = hud.Find("PauseMenu");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(hud);
        var root = CreateStretchRoot(hud, "PauseMenu");
        var overlay = CreateImage("Overlay", root.transform, null, new Color(0f, 0f, 0f, 128f / 255f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var panel = BeginPanel("Panel", root.transform, style);
        AddLabel(panel, "Title", "Pause", 34f, 48f);
        AddButton(panel, "PauseContinue", DefaultBtnW, DefaultBtnH);
        AddButton(panel, "PauseExit", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);

        root.transform.SetAsLastSibling();
        root.SetActive(false);
        return root;
    }

    public static GameObject EnsureAuthPrompt(Transform roundFinish)
    {
        if (roundFinish == null)
            return null;

        Transform existing = roundFinish.Find("AuthPrompt");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(roundFinish.parent != null ? roundFinish.parent : roundFinish);
        var panel = BeginPanel("AuthPrompt", roundFinish, style);
        AddLabel(panel, "AuthText", "Sign in to save?", 24f, 90f);
        AddButton(panel, "LoginBtn", DefaultBtnW, DefaultBtnH);
        AddButton(panel, "SkipBtn", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        panel.Root.SetActive(false);
        return panel.Root;
    }

    public static GameObject EnsureRoundWin(Transform hud)
    {
        if (hud == null)
            return null;
        Transform existing = hud.Find("RoundWin");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(hud);
        var panel = BeginPanel("RoundWin", hud, style);
        AddLabel(panel, "Title", "Win", 36f, 48f);
        AddLabel(panel, "RoundDollars", "$0", 28f, 40f);
        AddLabel(panel, "RunDollars", "$0", 28f, 40f);
        AddLabel(panel, "FactTime", "0.00s", 24f, 36f);
        AddButton(panel, "Next", DefaultBtnW, DefaultBtnH);
        AddButton(panel, "Map", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        panel.Root.SetActive(false);
        return panel.Root;
    }

    public static GameObject EnsureRoundLose(Transform hud)
    {
        if (hud == null)
            return null;
        Transform existing = hud.Find("RoundLose");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(hud);
        var panel = BeginPanel("RoundLose", hud, style);
        AddLabel(panel, "Title", "Lose", 36f, 48f);
        AddLabel(panel, "Reason", "Reason", 24f, 44f);
        AddLabel(panel, "BurnedText", "Run burned", 22f, 40f);
        AddButton(panel, "Retry", DefaultBtnW, DefaultBtnH);
        AddButton(panel, "Map", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        panel.Root.SetActive(false);
        return panel.Root;
    }

    public static GameObject EnsureLevelComplete(Transform hud)
    {
        if (hud == null)
            return null;
        Transform existing = hud.Find("LevelComplete");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(hud);
        var panel = BeginPanel("LevelComplete", hud, style);
        AddLabel(panel, "CityName", "City", 30f, 44f);
        AddLabel(panel, "TotalDollars", "$0", 28f, 40f);
        AddLabel(panel, "Stars", "***", 28f, 40f);
        AddLabel(panel, "RecordLabel", "Best: $0", 22f, 36f);
        var nick = AddInput(panel, "InputNickname", 400f, 48f);
        nick.gameObject.SetActive(false);
        var submit = AddButton(panel, "SubmitBtn", DefaultBtnW, DefaultBtnH);
        submit.gameObject.SetActive(false);
        AddButtonRow(panel, DefaultBtnH,
            ("Map", DefaultBtnW * 0.7f),
            ("NextCity", DefaultBtnW * 0.7f));
        AddButton(panel, "Retry", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        panel.Root.SetActive(false);
        return panel.Root;
    }

    public static GameObject EnsureAreYouSureAbort(Transform hud)
    {
        if (hud == null)
            return null;
        Transform existing = hud.Find("AreYouSureAbort");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(hud);
        var panel = BeginPanel("AreYouSureAbort", hud, style);
        AddLabel(panel, "SureText", "Abort run?", 24f, 80f);
        AddButton(panel, "Yes", DefaultBtnW, DefaultBtnH);
        AddButton(panel, "No", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        panel.Root.SetActive(false);
        return panel.Root;
    }

    public static void EnsureRoundStartFields(Transform roundStart)
    {
        if (roundStart == null)
            return;

        Style style = CaptureStyle(roundStart.parent != null ? roundStart.parent : roundStart);
        EnsureLabel(roundStart, "CityName", "City", new Vector2(0f, 110f), new Vector2(400f, 36f), 26, style);
        EnsureLabel(roundStart, "RoundLabel", "1 / 5", new Vector2(0f, 70f), new Vector2(400f, 32f), 24, style);
        EnsureLabel(roundStart, "EnemyName", "Enemy", new Vector2(0f, 35f), new Vector2(400f, 32f), 24, style);
        EnsureLabel(roundStart, "RunDollars", "$0", new Vector2(0f, 0f), new Vector2(400f, 32f), 24, style);

        // Do not resize designer RoundStart panel; only add Map if missing.
        if (roundStart.Find("Map") == null)
            CreateMenuButton(roundStart, "Map", new Vector2(0f, -120f), style, new Vector2(DefaultBtnW, DefaultBtnH));
    }

    public static GameObject EnsureWorldMap(Transform parent)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find("WorldMap");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(parent);
        var root = CreateStretchRoot(parent, "WorldMap");
        var overlay = CreateImage("Overlay", root.transform, null, new Color(0f, 0f, 0f, 0.55f));
        Stretch(overlay.rectTransform);

        const float rowGap = 12f;
        const float listH = CityRowHeight * 3f + rowGap * 2f;
        float contentW = 860f;

        var panel = BeginPanel("Panel", root.transform, style, minWidth: contentW + PadX * 2f);
        AddLabel(panel, "Title", "Towns", 34f, 48f);

        var listGo = new GameObject("CityList", typeof(RectTransform));
        listGo.transform.SetParent(panel.Root.transform, false);
        var listRt = listGo.GetComponent<RectTransform>();
        ReserveBlock(panel, listH, contentW);

        string[] cities = { "Dry Creek", "Dead Well", "Raven Rock" };
        for (int i = 0; i < cities.Length; i++)
        {
            float y = (listH * 0.5f) - CityRowHeight * 0.5f - i * (CityRowHeight + rowGap);
            CreateCityRow(listGo.transform, $"CityRow_0{i + 1}", cities[i], new Vector2(0f, y), style, contentW, CityRowHeight);
        }

        int listSlotIndex = panel.Slots.Count - 1;
        panel.Deferred.Add(() =>
        {
            Slot slot = panel.Slots[listSlotIndex];
            Center(listRt, slot.Pos, new Vector2(contentW, listH));
        });

        // Richest block = value + button, sized so both fit.
        float richestH = 36f + Gap + DefaultBtnH;
        ReserveBlock(panel, richestH, DefaultBtnW + 40f);
        int richestSlotIndex = panel.Slots.Count - 1;
        var richestBlock = CreateCenterPanel("RichestBlock", panel.Root.transform, style, new Vector2(DefaultBtnW + 40f, richestH));
        CreateLabel(richestBlock.transform, "RichestValue", "$0", new Vector2(0f, (richestH * 0.5f) - 18f), new Vector2(DefaultBtnW, 36f), 22, style);
        CreateMenuButton(richestBlock.transform, "RichestBtn", new Vector2(0f, -(richestH * 0.5f) + DefaultBtnH * 0.5f), style, new Vector2(DefaultBtnW, DefaultBtnH));
        panel.Deferred.Add(() =>
        {
            richestBlock.GetComponent<RectTransform>().anchoredPosition = panel.Slots[richestSlotIndex].Pos;
        });

        AddButton(panel, "FastestBtn", DefaultBtnW, DefaultBtnH);
        AddButton(panel, "Back", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        root.SetActive(false);
        return root;
    }

    public static GameObject EnsureCityLeaderboard(Transform parent)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find("CityLeaderboard");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(parent);
        var root = CreateStretchRoot(parent, "CityLeaderboard");
        var overlay = CreateImage("Overlay", root.transform, null, new Color(0f, 0f, 0f, 0.55f));
        Stretch(overlay.rectTransform);

        var panel = BeginPanel("Panel", root.transform, style);
        AddLabel(panel, "Title", "City", 30f, 44f);
        AddLabel(panel, "BoardList", "(top list)", 20f, 280f);
        AddLabel(panel, "PlayerRow", "You: $0", 22f, 40f);
        AddButton(panel, "Close", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        root.SetActive(false);
        return root;
    }

    public static GameObject EnsureLeaderboardsOverlay(Transform parent)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find("LeaderboardsOverlay");
        if (existing != null)
            return existing.gameObject;

        Style style = CaptureStyle(parent);
        var root = CreateStretchRoot(parent, "LeaderboardsOverlay");
        var overlay = CreateImage("Overlay", root.transform, null, new Color(0f, 0f, 0f, 0.55f));
        Stretch(overlay.rectTransform);

        var panel = BeginPanel("Panel", root.transform, style);
        AddLabel(panel, "Title", "Leaderboards", 30f, 44f);
        AddButtonRow(panel, DefaultBtnH,
            ("TabRichest", DefaultBtnW * 0.85f),
            ("TabFastest", DefaultBtnW * 0.85f));
        ReserveBlock(panel, 300f, 560f);

        var richest = CreateCenterPanel("RichestPanel", panel.Root.transform, style, new Vector2(560f, 300f));
        CreateLabel(richest.transform, "BoardList", "(richest)", new Vector2(0f, 20f), new Vector2(520f, 220f), 20, style);
        CreateLabel(richest.transform, "PlayerRow", "You: $0", new Vector2(0f, -120f), new Vector2(520f, 32f), 22, style);

        var fastest = CreateCenterPanel("FastestHandPanel", panel.Root.transform, style, new Vector2(560f, 300f));
        CreateLabel(fastest.transform, "BoardList", "(fastest)", new Vector2(0f, 20f), new Vector2(520f, 220f), 20, style);
        CreateLabel(fastest.transform, "PlayerRow", "You: 0.00s", new Vector2(0f, -120f), new Vector2(520f, 32f), 22, style);
        fastest.SetActive(false);

        panel.Deferred.Add(() =>
        {
            Slot slot = panel.Slots[2];
            richest.GetComponent<RectTransform>().anchoredPosition = slot.Pos;
            fastest.GetComponent<RectTransform>().anchoredPosition = slot.Pos;
        });

        AddButton(panel, "Close", DefaultBtnW, DefaultBtnH);
        FinishPanel(panel);
        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// Editor-only bake: apply default UI art and fonts and fit generated panels.
    /// Do not call from runtime — layout must be visible and editable in Scene view.
    /// </summary>
    public static void RestyleGeneratedPanels(Transform root)
    {
        if (root == null)
            return;

        string[] generated =
        {
            "RoundWin", "RoundLose", "LevelComplete", "AreYouSureAbort",
            "WorldMap", "CityLeaderboard", "LeaderboardsOverlay"
        };

        Style style = CaptureStyle(root);
        for (int i = 0; i < generated.Length; i++)
        {
            Transform panel = FindDeep(root, generated[i]);
            if (panel == null)
                continue;
            RestyleTree(panel, style);
            // LevelComplete reserves separate slots for controls hidden until nickname entry.
            if (panel.name == "LevelComplete")
                continue;

            Transform inner = panel.Find("Panel") ?? panel;
            ReflowVerticalChildren(inner as RectTransform);
            FitPanelToChildren(inner as RectTransform, PadX, PadTop, PadBottom);
        }
    }

    // ---- Content-driven panel builder ----

    private struct Slot
    {
        public Vector2 Pos;
        public Vector2 Size;
    }

    private class PanelBuild
    {
        public GameObject Root;
        public Style Style;
        public float CursorY; // top-down: starts at 0, goes negative
        public float MaxContentW;
        public float MinWidth;
        public readonly List<Slot> Slots = new List<Slot>();
        public readonly List<System.Action> Deferred = new List<System.Action>();
    }

    private static PanelBuild BeginPanel(string name, Transform parent, Style style, float minWidth = 0f)
    {
        var go = CreateCenterPanel(name, parent, style, new Vector2(400f, 200f));
        return new PanelBuild
        {
            Root = go,
            Style = style,
            CursorY = 0f,
            MaxContentW = 0f,
            MinWidth = minWidth
        };
    }

    private static void ReserveBlock(PanelBuild panel, float height, float width)
    {
        AddSlot(panel, width, height, out _);
    }

    private static void AddSlot(PanelBuild panel, float width, float height, out Vector2 pos)
    {
        if (panel.Slots.Count > 0)
            panel.CursorY += Gap;
        pos = new Vector2(0f, -(panel.CursorY + height * 0.5f));
        panel.CursorY += height;
        panel.MaxContentW = Mathf.Max(panel.MaxContentW, width);
        panel.Slots.Add(new Slot { Pos = pos, Size = new Vector2(width, height) });
    }

    private static TMP_Text AddLabel(PanelBuild panel, string name, string content, float fontSize, float height)
    {
        float width = Mathf.Max(280f, panel.MinWidth > 0 ? panel.MinWidth - PadX * 2f : 480f);
        AddSlot(panel, width, height, out Vector2 pos);
        return CreateLabel(panel.Root.transform, name, content, pos, new Vector2(width, height), fontSize, panel.Style);
    }

    private static Button AddButton(PanelBuild panel, string name, float width, float height)
    {
        AddSlot(panel, width, height, out Vector2 pos);
        return CreateMenuButton(panel.Root.transform, name, pos, panel.Style, new Vector2(width, height));
    }

    private static void AddButtonRow(PanelBuild panel, float height, params (string name, float width)[] buttons)
    {
        float gap = 16f;
        float totalW = 0f;
        for (int i = 0; i < buttons.Length; i++)
            totalW += buttons[i].width + (i > 0 ? gap : 0f);

        AddSlot(panel, totalW, height, out Vector2 center);
        float x = -totalW * 0.5f;
        for (int i = 0; i < buttons.Length; i++)
        {
            float w = buttons[i].width;
            x += w * 0.5f;
            CreateMenuButton(panel.Root.transform, buttons[i].name, new Vector2(center.x + x, center.y), panel.Style, new Vector2(w, height));
            x += w * 0.5f + gap;
        }
    }

    private static TMP_InputField AddInput(PanelBuild panel, string name, float width, float height)
    {
        AddSlot(panel, width, height, out Vector2 pos);
        return CreateInput(panel.Root.transform, name, pos, new Vector2(width, height), panel.Style);
    }

    private static void FinishPanel(PanelBuild panel)
    {
        // Convert top-down CursorY into centered stack: shift all children up by half total height.
        float totalH = panel.CursorY;
        float shift = totalH * 0.5f;
        for (int i = 0; i < panel.Root.transform.childCount; i++)
        {
            var rt = panel.Root.transform.GetChild(i) as RectTransform;
            if (rt == null)
                continue;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + shift);
        }

        for (int i = 0; i < panel.Slots.Count; i++)
        {
            Slot s = panel.Slots[i];
            s.Pos = new Vector2(s.Pos.x, s.Pos.y + shift);
            panel.Slots[i] = s;
        }

        for (int i = 0; i < panel.Deferred.Count; i++)
            panel.Deferred[i]?.Invoke();

        float width = Mathf.Max(panel.MinWidth, panel.MaxContentW + PadX * 2f);
        float height = totalH + PadTop + PadBottom;
        var panelRt = panel.Root.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(width, height);
        panelRt.anchoredPosition = Vector2.zero;
    }

    private static void FitPanelToChildren(RectTransform panel, float padX, float padTop, float padBottom)
    {
        if (panel == null || panel.childCount == 0)
            return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        bool any = false;
        for (int i = 0; i < panel.childCount; i++)
        {
            var child = panel.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;
            // Skip full-stretch overlays
            if (Approximately(child.anchorMin, Vector2.zero) && Approximately(child.anchorMax, Vector2.one))
                continue;

            Vector2 pos = child.anchoredPosition;
            Vector2 size = child.sizeDelta;
            minX = Mathf.Min(minX, pos.x - size.x * 0.5f);
            maxX = Mathf.Max(maxX, pos.x + size.x * 0.5f);
            minY = Mathf.Min(minY, pos.y - size.y * 0.5f);
            maxY = Mathf.Max(maxY, pos.y + size.y * 0.5f);
            any = true;
        }

        if (!any)
            return;

        float needW = (maxX - minX) + padX * 2f;
        float needH = (maxY - minY) + padTop + padBottom;
        panel.sizeDelta = new Vector2(Mathf.Max(panel.sizeDelta.x, needW), Mathf.Max(panel.sizeDelta.y, needH));
    }

    private static void ReflowVerticalChildren(RectTransform panel)
    {
        if (panel == null)
            return;

        var items = new List<RectTransform>();
        for (int i = 0; i < panel.childCount; i++)
        {
            var child = panel.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;
            if (Approximately(child.anchorMin, Vector2.zero) && Approximately(child.anchorMax, Vector2.one))
                continue;
            // Keep nested panels (RichestBlock etc.) in flow by Y order
            items.Add(child);
        }

        if (items.Count == 0)
            return;

        items.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));

        float total = 0f;
        for (int i = 0; i < items.Count; i++)
            total += items[i].sizeDelta.y + (i > 0 ? Gap : 0f);

        float y = total * 0.5f;
        for (int i = 0; i < items.Count; i++)
        {
            float h = items[i].sizeDelta.y;
            y -= h * 0.5f;
            items[i].anchoredPosition = new Vector2(0f, y);
            y -= h * 0.5f + Gap;
        }
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
    }

    // ---- primitives ----

    private struct Style
    {
        public Sprite PanelSprite;
        public Sprite ButtonSprite;
        public TMP_FontAsset Font;
    }

    public static bool ApplyTextAutoSize(TMP_Text text, float minSize = 12f, float maxSize = 48f)
    {
        if (text.enableAutoSizing && text.fontSizeMin == minSize && text.fontSizeMax == maxSize)
            return false;

        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        return true;
    }

    private static Style CaptureStyle(Transform root)
    {
        var style = new Style();
        var skin = MenuUiSkin.Load();
        if (skin != null)
        {
            style.ButtonSprite = skin.Button;
            style.PanelSprite = skin.Panel;
        }

        if (root == null)
            return style;

        Transform finish = root.Find("RoundFinish")
                           ?? root.Find("RoundWin")
                           ?? root.Find("RoundStartPanel")
                           ?? root.Find("RoundStart");
        if (finish == null && root.childCount > 0)
            finish = root;

        if (finish != null)
        {
            var label = finish.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                style.Font = label.font;
        }

        if (style.Font == null)
        {
            var any = root.GetComponentInChildren<TMP_Text>(true);
            if (any != null)
                style.Font = any.font;
        }

        return style;
    }

    private static GameObject CreateStretchRoot(Transform parent, string name)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        root.transform.SetAsLastSibling();
        return root;
    }

    private static GameObject CreateCenterPanel(string name, Transform parent, Style style, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = style.PanelSprite;
        image.type = style.PanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        var rt = go.GetComponent<RectTransform>();
        Center(rt, Vector2.zero, size);
        return go;
    }

    private static void CreateCityRow(Transform parent, string name, string city, Vector2 pos, Style style, float width, float height)
    {
        var row = CreateCenterPanel(name, parent, style, new Vector2(width, height));
        row.GetComponent<RectTransform>().anchoredPosition = pos;

        float icon = Mathf.Min(CityIconBtnSize, height - 24f);
        float right = width * 0.5f - 20f;
        CreateLabel(row.transform, "Name", city, new Vector2(-width * 0.22f, 0f), new Vector2(width * 0.42f, 40f), 22, style);
        CreateLabel(row.transform, "Stars", "0", new Vector2(40f, 0f), new Vector2(80f, 40f), 22, style);
        CreateLabel(row.transform, "PlayerBest", "$0", new Vector2(160f, 0f), new Vector2(140f, 40f), 22, style);
        // Square icon buttons, no captions — pictograms go here later.
        CreateMenuButton(row.transform, "ServerBestBtn", new Vector2(right - icon * 1.5f - 12f, 0f), style, new Vector2(icon, icon), withLabel: false);
        CreateMenuButton(row.transform, "Play", new Vector2(right - icon * 0.5f, 0f), style, new Vector2(icon, icon), withLabel: false);
    }

    private static void EnsureLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Style style)
    {
        if (parent.Find(name) != null)
            return;
        CreateLabel(parent, name, text, pos, size, fontSize, style);
    }

    private static TMP_Text CreateLabel(Transform parent, string name, string content, Vector2 pos, Vector2 size, float fontSize, Style style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        Center(rt, pos, size);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(16f, fontSize);
        text.fontSizeMax = fontSize;
        text.color = Color.black;
        text.raycastTarget = false;
        if (style.Font != null)
        {
            text.font = style.Font;
            text.fontSharedMaterial = style.Font.material;
        }
        return text;
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

    private static Button CreateMenuButton(Transform parent, string name, Vector2 pos, Style style, Vector2? size = null, bool withLabel = true)
    {
        Vector2 buttonSize = size ?? new Vector2(DefaultBtnW, DefaultBtnH);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        Center(rt, pos, buttonSize);

        Sprite buttonSprite = style.ButtonSprite;
        var image = go.GetComponent<Image>();
        image.sprite = buttonSprite;
        image.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        if (withLabel)
        {
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>(), 10f);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = name;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 26;
            ApplyTextAutoSize(text);
            text.color = Color.black;
            text.raycastTarget = false;
            if (style.Font != null)
            {
                text.font = style.Font;
                text.fontSharedMaterial = style.Font.material;
            }
        }

        return button;
    }

    private static void RestyleTree(Transform t, Style style)
    {
        var button = t.GetComponent<Button>();
        bool isButton = button != null;
        bool inCityRow = IsUnderCityRow(t);
        Sprite buttonSprite = style.ButtonSprite;

        var image = t.GetComponent<Image>();
        if (image != null)
        {
            bool overlay = t.name == "Overlay";
            if (!overlay)
            {
                if (isButton && buttonSprite != null && MenuUiSkin.NeedsDefaultArt(image.sprite))
                {
                    image.sprite = buttonSprite;
                    image.type = Image.Type.Sliced;
                }
                else if (style.PanelSprite != null && (image.sprite == null || image.sprite.name.Contains("UISprite") || image.sprite.name.Contains("Background")))
                {
                    image.sprite = style.PanelSprite;
                    image.type = Image.Type.Sliced;
                }

                image.color = Color.white;
            }
        }

        if (isButton && inCityRow)
        {
            var rt = t as RectTransform;
            if (rt != null)
            {
                float s = CityIconBtnSize;
                rt.sizeDelta = new Vector2(s, s);
            }

            // Icon buttons: remove text labels.
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Transform child = t.GetChild(i);
                if (child.GetComponent<TMP_Text>() != null || child.name == "Label")
                {
                    if (Application.isPlaying)
                        Object.Destroy(child.gameObject);
                    else
                        Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        // City rows keep fixed height 120.
        if (t.name.StartsWith("CityRow_"))
        {
            var rt = t as RectTransform;
            if (rt != null)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, CityRowHeight);
        }

        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null && style.Font != null)
        {
            tmp.font = style.Font;
            tmp.fontSharedMaterial = style.Font.material;
        }

        for (int i = 0; i < t.childCount; i++)
            RestyleTree(t.GetChild(i), style);
    }

    private static bool IsUnderCityRow(Transform t)
    {
        Transform p = t;
        while (p != null)
        {
            if (p.name.StartsWith("CityRow_"))
                return true;
            p = p.parent;
        }
        return false;
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

    private static TMP_InputField CreateInput(Transform parent, string name, Vector2 pos, Vector2 size, Style style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        Center(rt, pos, size);
        var image = go.GetComponent<Image>();
        image.sprite = style.PanelSprite;
        image.type = style.PanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), 8f);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 22;
        text.color = Color.black;
        text.raycastTarget = false;
        if (style.Font != null)
        {
            text.font = style.Font;
            text.fontSharedMaterial = style.Font.material;
        }

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        placeholderGo.transform.SetParent(go.transform, false);
        Stretch(placeholderGo.GetComponent<RectTransform>(), 8f);
        var placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
        placeholder.text = "Nickname";
        placeholder.fontSize = 22;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.3f, 0.3f, 0.3f, 0.75f);
        placeholder.raycastTarget = false;
        if (style.Font != null)
        {
            placeholder.font = style.Font;
            placeholder.fontSharedMaterial = style.Font.material;
        }

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = rt;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }
}
