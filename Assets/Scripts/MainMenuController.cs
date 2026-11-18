using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GDD 1.0 MainMenu: Begin → Towns hub; boards via LeaderboardsOverlay.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public const string SelectedCityPrefsKey = "QDG2_SelectedCity";

    [SerializeField] private Button startButton;
    [SerializeField] private Button trainingButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button leaderboardsButton;
    [SerializeField] private Image soundImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text beginLabelTmp;
    [SerializeField] private GameObject worldMapRoot;
    [SerializeField] private GameObject cityLeaderboardRoot;
    [SerializeField] private GameObject leaderboardsOverlayRoot;

    private void Awake()
    {
        GameAudioSettings.ApplyListenerVolume();
        if (!LeaderboardCache.IsReady)
            SetMenuPresented(false);

        AutoBindIfNeeded();
        ApplyLocalizedTexts();

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnBeginClicked);
        }

        if (trainingButton != null)
        {
            trainingButton.onClick.RemoveAllListeners();
            trainingButton.onClick.AddListener(OnTrainingClicked);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveAllListeners();
            soundButton.onClick.AddListener(OnSoundClicked);
        }

        if (leaderboardsButton != null)
        {
            leaderboardsButton.onClick.RemoveAllListeners();
            leaderboardsButton.onClick.AddListener(() => ShowLeaderboardsOverlay(richest: true));
        }

        WireWorldMap();
        WireOverlays();

        if (soundImage == null && soundButton != null)
            soundImage = soundButton.targetGraphic as Image;

        RefreshSoundVisual();
        EnsureSafeAreaPadding();
        SetPanel(worldMapRoot, false);
        SetPanel(cityLeaderboardRoot, false);
        SetPanel(leaderboardsOverlayRoot, false);
        RefreshWorldMapProgress();
        YandexGamesSdk.GameplayStop();
    }

    private static void EnsureSafeAreaPadding()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform root = canvas.transform.Find("HUD");
        if (root == null)
            root = canvas.transform.Find("SafeArea");
        if (root == null)
            root = canvas.transform;

        var rt = root as RectTransform;
        if (rt == null)
            return;

        if (root.GetComponent<SafeAreaPadding>() == null)
            root.gameObject.AddComponent<SafeAreaPadding>();
    }

    private void OnEnable()
    {
        LocalizationTables.LanguageChanged += ApplyLocalizedTexts;
        LeaderboardCache.Ready += OnLeaderboardsReady;
        if (LeaderboardCache.IsReady)
            SetMenuPresented(true);
    }

    private void OnDisable()
    {
        LocalizationTables.LanguageChanged -= ApplyLocalizedTexts;
        LeaderboardCache.Ready -= OnLeaderboardsReady;
    }

    private void OnLeaderboardsReady()
    {
        SetMenuPresented(true);
    }

    private void SetMenuPresented(bool presented)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        var group = canvas.GetComponent<CanvasGroup>();
        if (group == null)
            group = canvas.gameObject.AddComponent<CanvasGroup>();

        group.alpha = presented ? 1f : 0f;
        group.interactable = presented;
        group.blocksRaycasts = presented;
    }

    private void AutoBindIfNeeded()
    {
        if (startButton == null)
        {
            var begin = GameObject.Find("Begin");
            if (begin == null)
                begin = GameObject.Find("Start");
            if (begin != null)
                startButton = begin.GetComponent<Button>();
        }

        if (soundButton == null)
        {
            var sound = GameObject.Find("Sound");
            if (sound != null)
                soundButton = sound.GetComponent<Button>();
        }

        if (leaderboardsButton == null)
        {
            var boards = GameObject.Find("Leaderboards");
            if (boards == null)
                boards = GameObject.Find("LeaderboardsBtn");
            if (boards != null)
                leaderboardsButton = boards.GetComponent<Button>();
        }

        if (titleText == null)
        {
            var title = GameObject.Find("Title");
            if (title != null)
                titleText = title.GetComponent<TMP_Text>();
        }

        if (beginLabelTmp == null && startButton != null)
            beginLabelTmp = startButton.GetComponentInChildren<TMP_Text>(true);

        if (trainingButton == null)
        {
            var training = GameObject.Find("Training");
            if (training != null)
                trainingButton = training.GetComponent<Button>();
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform parent = canvas.transform.Find("HUD");
        if (parent == null)
            parent = canvas.transform.Find("SafeArea");
        if (parent == null)
            parent = canvas.transform;

        if (trainingButton == null)
        {
            Transform t = parent.Find("Training");
            if (t != null)
                trainingButton = t.GetComponent<Button>();
        }

        if (worldMapRoot == null)
        {
            Transform t = parent.Find("WorldMap");
            if (t != null)
                worldMapRoot = t.gameObject;
        }

        if (cityLeaderboardRoot == null)
        {
            Transform t = parent.Find("CityLeaderboard");
            if (t != null)
                cityLeaderboardRoot = t.gameObject;
        }

        if (leaderboardsOverlayRoot == null)
        {
            Transform t = parent.Find("LeaderboardsOverlay");
            if (t != null)
                leaderboardsOverlayRoot = t.gameObject;
        }
    }

    private void WireWorldMap()
    {
        if (worldMapRoot == null)
            return;

        WireButton(worldMapRoot.transform, "Panel/Back", HideWorldMap);
        WireButton(worldMapRoot.transform, "Panel/FastestBtn", () => ShowLeaderboardsOverlay(richest: false));
        WireButton(worldMapRoot.transform, "Panel/RichestBlock/RichestBtn", () => ShowLeaderboardsOverlay(richest: true));

        for (int i = 1; i <= 3; i++)
        {
            int cityIndex = i - 1;
            string row = $"Panel/CityList/CityRow_0{i}";
            WireButton(worldMapRoot.transform, row + "/Play", () => StartCity(cityIndex));
            WireButton(worldMapRoot.transform, row + "/ServerBestBtn", () => ShowCityLeaderboard(cityIndex));
        }
    }

    private void WireOverlays()
    {
        if (cityLeaderboardRoot != null)
            WireButton(cityLeaderboardRoot.transform, "Panel/Close", () => SetPanel(cityLeaderboardRoot, false));

        if (leaderboardsOverlayRoot != null)
        {
            WireButton(leaderboardsOverlayRoot.transform, "Panel/Close", () => SetPanel(leaderboardsOverlayRoot, false));
            WireButton(leaderboardsOverlayRoot.transform, "Panel/TabRichest", () => ShowLeaderboardsOverlay(richest: true));
            WireButton(leaderboardsOverlayRoot.transform, "Panel/TabFastest", () => ShowLeaderboardsOverlay(richest: false));
        }
    }

    private static void WireButton(Transform root, string path, UnityEngine.Events.UnityAction action)
    {
        Transform t = root.Find(path);
        if (t == null)
            return;
        var button = t.GetComponent<Button>();
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ApplyLocalizedTexts()
    {
        SceneTextLocalizer.Apply();

        if (titleText != null)
            titleText.text = LocalizationTables.Get(LocalizationTables.Keys.GameTitle);

        if (beginLabelTmp != null)
            beginLabelTmp.text = LocalizationTables.Get(LocalizationTables.Keys.MenuBegin);

        if (trainingButton != null)
            SceneTextLocalizer.SetButton(trainingButton, LocalizationTables.Keys.MenuTraining);

        ApplyTownsTitle();
    }

    private void ApplyTownsTitle()
    {
        if (worldMapRoot == null)
            return;

        Transform title = worldMapRoot.transform.Find("Panel/Title");
        if (title == null)
            return;

        var text = title.GetComponent<TMP_Text>();
        if (text != null)
            text.text = LocalizationTables.Get(LocalizationTables.Keys.Towns);
    }

    private void OnBeginClicked()
    {
        if (TrainingSession.IsFirstLaunch)
        {
            StartTraining(silentModals: false);
            return;
        }

        if (worldMapRoot != null)
        {
            RefreshWorldMapProgress();
            SetPanel(worldMapRoot, true);
            worldMapRoot.transform.SetAsLastSibling();
            return;
        }

        PlatformAds.ShowInterstitial();
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnTrainingClicked()
    {
        StartTraining(silentModals: false);
    }

    private void StartTraining(bool silentModals)
    {
        TrainingSession.RequestStart(silentModals);
        SceneManager.LoadScene(gameSceneName);
    }

    private void RefreshWorldMapProgress()
    {
        if (worldMapRoot == null)
            return;

        string[] names = { "Dry Creek", "Dead Well", "Raven Rock" };
        RefreshNetWorthStars();

        for (int i = 1; i <= 3; i++)
        {
            int city = i - 1;
            Transform row = worldMapRoot.transform.Find($"Panel/CityList/CityRow_0{i}");
            if (row == null)
                continue;

            bool unlocked = CityProgress.IsUnlocked(city);
            SetTmp(row, "Name", names[city]);
            SetTmp(row, "Stars", CityProgress.GetStars(city).ToString());
            SetTmp(row, "PlayerBest", "$" + CityProgress.GetBestDollars(city));

            var play = row.Find("Play");
            if (play != null)
            {
                var btn = play.GetComponent<Button>();
                if (btn != null)
                    btn.interactable = unlocked;
            }

            var canvasGroup = row.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = row.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = unlocked ? 1f : 0.45f;
        }

        Transform richest = worldMapRoot.transform.Find("Panel/RichestBlock/RichestValue");
        if (richest != null)
        {
            var text = richest.GetComponent<TMP_Text>();
            if (text != null)
                text.text = "$" + CityProgress.RichestSum();
        }
    }

    private static void SetTmp(Transform row, string child, string value)
    {
        Transform t = row.Find(child);
        if (t == null)
            return;
        var text = t.GetComponent<TMP_Text>();
        if (text != null)
            text.text = value;
    }

    private void HideWorldMap()
    {
        SetPanel(worldMapRoot, false);
        SetPanel(cityLeaderboardRoot, false);
        SetPanel(leaderboardsOverlayRoot, false);
    }

    private void ShowCityLeaderboard(int cityIndex)
    {
        if (cityLeaderboardRoot == null)
            return;

        Transform title = cityLeaderboardRoot.transform.Find("Panel/Title");
        if (title != null)
        {
            var text = title.GetComponent<TMP_Text>();
            if (text != null)
                text.text = CityName(cityIndex);
        }

        Transform list = cityLeaderboardRoot.transform.Find("Panel/BoardList");

        Transform player = cityLeaderboardRoot.transform.Find("Panel/PlayerRow");
        if (player != null)
        {
            var text = player.GetComponent<TMP_Text>();
            if (text != null)
                text.text = "You: $" + CityProgress.GetBestDollars(cityIndex);
        }

        if (list != null)
        {
            var text = list.GetComponent<TMP_Text>();
            if (text != null)
                text.text = FormatCachedBoard(LevelSystem.CityBoardId(cityIndex), money: true);
        }

        SetPanel(cityLeaderboardRoot, true);
        cityLeaderboardRoot.transform.SetAsLastSibling();
    }

    private void ShowLeaderboardsOverlay(bool richest)
    {
        if (leaderboardsOverlayRoot == null)
            return;

        Transform richestPanel = leaderboardsOverlayRoot.transform.Find("Panel/RichestPanel");
        Transform fastestPanel = leaderboardsOverlayRoot.transform.Find("Panel/FastestHandPanel");
        if (richestPanel != null)
            richestPanel.gameObject.SetActive(richest);
        if (fastestPanel != null)
            fastestPanel.gameObject.SetActive(!richest);

        FillCachedBoard(richestPanel, HttpLeaderboards.RichestBoard, money: true);
        FillCachedBoard(fastestPanel, HttpLeaderboards.FastestBoard, money: false);

        SetPanel(leaderboardsOverlayRoot, true);
        leaderboardsOverlayRoot.transform.SetAsLastSibling();
    }

    private static void FillCachedBoard(Transform panel, string board, bool money)
    {
        if (panel == null)
            return;

        Transform list = panel.Find("BoardList");
        if (list != null)
        {
            var text = list.GetComponent<TMP_Text>();
            if (text != null)
                text.text = FormatCachedBoard(board, money);
        }

        Transform player = panel.Find("PlayerRow");
        if (player == null)
            return;

        var playerText = player.GetComponent<TMP_Text>();
        if (playerText == null)
            return;

        if (money)
        {
            playerText.text = "You: $" + CityProgress.RichestSum();
            return;
        }

        int ms = CityProgress.GetFastestMs();
        playerText.text = ms < 0
            ? "You: —"
            : "You: " + SeriesRun.FormatSeconds(ms / 1000f) + "s";
    }

    private static string FormatCachedBoard(string board, bool money)
    {
        if (!LeaderboardCache.TryGet(board, out YandexLeaderboards.BoardPage page)
            || !page.Ok
            || page.Entries == null
            || page.Entries.Length == 0)
            return "0";

        var sb = new System.Text.StringBuilder();
        int n = Mathf.Min(10, page.Entries.Length);
        for (int i = 0; i < n; i++)
        {
            YandexLeaderboards.Entry entry = page.Entries[i];
            sb.Append(entry.Rank).Append(". ").Append(entry.Name).Append(' ');
            if (money)
                sb.Append('$').Append(entry.Score);
            else
                sb.Append(SeriesRun.FormatSeconds(entry.Score / 1000f)).Append('s');
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private void StartCity(int cityIndex)
    {
        PlayerPrefs.SetInt(SelectedCityPrefsKey, cityIndex);
        PlayerPrefs.Save();
        PlatformAds.ShowInterstitial();
        SceneManager.LoadScene(gameSceneName);
    }

    private void RefreshNetWorthStars()
    {
        if (worldMapRoot == null)
            return;

        Transform stars = worldMapRoot.transform.Find("Panel/RichestBlock/Stars")
                         ?? worldMapRoot.transform.Find("Panel/RichestBlock/NetWorthStars");
        if (stars == null)
            return;

        int count = StarsNetWorth.Instance != null
            ? StarsNetWorth.Instance.StarsForTotal(CityProgress.RichestSum())
            : StarThresholds.Count(CityProgress.RichestSum(), 100, 200, 300);
        string value = FormatStars(count);
        var text = stars.GetComponent<TMP_Text>();
        if (text != null)
            text.text = value;
        else
            SetTmp(stars.parent, stars.name, value);
    }

    private static string FormatStars(int stars)
    {
        stars = Mathf.Clamp(stars, 0, 3);
        return new string('*', stars).PadRight(3, '-');
    }

    private static string CityName(int index)
    {
        switch (index)
        {
            case 1: return "Dead Well";
            case 2: return "Raven Rock";
            default: return "Dry Creek";
        }
    }

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private void OnSoundClicked()
    {
        GameAudioSettings.ToggleSound();
        RefreshSoundVisual();
    }

    private void RefreshSoundVisual()
    {
        if (soundImage == null)
            return;

        bool on = GameAudioSettings.SoundEnabled;
        Sprite sprite = on ? soundOnSprite : soundOffSprite;
        if (sprite != null)
            soundImage.sprite = sprite;
    }
}
