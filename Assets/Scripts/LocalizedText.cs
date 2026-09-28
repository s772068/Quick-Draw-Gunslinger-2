using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds a UI Text / TMP_Text to a Unity Localization Tables key (UI collection).
/// Hint D/P/F stay unlocalized.
/// </summary>
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;
    [SerializeField] private Text uiText;
    [SerializeField] private TMP_Text tmpText;

    public string Key
    {
        get => key;
        set
        {
            key = value;
            Apply();
        }
    }

    private void Awake()
    {
        AutoBind();
        Apply();
    }

    private void OnEnable()
    {
        LocalizationTables.LanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        LocalizationTables.LanguageChanged -= Apply;
    }

    public void Apply()
    {
        if (string.IsNullOrEmpty(key))
            return;

        AutoBind();
        string value = LocalizationTables.Get(key);
        if (uiText != null)
            uiText.text = value;
        if (tmpText != null)
            tmpText.text = value;
    }

    private void AutoBind()
    {
        if (uiText == null)
            uiText = GetComponent<Text>();
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();
    }
}
