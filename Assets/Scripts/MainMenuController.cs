using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GDD 0.4/0.5 MainMenu: Begin → GameScene, Sound toggle, localized title/button.
/// Title = Zantroke SDF; Begin label = LiberationSans SDF (TMP).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Image soundImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text beginLabelTmp;

    private void Awake()
    {
        GameAudioSettings.ApplyListenerVolume();
        AutoBindIfNeeded();
        ApplyLocalizedTexts();

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnBeginClicked);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveAllListeners();
            soundButton.onClick.AddListener(OnSoundClicked);
        }

        if (soundImage == null && soundButton != null)
            soundImage = soundButton.targetGraphic as Image;

        RefreshSoundVisual();
        YandexGamesSdk.GameplayStop();
    }

    private void OnEnable()
    {
        LocalizationTables.LanguageChanged += ApplyLocalizedTexts;
    }

    private void OnDisable()
    {
        LocalizationTables.LanguageChanged -= ApplyLocalizedTexts;
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

        if (titleText == null)
        {
            var title = GameObject.Find("Title");
            if (title != null)
                titleText = title.GetComponent<TMP_Text>();
        }

        if (beginLabelTmp == null && startButton != null)
            beginLabelTmp = startButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void ApplyLocalizedTexts()
    {
        if (titleText != null)
            titleText.text = LocalizationTables.Get(LocalizationTables.Keys.GameTitle);

        if (beginLabelTmp != null)
            beginLabelTmp.text = LocalizationTables.Get(LocalizationTables.Keys.MenuBegin);
    }

    private void OnBeginClicked()
    {
        SceneManager.LoadScene(gameSceneName);
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
