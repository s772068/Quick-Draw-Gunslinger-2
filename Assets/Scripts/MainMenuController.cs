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
        PlatformGameplay.Stop();
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
        LeaderboardCache.BoardChanged += OnLeaderboardChanged;
        if (LeaderboardCache.IsReady)
            SetMenuPresented(true);
    }

    private void OnDisable()
    {
        LocalizationTables.LanguageChanged -= ApplyLocalizedTexts;
        LeaderboardCache.Ready -= OnLeaderboardsReady;
        LeaderboardCache.BoardChanged -= OnLeaderboardChanged;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!YandexGamesSdk.SdkAvailable)
            GameAudioSettings.SetPlatformMuted(!hasFocus);
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

    private Transform WorldMapPanel => worldMapRoot == null
        ? null
        : worldMapRoot.transform.Find("CitiesPanel") ?? worldMapRoot.transform.Find("Panel");

    private static Transform FindCityRow(Transform panel, int cityIndex) =>
        panel.Find($"CityList/CityRow_{cityIndex + 1:00}");

    private static Transform FindCityPlay(Transform row) => row.Find("PlayBtn") ?? row.Find("Play");

    private void WireWorldMap()
    {
        Transform panel = WorldMapPanel;
        if (panel == null)
            return;

        WireButton(panel, "Back", HideWorldMap);
        WireButton(panel, "FastestBtn", () => ShowLeaderboardsOverlay(richest: false));
        UnityEngine.Events.UnityAction showRichest = () => ShowLeaderboardsOverlay(richest: true);
        WireButton(panel, "RichestBlock/RichestBtn", showRichest);
        Transform richestRow = panel.Find("Richest_Row");
        if (richestRow != null)
        {
            foreach (Button button in richestRow.GetComponentsInChildren<Button>(true))
                WireButton(button.transform, showRichest);
        }

        for (int i = 0; i < 3; i++)
        {
            int cityIndex = i;
            Transform row = FindCityRow(panel, cityIndex);
            if (row == null)
                continue;

            UnityEngine.Events.UnityAction play = () => StartCity(cityIndex, gameSceneName);
            UnityEngine.Events.UnityAction showBoard = () => ShowCityLeaderboard(cityIndex);
            WireButton(FindCityPlay(row), play);
            WireButton(row, "LevelIcon", play);
            WireButton(row, "ServerBestBtn", showBoard);
            WireButton(row, "ServerBest/ServerBestIcon", showBoard);
            WireButton(row, "Stars/StarsIcon", showBoard);
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

    private static void WireButton(Transform root, string path, UnityEngine.Events.UnityAction action) =>
        WireButton(root.Find(path), action);

    private static void WireButton(Transform target, UnityEngine.Events.UnityAction action)
    {
        if (target == null)
            return;
        var button = target.GetComponent<Button>();
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
        Transform panel = WorldMapPanel;
        if (panel == null)
            return;

        Transform title = panel.Find("TitlePanel/Title") ?? panel.Find("Title");
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
            StartTraining(gameSceneName);
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
        StartTraining(gameSceneName);
    }

    internal static void StartTraining(string gameSceneName)
    {
        TrainingSession.RequestStart();
        SceneManager.LoadScene(gameSceneName);
    }

    private void RefreshWorldMapProgress()
    {
        Transform panel = WorldMapPanel;
        if (panel == null)
            return;

        RefreshNetWorthStars(panel);

        for (int city = 0; city < 3; city++)
        {
            Transform row = FindCityRow(panel, city);
            if (row == null)
                continue;

            bool unlocked = CityProgress.IsUnlocked(city);
            string name = CityName(city);
            string best = "$" + CityProgress.GetBestDollars(city);
            SetTmp(row, "Name", name);
            SetTmp(row, "LevelName", name);
            SetTmp(row, "Stars", CityProgress.GetStars(city).ToString());
            SetTmp(row, "PlayerBest", best);
            SetTmp(row, "Stars/PlayerBestValue", best);
            SetTmp(row, "ServerBest/PlayerBestValue", "$" + GetCachedBest(LevelSystem.CityBoardId(city)));

            SetInteractable(FindCityPlay(row), unlocked);
            SetInteractable(row.Find("LevelIcon"), unlocked);

            var canvasGroup = row.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = row.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = unlocked ? 1f : 0.45f;
        }

        string netWorth = "$" + CityProgress.RichestSum();
        SetTmp(panel, "RichestBlock/RichestValue", netWorth);
        SetTmp(panel, "Richest_Row/Stars/PlayerBestValue", netWorth);
        SetTmp(panel, "Richest_Row/ServerBest/PlayerBestValue", "$" + GetCachedBest(HttpLeaderboards.RichestBoard));
    }

    private static int GetCachedBest(string board)
    {
        if (LeaderboardCache.TryGet(board, out YandexLeaderboards.BoardPage page)
            && page.Ok && page.Entries != null && page.Entries.Length > 0)
            return page.Entries[0].Score;

        return 0;
    }

    private static void SetInteractable(Transform target, bool interactable)
    {
        if (target != null && target.TryGetComponent(out Button button))
            button.interactable = interactable;
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

    private int _leaderboardCityIndex;

    private void OnLeaderboardChanged(string board)
    {
        RefreshWorldMapProgress();
        if (cityLeaderboardRoot != null && cityLeaderboardRoot.activeInHierarchy
            && board == LevelSystem.CityBoardId(_leaderboardCityIndex))
            RenderCityLeaderboard(_leaderboardCityIndex);

        if (leaderboardsOverlayRoot != null && leaderboardsOverlayRoot.activeInHierarchy)
        {
            if (board == HttpLeaderboards.RichestBoard)
                FillCachedBoard(leaderboardsOverlayRoot.transform.Find("Panel/RichestPanel"), board, money: true);
            if (board == HttpLeaderboards.FastestBoard)
                FillCachedBoard(leaderboardsOverlayRoot.transform.Find("Panel/FastestHandPanel"), board, money: false);
        }
    }

    private void ShowCityLeaderboard(int cityIndex)
    {
        if (cityLeaderboardRoot == null)
            return;

        _leaderboardCityIndex = cityIndex;
        RenderCityLeaderboard(cityIndex);
        SetPanel(cityLeaderboardRoot, true);
        cityLeaderboardRoot.transform.SetAsLastSibling();
        LeaderboardCache.Refresh(LevelSystem.CityBoardId(cityIndex));
    }

    private void RenderCityLeaderboard(int cityIndex)
    {
        string board = LevelSystem.CityBoardId(cityIndex);
        Transform title = cityLeaderboardRoot.transform.Find("Panel/TitlePanel/Title")
                          ?? cityLeaderboardRoot.transform.Find("Panel/Title");
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
                text.text = FormatPlayerResult(board, "$" + CityProgress.GetBestDollars(cityIndex));
        }

        if (list != null)
        {
            var text = list.GetComponent<TMP_Text>();
            if (text != null)
                text.text = FormatCachedBoard(board, money: true);
        }
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
        LeaderboardCache.Refresh(richest ? HttpLeaderboards.RichestBoard : HttpLeaderboards.FastestBoard);
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
            playerText.text = FormatPlayerResult(board, "$" + CityProgress.RichestSum());
            return;
        }

        int ms = CityProgress.GetFastestMs();
        playerText.text = FormatPlayerResult(board, ms < 0
            ? "0"
            : SeriesRun.FormatSeconds(ms / 1000f) + "s");
    }

    private static string FormatPlayerResult(string board, string score)
    {
        string player = LocalizationTables.Get("player_you") + " ";
        if (LeaderboardCache.TryGet(board, out var page) && page.Ok && page.UserRank > 0)
            player += "#" + page.UserRank + " ";
        return player + score;
    }

    private static string FormatCachedBoard(string board, bool money)
    {
        if (!LeaderboardCache.TryGet(board, out YandexLeaderboards.BoardPage page))
            return LocalizationTables.Get("leaderboard_loading");
        if (!page.Ok)
            return LocalizationTables.Get("leaderboard_unavailable");
        if (page.Entries == null || page.Entries.Length == 0)
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

    internal static void StartCity(int cityIndex, string gameSceneName)
    {
        if (cityIndex < 0 || cityIndex >= 3 || !CityProgress.IsUnlocked(cityIndex))
            return;

        TrainingSession.CancelPending();
        PlayerPrefs.SetInt(SelectedCityPrefsKey, cityIndex);
        PlayerPrefs.Save();
        PlatformAds.ShowInterstitial();
        SceneManager.LoadScene(gameSceneName);
    }

    private static void RefreshNetWorthStars(Transform panel)
    {
        Transform stars = panel.Find("RichestBlock/Stars")
                         ?? panel.Find("RichestBlock/NetWorthStars");
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
        return LocalizationTables.Get("city_" + LevelSystem.CityBoardId(index));
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
