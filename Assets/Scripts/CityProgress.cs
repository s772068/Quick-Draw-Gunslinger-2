using UnityEngine;

/// <summary>
/// Local progression: unlocks, per-city best $, stars, richest sum, fastest hand.
/// </summary>
public static class CityProgress
{
#if UNITY_EDITOR
    private const string ProgressPrefix = "QDG2_EditorPlay_";
#else
    private const string ProgressPrefix = "QDG2_";
#endif
    private const string UnlockKey = ProgressPrefix + "UnlockCity";
    private const string BestPrefix = ProgressPrefix + "Best_";
    private const string StarsPrefix = ProgressPrefix + "Stars_";
    private const string FastestKey = ProgressPrefix + "FastestMs";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEditorProgress()
    {
        PlayerPrefs.DeleteKey(UnlockKey);
        PlayerPrefs.DeleteKey(FastestKey);
        for (int city = 0; city < 3; city++)
        {
            PlayerPrefs.DeleteKey(BestPrefix + city);
            PlayerPrefs.DeleteKey(StarsPrefix + city);
        }
    }
#endif

    public static int HighestUnlockedCity
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(UnlockKey, 0), 0, 2);
        private set => PlayerPrefs.SetInt(UnlockKey, value);
    }

    public static bool IsUnlocked(int cityIndex) => cityIndex <= HighestUnlockedCity;

    public static int GetBestDollars(int cityIndex) => PlayerPrefs.GetInt(BestPrefix + cityIndex, 0);

    public static int GetStars(int cityIndex) => PlayerPrefs.GetInt(StarsPrefix + cityIndex, 0);

    public static int GetFastestMs()
    {
        int v = PlayerPrefs.GetInt(FastestKey, -1);
        return v;
    }

    public static int RichestSum()
    {
        int sum = 0;
        for (int i = 0; i < 3; i++)
            sum += GetBestDollars(i);
        return sum;
    }

    public static void CommitCityClear(int cityIndex, int totalDollars, int stars, float bestFactTimeSeconds)
    {
        int prevBest = GetBestDollars(cityIndex);
        if (totalDollars > prevBest)
            PlayerPrefs.SetInt(BestPrefix + cityIndex, totalDollars);

        int prevStars = GetStars(cityIndex);
        if (stars > prevStars)
            PlayerPrefs.SetInt(StarsPrefix + cityIndex, stars);

        int ms = Mathf.Max(0, Mathf.RoundToInt(bestFactTimeSeconds * 1000f));
        int prevFast = GetFastestMs();
        if (prevFast < 0 || ms < prevFast)
            PlayerPrefs.SetInt(FastestKey, ms);

        if (cityIndex >= HighestUnlockedCity && cityIndex < 2)
            HighestUnlockedCity = cityIndex + 1;

#if !UNITY_EDITOR
        HttpLeaderboards.QueueCityClear(cityIndex);
#endif
    }
}

/// <summary>
/// In-memory run for one city (5 rounds). Burned on lose/abort.
/// </summary>
public class CityRun
{
    public int CityIndex { get; private set; }
    public int RoundNumber { get; private set; } = 1;
    public int RunDollars { get; private set; }
    public int LastRoundDollars { get; private set; }
    public float LastFactTime { get; private set; }
    public float BestFactTime { get; private set; } = float.MaxValue;
    public bool Active { get; private set; }

    public void Begin(int cityIndex)
    {
        CityIndex = cityIndex;
        RoundNumber = 1;
        RunDollars = 0;
        LastRoundDollars = 0;
        LastFactTime = 0f;
        BestFactTime = float.MaxValue;
        Active = true;
    }

    public void Restore(int cityIndex, int roundNumber, int runDollars, float bestFactTime)
    {
        CityIndex = Mathf.Clamp(cityIndex, 0, 2);
        RoundNumber = Mathf.Clamp(roundNumber, 1, 5);
        RunDollars = Mathf.Max(0, runDollars);
        LastRoundDollars = 0;
        LastFactTime = 0f;
        BestFactTime = bestFactTime > 0f ? bestFactTime : float.MaxValue;
        Active = true;
    }

    public void Clear()
    {
        Active = false;
        RoundNumber = 1;
        RunDollars = 0;
        LastRoundDollars = 0;
        LastFactTime = 0f;
        BestFactTime = float.MaxValue;
    }

    public void RegisterWin(float factTimeSeconds)
    {
        LastFactTime = Mathf.Max(0f, factTimeSeconds);
        if (LastFactTime < BestFactTime)
            BestFactTime = LastFactTime;
        LastRoundDollars = ScoreSystem.RoundDollars(RoundNumber, LastFactTime);
        RunDollars += LastRoundDollars;
    }

    public void AdvanceRound()
    {
        RoundNumber = Mathf.Min(5, RoundNumber + 1);
    }

    public bool IsFinalRound => RoundNumber >= 5;
}
