using System.Collections;
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
    internal static int? CityLeaderboardOnLoad { private get; set; }

    [SerializeField] private GameObject menuScreen, mapScreen;
    [SerializeField] private Button startButton, trainingButton, backButton, soundButton;
    [SerializeField] private Sprite soundOn, soundOff;
    [SerializeField] private Sprite[] ratings;
    [SerializeField] private Image[] townPortraits;
    [SerializeField] private TextMeshProUGUI[] townNames;
    [SerializeField] private Button[] townButtons;
    [SerializeField] private Image[] highlights;
    [SerializeField] private Image[] townLocks;
    [SerializeField] private RectTransform modal;
    [SerializeField] private Button backdrop, mapPlay, modalPlay, close;
    [SerializeField] private GameObject leaderboardModal;
    [SerializeField] private Button leaderboardButton, balanceButton, leaderboardBackdrop, leaderboardClose;
    [SerializeField] private TextMeshProUGUI leaderboardTitle, leaderboardStatus;
    [SerializeField] private TextMeshProUGUI[] leaderboardColumns;
    [SerializeField] private TextMeshProUGUI mapName, mapMoney, modalName, modalMoney, totalBalance;
    [SerializeField] private Image mapStars, modalStars, portrait, mapPlayIcon, modalPlayIcon;
    [SerializeField] private Sprite lockedPlayIcon;
    [SerializeField] private AudioSource menuMusic;
    [SerializeField] private AudioClip buttonClick;

    [Tooltip("In Editor Play Mode, launch directly into training as on a first launch.")]
    [SerializeField] private bool simulateFirstLaunch;

    private TextMeshProUGUI mapPlayLabel, modalPlayLabel;
    private Sprite mapPlaySprite, modalPlaySprite;
    private int selected;
    private Button leaderboardOrigin;
    private bool returnToTownModal;
    private string activeBoard;
    private readonly List<TextMeshProUGUI[]> leaderboardRows = new List<TextMeshProUGUI[]>();

    private void OnEnable()
    {
        LeaderboardCache.BoardChanged += OnLeaderboardChanged;
        LocalizationTables.LanguageChanged += ApplyLocalizedTexts;
    }

    private void OnDisable()
    {
        LeaderboardCache.BoardChanged -= OnLeaderboardChanged;
        LocalizationTables.LanguageChanged -= ApplyLocalizedTexts;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!YandexGamesSdk.SdkAvailable)
            GameAudioSettings.SetPlatformMuted(!hasFocus);
    }

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
            int? leaderboardCity = CityLeaderboardOnLoad;
            CityLeaderboardOnLoad = null;
            int city = leaderboardCity ?? PlayerPrefs.GetInt(MainMenuController.SelectedCityPrefsKey, 0);
            if (city >= 0 && city < townButtons.Length && CityProgress.IsUnlocked(city))
                selected = city;
            ShowMap();
            if (leaderboardCity == selected)
                OpenLeaderboard(leaderboardButton, returnToTown: false);
        }
        else
        {
            ShowMenu();
        }

        ApplyLocalizedTexts();

        if (buttonClick != null)
        {
            buttonClick.LoadAudioData();
            foreach (var button in GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => GameAudioSettings.PlayButtonClick(buttonClick));
        }

        if (menuMusic != null)
            StartCoroutine(PlayMenuMusic());
    }

    private IEnumerator PlayMenuMusic()
    {
        yield return new WaitForSecondsRealtime(2f);
        menuMusic.Play();
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

    private void OpenLeaderboard(Button origin, bool returnToTown = true)
    {
        leaderboardOrigin = origin;
        bool cityBoard = origin == leaderboardButton;
        returnToTownModal = cityBoard && returnToTown;
        activeBoard = cityBoard ? LevelSystem.CityBoardId(selected) : HttpLeaderboards.RichestBoard;
        ApplyLeaderboardTitle();

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

    private void ApplyLocalizedTexts()
    {
        if (mapPlayLabel == null || !LocalizationTables.IsReady)
            return;

        SceneTextLocalizer.Apply();
        for (int i = 0; i < townNames.Length; i++)
            townNames[i].text = LocalizationTables.Get("city_" + LevelSystem.CityBoardId(i)).ToUpperInvariant();

        SelectTown(selected);
        if (leaderboardModal.activeInHierarchy)
        {
            ApplyLeaderboardTitle();
            RenderLeaderboard();
        }
    }

    private void ApplyLeaderboardTitle()
    {
        leaderboardTitle.text = activeBoard == HttpLeaderboards.RichestBoard
            ? LocalizationTables.Get("net_worth_leaderboard", leaderboardTitle.text)
            : townNames[selected].text;
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
            if (!cached)
                leaderboardStatus.text = LocalizationTables.Get("leaderboard_loading");
            else if (!page.Ok)
                leaderboardStatus.text = LocalizationTables.Get("leaderboard_unavailable");
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
        modal.gameObject.SetActive(returnToTownModal);
        if (returnToTownModal || leaderboardOrigin == balanceButton)
            leaderboardOrigin.Select();
        else
            townButtons[selected].Select();
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
        if (LocalizationTables.IsReady)
            mapPlayLabel.text = modalPlayLabel.text = LocalizationTables.Get(locked ? "locked" : "play");
        mapPlay.interactable = modalPlay.interactable = !locked;
        mapPlayIcon.sprite = locked ? lockedPlayIcon : mapPlaySprite;
        modalPlayIcon.sprite = locked ? lockedPlayIcon : modalPlaySprite;
    }

    private void RefreshTownAccess()
    {
        // Dry Creek starts open, so the padlocks begin with the second town.
        for (int i = 0; i < townLocks.Length; i++)
        {
            int town = i + 1;
            bool unlocked = CityProgress.IsUnlocked(town);
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




