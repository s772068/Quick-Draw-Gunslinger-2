using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Main-menu leaderboard inside LeaderboardsPanel.
/// Sure / Fastest / Steady icons switch the board. Layout matches the Yandex card:
/// top 3, a wave, then the player's neighborhood.
/// </summary>
[ExecuteAlways]
public class MenuLeaderboardPanel : MonoBehaviour
{
    private LeaderboardKind _kind = LeaderboardKind.Sure;
    private RectTransform _list;
    private TMP_Text _title;
    private TMP_Text _localBest;
    private TMP_Text _empty;
    private Image _sureIcon;
    private Image _fastIcon;
    private Image _steadyIcon;
    private Sprite _rowSprite;
    private TMP_FontAsset _font;
    private readonly List<GameObject> _liveRows = new List<GameObject>();

    private void OnEnable()
    {
        LocalizationTables.LanguageChanged += OnLanguage;
        EnsureScaffold();
        ApplySelectionVisual();
        if (Application.isPlaying)
            StartCoroutine(RefreshWhenReady());
        else
            ShowSamples(true);
    }

    private void OnDisable()
    {
        LocalizationTables.LanguageChanged -= OnLanguage;
    }

    private void OnLanguage()
    {
        ApplyStaticTexts();
        ApplySelectionVisual();
        if (Application.isPlaying)
            StartCoroutine(RefreshWhenReady());
    }

    private IEnumerator RefreshWhenReady()
    {
        ShowSamples(false);
        float wait = 0f;
        while (!LocalizationTables.IsReady && wait < 2f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyStaticTexts();
        LoadSelected();
    }

    public void EnsureScaffold()
    {
        CacheFonts();
        _title = FindLabel("BoardLabel");

        Transform existing = transform.Find("BoardList");
        if (existing == null)
        {
            var go = new GameObject("BoardList", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(24f, 24f);
            rt.offsetMax = new Vector2(-24f, -300f);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            existing = go.transform;
        }

        _list = existing as RectTransform;
        EnsureChrome();
        WireIcons();
        if (!Application.isPlaying && _list.Find("SampleRow1") == null)
            BuildSamples();
    }

    private void EnsureChrome()
    {
        if (_list.Find("LocalBest") == null)
        {
            TMP_Text best = CreateText(_list, "LocalBest", 22, TextAlignmentOptions.Center, FontStyles.Italic);
            var le = best.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;
            best.transform.SetAsLastSibling();
        }

        _localBest = _list.Find("LocalBest").GetComponent<TMP_Text>();

        if (_list.Find("EmptyLabel") == null)
        {
            TMP_Text empty = CreateText(_list, "EmptyLabel", 24, TextAlignmentOptions.Center, FontStyles.Normal);
            var le = empty.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 40f;
        }

        _empty = _list.Find("EmptyLabel").GetComponent<TMP_Text>();
        _empty.gameObject.SetActive(false);
        ApplyStaticTexts();
    }

    private void BuildSamples()
    {
        AddSample("SampleRow1", "1", "Вячеслав", "96", false);
        AddSample("SampleRow2", "2", "Нина Сергеева", "92", false);
        AddSample("SampleRow3", "3", "Инкогнито 0094", "92", false);
        AddSample("SampleWave", "", "~~~~~~~~~~~~", "", true);
        AddSample("SampleRow4", "40335", "Дима Б.", "4", false);
        AddSample("SampleRow5", "40336", "Мария Лаврова", "4", false);
        AddSample("SampleRow6", "40337", "Денис Ракша", "4", false);
        if (_localBest != null)
            _localBest.transform.SetAsLastSibling();
    }

    private void AddSample(string name, string rank, string player, string score, bool wave)
    {
        CreateRow(_list, name, rank, player, score, null, wave);
    }

    private void WireIcons()
    {
        _sureIcon = WireIcon("SureIcon", LeaderboardKind.Sure);
        _fastIcon = WireIcon("FastestIcon", LeaderboardKind.Fastest);
        _steadyIcon = WireIcon("SteadyIcon", LeaderboardKind.Steady);
    }

    private Image WireIcon(string objectName, LeaderboardKind kind)
    {
        Transform t = transform.Find(objectName);
        if (t == null)
            return null;

        var image = t.GetComponent<Image>();
        var button = t.GetComponent<Button>();
        if (button == null && image != null)
        {
            button = t.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
        }

        if (button != null && Application.isPlaying)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Select(kind));
        }

        return image;
    }

    private void Select(LeaderboardKind kind)
    {
        _kind = kind;
        ApplySelectionVisual();
        if (Application.isPlaying)
            LoadSelected();
    }

    private void ApplySelectionVisual()
    {
        Tint(_sureIcon, _kind == LeaderboardKind.Sure);
        Tint(_fastIcon, _kind == LeaderboardKind.Fastest);
        Tint(_steadyIcon, _kind == LeaderboardKind.Steady);

        if (_title == null)
            _title = FindLabel("BoardLabel");
        if (_title != null)
            _title.text = Title(_kind);
    }

    private static void Tint(Image image, bool selected)
    {
        if (image == null)
            return;
        image.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.4f);
    }

    private void LoadSelected()
    {
        ApplyStaticTexts();
        ClearLiveRows();
        ShowSamples(false);
        if (_empty != null)
        {
            _empty.gameObject.SetActive(true);
            _empty.text = LocalizationTables.Get(LocalizationTables.Keys.LeaderboardEmpty);
        }

        if (YandexLeaderboards.Instance == null)
            return;

        YandexLeaderboards.Instance.Load(_kind, OnPage);
    }

    private void OnPage(YandexLeaderboards.BoardPage page)
    {
        ClearLiveRows();
        if (!page.Ok || page.Entries == null || page.Entries.Length == 0)
        {
            if (_empty != null)
                _empty.gameObject.SetActive(true);
            return;
        }

        if (_empty != null)
            _empty.gameObject.SetActive(false);

        var top = new List<YandexLeaderboards.Entry>();
        var around = new List<YandexLeaderboards.Entry>();
        var seen = new HashSet<int>();

        for (int i = 0; i < page.Entries.Length; i++)
        {
            YandexLeaderboards.Entry entry = page.Entries[i];
            int rank = entry.Rank <= 0 ? i + 1 : entry.Rank;
            if (!seen.Add(rank))
                continue;

            var normalized = new YandexLeaderboards.Entry(rank, entry.Name, entry.Score, entry.AvatarUrl);
            if (rank <= 3)
                top.Add(normalized);
            else
                around.Add(normalized);
        }

        top.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        around.Sort((a, b) => a.Rank.CompareTo(b.Rank));

        for (int i = 0; i < top.Count; i++)
            Spawn(top[i], false);

        if (around.Count > 0)
            SpawnWave();

        for (int i = 0; i < around.Count; i++)
            Spawn(around[i], false);

        if (_localBest != null)
            _localBest.transform.SetAsLastSibling();
    }

    private void Spawn(YandexLeaderboards.Entry entry, bool wave)
    {
        string score = FormatScore(entry.Score);
        GameObject row = CreateRow(_list, "LiveRow", entry.Rank.ToString(), entry.Name, score, entry.AvatarUrl, wave);
        _liveRows.Add(row);
        if (!string.IsNullOrEmpty(entry.AvatarUrl))
            StartCoroutine(LoadAvatar(row, entry.AvatarUrl));
    }

    private void SpawnWave()
    {
        GameObject row = CreateRow(_list, "LiveRow", "", "~~~~~~~~~~~~", "", null, true);
        _liveRows.Add(row);
    }

    private string FormatScore(int score)
    {
        if (!YandexLeaderboards.IsTimeBoard(_kind))
            return score.ToString();

        return SeriesRun.FormatSeconds(score / 1000f);
    }

    private void ClearLiveRows()
    {
        for (int i = 0; i < _liveRows.Count; i++)
        {
            if (_liveRows[i] != null)
                Destroy(_liveRows[i]);
        }

        _liveRows.Clear();
    }

    private void ShowSamples(bool show)
    {
        if (_list == null)
            return;

        for (int i = 0; i < _list.childCount; i++)
        {
            Transform child = _list.GetChild(i);
            if (child.name.StartsWith("Sample"))
                child.gameObject.SetActive(show);
        }
    }

    private void ApplyStaticTexts()
    {
        if (_localBest != null)
        {
            string value = PersonalRecords.Format(_kind);
            _localBest.text = string.Format(LocalizationTables.Get(LocalizationTables.Keys.YourRecord), value);
        }

        if (_title != null)
            _title.text = Title(_kind);
    }

    private static string Title(LeaderboardKind kind)
    {
        switch (kind)
        {
            case LeaderboardKind.Fastest: return LocalizationTables.Get(LocalizationTables.Keys.FastestHand);
            case LeaderboardKind.Steady: return LocalizationTables.Get(LocalizationTables.Keys.SteadyHand);
            default: return LocalizationTables.Get(LocalizationTables.Keys.SureHand);
        }
    }

    private TMP_Text FindLabel(string objectName)
    {
        Transform t = transform.Find(objectName);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private void CacheFonts()
    {
        if (_font == null)
        {
            TMP_Text any = GetComponentInChildren<TMP_Text>(true);
            if (any != null)
                _font = any.font;
        }

        if (_rowSprite == null)
        {
            var image = GetComponent<Image>();
            if (image != null)
                _rowSprite = image.sprite;
        }
    }

    private GameObject CreateRow(Transform parent, string name, string rank, string player, string score, string avatarUrl, bool wave)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = _rowSprite;
        image.type = Image.Type.Sliced;
        image.color = wave ? new Color(1f, 1f, 1f, 0f) : new Color(0f, 0f, 0f, 0.45f);
        var layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = wave ? 28f : 52f;

        var row = go.GetComponent<RectTransform>();
        if (!wave)
        {
            var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            avatarGo.transform.SetParent(row, false);
            var art = avatarGo.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0.5f);
            art.anchorMax = new Vector2(0f, 0.5f);
            art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(36f, 36f);
            art.anchoredPosition = new Vector2(56f, 0f);
            var avatar = avatarGo.GetComponent<Image>();
            avatar.sprite = _rowSprite;
            avatar.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            if (!string.IsNullOrEmpty(avatarUrl))
                avatar.color = Color.white;
        }

        TMP_Text rankText = CreateText(row, "Rank", 22, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Stretch(rankText.rectTransform, 8f, 52f);
        rankText.text = rank;

        TMP_Text nameText = CreateText(row, "Name", 22, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        Stretch(nameText.rectTransform, wave ? 8f : 100f, 90f);
        nameText.text = string.IsNullOrEmpty(player) ? LocalizationTables.Get(LocalizationTables.Keys.LeaderboardEmpty) : player;
        if (wave)
            nameText.alignment = TextAlignmentOptions.Center;

        TMP_Text scoreText = CreateText(row, "Score", 22, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Stretch(scoreText.rectTransform, 0f, 12f);
        scoreText.rectTransform.offsetMin = new Vector2(scoreText.rectTransform.offsetMin.x, 0f);
        var scoreRt = scoreText.rectTransform;
        scoreRt.anchorMin = new Vector2(1f, 0f);
        scoreRt.anchorMax = new Vector2(1f, 1f);
        scoreRt.pivot = new Vector2(1f, 0.5f);
        scoreRt.sizeDelta = new Vector2(80f, 0f);
        scoreRt.anchoredPosition = new Vector2(-12f, 0f);
        scoreText.text = score;

        return go;
    }

    private TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        if (_font != null)
        {
            text.font = _font;
            text.fontSharedMaterial = _font.material;
        }

        return text;
    }

    private static void Stretch(RectTransform rt, float left, float right)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
    }

    private IEnumerator LoadAvatar(GameObject row, string url)
    {
        if (row == null || string.IsNullOrEmpty(url))
            yield break;

        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (row == null || request.result != UnityWebRequest.Result.Success)
                yield break;

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
                yield break;

            Transform avatar = row.transform.Find("Avatar");
            if (avatar == null)
                yield break;

            var image = avatar.GetComponent<Image>();
            if (image == null)
                yield break;

            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            image.color = Color.white;
        }
    }
}
