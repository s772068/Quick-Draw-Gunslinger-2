using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class WorldMapPreview : MonoBehaviour
{
    private const string GameSceneName = "GameScene";
    private const float LockedBrightness = 0.82f;

    internal static bool OpenMapOnLoad { private get; set; }

    [SerializeField] private GameObject menuScreen, mapScreen;
    [SerializeField] private Button startButton, trainingButton, backButton, soundButton;
    [SerializeField] private Sprite soundOn, soundOff;
    [SerializeField] private Sprite[] ratings;
    [SerializeField] private Image[] townPortraits;
    [SerializeField] private TextMeshProUGUI[] townNames;
    [SerializeField] private Button[] townButtons;
    [SerializeField] private Image[] highlights;
    [SerializeField] private Image[] townLocks, routesToTown;
    [SerializeField] private RectTransform modal;
    [SerializeField] private Button backdrop, mapPlay, modalPlay, close;
    [SerializeField] private GameObject leaderboardModal;
    [SerializeField] private Button leaderboardButton, balanceButton, leaderboardBackdrop, leaderboardClose;
    [SerializeField] private TextMeshProUGUI leaderboardTitle, leaderboardStatus;
    [SerializeField] private TextMeshProUGUI[] leaderboardColumns;
    [SerializeField] private TextMeshProUGUI mapName, mapMoney, modalName, modalMoney, totalBalance;
    [SerializeField] private Image mapStars, modalStars, portrait, mapPlayIcon, modalPlayIcon;
    [SerializeField] private Sprite lockedPlayIcon;

    [Tooltip("In Editor Play Mode, launch directly into training as on a first launch.")]
    [SerializeField] private bool simulateFirstLaunch;
    [SerializeField] private string lockedLabel = "LOCKED";

    private TextMeshProUGUI mapPlayLabel, modalPlayLabel;
    private string mapPlayText, modalPlayText;
    private Sprite mapPlaySprite, modalPlaySprite;
    private int selected;
    private Button leaderboardOrigin;
    private string activeBoard;
    private readonly List<TextMeshProUGUI[]> leaderboardRows = new List<TextMeshProUGUI[]>();

    private void OnEnable() => LeaderboardCache.BoardChanged += OnLeaderboardChanged;

    private void OnDisable() => LeaderboardCache.BoardChanged -= OnLeaderboardChanged;

    private void Start()
    {
#if UNITY_EDITOR
        if (simulateFirstLaunch)
            TrainingSession.SimulateFirstLaunch();
#endif
        if (TrainingSession.IsFirstLaunch)
        {
            StartTraining();
            return;
        }

        var input = GetComponentInChildren<InputSystemUIInputModule>();
        if (input.actionsAsset == null || input.point == null)
            input.AssignDefaultActions();

        mapPlayLabel = mapPlay.GetComponentInChildren<TextMeshProUGUI>(true);
        modalPlayLabel = modalPlay.GetComponentInChildren<TextMeshProUGUI>(true);
        mapPlayText = mapPlayLabel.text;
        modalPlayText = modalPlayLabel.text;
        mapPlaySprite = mapPlayIcon.sprite;
        modalPlaySprite = modalPlayIcon.sprite;
        for (int i = 0; i < townButtons.Length; i++)
        {
            int index = i;
            townButtons[index].onClick.AddListener(() => OpenTown(index));
        }

        backdrop.onClick.AddListener(CloseModal);
        close.onClick.AddListener(CloseModal);
        leaderboardButton.onClick.AddListener(() => OpenLeaderboard(leaderboardButton));
        balanceButton.onClick.AddListener(() => OpenLeaderboard(balanceButton));
        leaderboardBackdrop.onClick.AddListener(CloseLeaderboard);
        leaderboardClose.onClick.AddListener(CloseLeaderboard);
        mapPlay.onClick.AddListener(PlaySelectedCity);
        modalPlay.onClick.AddListener(PlaySelectedCity);
        startButton.onClick.AddListener(OnStartClicked);
        trainingButton.onClick.AddListener(StartTraining);
        backButton.onClick.AddListener(ShowMenu);
        soundButton.onClick.AddListener(ToggleSound);
        UpdateSoundIcon();
        modal.gameObject.SetActive(false);
        leaderboardModal.SetActive(false);

        if (OpenMapOnLoad)
        {
            OpenMapOnLoad = false;
            int city = PlayerPrefs.GetInt(MainMenuController.SelectedCityPrefsKey, 0);
            if (city >= 0 && city < townButtons.Length && CityProgress.IsUnlocked(city))
                selected = city;
            ShowMap();
        }
        else
        {
            ShowMenu();
        }
    }

    private void OnStartClicked()
    {
        if (TrainingSession.IsFirstLaunch)
            StartTraining();
        else
            ShowMap();
    }

    private static void StartTraining() => MainMenuController.StartTraining(GameSceneName);

    private void PlaySelectedCity() => MainMenuController.StartCity(selected, GameSceneName);

    private void ShowMap()
    {
        RefreshTownAccess();
        SelectTown(selected);
        menuScreen.SetActive(false);
        mapScreen.SetActive(true);
        townButtons[selected].Select();
    }

    private void ShowMenu()
    {
        mapScreen.SetActive(false);
        menuScreen.SetActive(true);
        startButton.Select();
    }

    private void ToggleSound()
    {
        GameAudioSettings.ToggleSound();
        UpdateSoundIcon();
    }

    private void UpdateSoundIcon()
    {
        soundButton.image.sprite = GameAudioSettings.SoundEnabled ? soundOn : soundOff;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (leaderboardModal.activeInHierarchy)
            CloseLeaderboard();
        else if (modal.gameObject.activeInHierarchy)
            CloseModal();
        else if (mapScreen.activeInHierarchy)
            ShowMenu();
    }

    private void OpenTown(int index)
    {
        SelectTown(index);
        modal.gameObject.SetActive(true);
        close.Select();
    }

    private void OpenLeaderboard(Button origin)
    {
        leaderboardOrigin = origin;
        bool cityBoard = origin == leaderboardButton;
        activeBoard = cityBoard ? LevelSystem.CityBoardId(selected) : HttpLeaderboards.RichestBoard;
        leaderboardTitle.text = cityBoard
            ? townNames[selected].text.ToUpperInvariant()
            : LocalizationTables.IsRussian ? "РЕЙТИНГ КАПИТАЛА" : "NET WORTH LEADERBOARD";

        modal.gameObject.SetActive(false);
        leaderboardModal.SetActive(true);
        RenderLeaderboard();
        LeaderboardCache.Refresh(activeBoard);
        leaderboardClose.Select();
    }

    private void OnLeaderboardChanged(string board)
    {
        if (board == activeBoard && leaderboardModal.activeInHierarchy)
            RenderLeaderboard();
    }

    private void RenderLeaderboard()
    {
        bool cached = LeaderboardCache.TryGet(activeBoard, out var page);
        var entries = cached && page.Ok ? page.Entries : null;
        // The Worker returns the top three and up to three entries around the player.
        int count = Mathf.Min(6, entries?.Length ?? 0);
        leaderboardStatus.gameObject.SetActive(count == 0);
        if (count == 0)
        {
            bool russian = LocalizationTables.IsRussian;
            if (!cached)
                leaderboardStatus.text = russian ? "Загрузка…" : "Loading…";
            else if (!page.Ok)
                leaderboardStatus.text = russian ? "Таблица недоступна" : "Leaderboard unavailable";
            else
                leaderboardStatus.text = LocalizationTables.Get(LocalizationTables.Keys.LeaderboardEmpty);
        }

        for (int i = 0; i < count; i++)
        {
            if (i == leaderboardRows.Count)
                AddLeaderboardRow(i);

            var row = leaderboardRows[i];
            row[0].text = entries[i].Rank.ToString();
            row[1].text = entries[i].Name;
            row[2].text = "$" + entries[i].Score;
        }

        for (int i = 0; i < leaderboardRows.Count; i++)
            foreach (var cell in leaderboardRows[i])
                cell.gameObject.SetActive(i < count);
    }

    private void AddLeaderboardRow(int index)
    {
        var row = new TextMeshProUGUI[leaderboardColumns.Length];
        for (int i = 0; i < leaderboardColumns.Length; i++)
        {
            var heading = leaderboardColumns[i];
            var cell = Instantiate(heading, heading.transform.parent, false);
            cell.name = "Leaderboard row " + (index + 1) + " column " + (i + 1);
            cell.richText = false;
            cell.enableAutoSizing = true;
            cell.fontSizeMin = 12f;
            cell.fontSizeMax = 32f;
            cell.overflowMode = TextOverflowModes.Ellipsis;
            cell.rectTransform.anchoredPosition = heading.rectTransform.anchoredPosition
                                                 + Vector2.down * (70f + index * 60f);
            row[i] = cell;
        }
        leaderboardRows.Add(row);
    }

    private void CloseLeaderboard()
    {
        leaderboardModal.SetActive(false);
        modal.gameObject.SetActive(leaderboardOrigin == leaderboardButton);
        leaderboardOrigin.Select();
    }

    private void SelectTown(int index)
    {
        selected = index;
        for (int i = 0; i < highlights.Length; i++)
            highlights[i].enabled = i == index;

        mapName.text = modalName.text = townNames[index].text;
        mapMoney.text = modalMoney.text = "$" + CityProgress.GetBestDollars(index);
        mapStars.sprite = modalStars.sprite = ratings[Mathf.Clamp(CityProgress.GetStars(index), 0, ratings.Length - 1)];
        totalBalance.text = "$" + CityProgress.RichestSum();
        portrait.sprite = townPortraits[index].sprite;
        bool locked = !CityProgress.IsUnlocked(index);
        portrait.color = townPortraits[index].color;
        mapPlayLabel.text = locked ? lockedLabel : mapPlayText;
        modalPlayLabel.text = locked ? lockedLabel : modalPlayText;
        mapPlay.interactable = modalPlay.interactable = !locked;
        mapPlayIcon.sprite = locked ? lockedPlayIcon : mapPlaySprite;
        modalPlayIcon.sprite = locked ? lockedPlayIcon : modalPlaySprite;
    }

    private void RefreshTownAccess()
    {
        // Each transition and padlock belongs to its destination town. Dry Creek starts open.
        for (int i = 0; i < routesToTown.Length; i++)
        {
            int town = i + 1;
            bool unlocked = CityProgress.IsUnlocked(town);
            routesToTown[i].enabled = unlocked;
            townLocks[i].enabled = !unlocked;
            townPortraits[town].color = unlocked
                ? Color.white
                : new Color(LockedBrightness, LockedBrightness, LockedBrightness);
        }
    }

    private void CloseModal()
    {
        modal.gameObject.SetActive(false);
        townButtons[selected].Select();
    }
}




