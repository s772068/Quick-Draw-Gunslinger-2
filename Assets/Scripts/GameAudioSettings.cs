using UnityEngine;

/// <summary>
/// Persistent mute for GDD 0.4/0.5. User toggle survives F5 via PlayerPrefs.
/// Platform mute (focus / ads) is separate and does not overwrite user preference.
/// </summary>
public static class GameAudioSettings
{
    private const string PrefKey = "QDG2.SoundEnabled";

    private static bool _platformMuted;

    public static bool SoundEnabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) != 0;
        private set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyListenerVolume();
        }
    }

    public static bool PlatformMuted => _platformMuted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        ApplyListenerVolume();
    }

    public static void SetSoundEnabled(bool enabled)
    {
        SoundEnabled = enabled;
    }

    public static void ToggleSound()
    {
        SoundEnabled = !SoundEnabled;
    }

    /// <summary>
    /// Yandex req 1.3 / 4.7: mute while unfocused or during ads (game_api_pause).
    /// </summary>
    public static void SetPlatformMuted(bool muted)
    {
        if (_platformMuted == muted)
            return;
        _platformMuted = muted;
        ApplyListenerVolume();
    }

    public static void ApplyListenerVolume()
    {
        bool audible = SoundEnabled && !_platformMuted;
        AudioListener.volume = audible ? 1f : 0f;
    }
}
