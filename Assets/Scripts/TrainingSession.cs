using UnityEngine;

/// <summary>
/// GDD 1.2: first-launch flag and pending/active training session across MainMenu → GameScene.
/// </summary>
public static class TrainingSession
{
    public const string FirstLaunchKey = "QDG2_FirstLaunch";
    public const string PendingKey = "QDG2_TrainingPending";
    public const int TrainingLevelIndex = 3;

    public static bool IsFirstLaunch
    {
        get => PlayerPrefs.GetInt(FirstLaunchKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(FirstLaunchKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool Pending
    {
        get => PlayerPrefs.GetInt(PendingKey, 0) != 0;
        private set
        {
            PlayerPrefs.SetInt(PendingKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool Active { get; private set; }
    public static bool SilentModals { get; set; }

    public static void RequestStart(bool silentModals = false)
    {
        SilentModals = silentModals;
        Pending = true;
    }

    public static bool ConsumePending()
    {
        if (!Pending)
        {
            Active = false;
            return false;
        }

        Pending = false;
        Active = true;
        return true;
    }

    public static void MarkCompletedAndExit()
    {
        IsFirstLaunch = false;
        SilentModals = false;
        Active = false;
        Pending = false;
    }

    public static void ClearActive()
    {
        Active = false;
        SilentModals = false;
    }
}
