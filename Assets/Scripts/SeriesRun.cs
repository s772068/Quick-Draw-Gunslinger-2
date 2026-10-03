using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// One duel series: consecutive wins and bell-to-hit times.
/// A loss or leaving the game discards it. Personal bests are stored only on a confirmed finish.
/// </summary>
public class SeriesRun
{
    public const string Dash = "—";

    private readonly List<float> _winSeconds = new List<float>();

    public int Wins { get; private set; }
    public bool Started { get; private set; }

    public void Begin()
    {
        Started = true;
    }

    public void Clear()
    {
        Started = false;
        Wins = 0;
        _winSeconds.Clear();
    }

    public void AddWin(float bellToHitSeconds)
    {
        Started = true;
        Wins++;
        _winSeconds.Add(Mathf.Max(0f, bellToHitSeconds));
    }

    public bool TryBestSeconds(out float seconds)
    {
        seconds = 0f;
        if (_winSeconds.Count == 0)
            return false;

        seconds = _winSeconds[0];
        for (int i = 1; i < _winSeconds.Count; i++)
        {
            if (_winSeconds[i] < seconds)
                seconds = _winSeconds[i];
        }

        return true;
    }

    /// <summary>
    /// Average of the last 10 winning rounds. Missing until the series has 10 wins.
    /// </summary>
    public bool TrySteadySeconds(out float seconds)
    {
        seconds = 0f;
        if (Wins < 10 || _winSeconds.Count < 10)
            return false;

        float sum = 0f;
        int start = _winSeconds.Count - 10;
        for (int i = start; i < _winSeconds.Count; i++)
            sum += _winSeconds[i];

        seconds = sum / 10f;
        return true;
    }

    public string SureText => Started ? Wins.ToString(CultureInfo.InvariantCulture) : Dash;

    public string FastestText => TryBestSeconds(out float seconds) ? FormatSeconds(seconds) : Dash;

    public string SteadyText => TrySteadySeconds(out float seconds) ? FormatSeconds(seconds) : Dash;

    public static string FormatSeconds(float seconds)
    {
        return seconds.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static int ToMilliseconds(float seconds)
    {
        return Mathf.Max(0, Mathf.RoundToInt(seconds * 1000f));
    }
}

/// <summary>
/// Device-local personal bests. Survives refresh. Guests never go to the Yandex board.
/// Sure: higher wins. Fastest and Steady: lower time.
/// </summary>
public static class PersonalRecords
{
    private const string SureKey = "QDG2.Best.Sure";
    private const string FastKey = "QDG2.Best.FastMs";
    private const string SteadyKey = "QDG2.Best.SteadyMs";

    public static void Commit(SeriesRun series)
    {
        if (series == null || series.Wins <= 0)
            return;

        int sure = PlayerPrefs.GetInt(SureKey, 0);
        if (series.Wins > sure)
            PlayerPrefs.SetInt(SureKey, series.Wins);

        if (series.TryBestSeconds(out float best))
        {
            int ms = SeriesRun.ToMilliseconds(best);
            int prev = PlayerPrefs.GetInt(FastKey, -1);
            if (prev < 0 || ms < prev)
                PlayerPrefs.SetInt(FastKey, ms);
        }

        if (series.TrySteadySeconds(out float steady))
        {
            int ms = SeriesRun.ToMilliseconds(steady);
            int prev = PlayerPrefs.GetInt(SteadyKey, -1);
            if (prev < 0 || ms < prev)
                PlayerPrefs.SetInt(SteadyKey, ms);
        }

        PlayerPrefs.Save();
    }

    public static string Format(LeaderboardKind kind)
    {
        switch (kind)
        {
            case LeaderboardKind.Sure:
            {
                int wins = PlayerPrefs.GetInt(SureKey, 0);
                return wins > 0 ? wins.ToString(CultureInfo.InvariantCulture) : SeriesRun.Dash;
            }
            case LeaderboardKind.Fastest:
                return FormatMs(PlayerPrefs.GetInt(FastKey, -1));
            default:
                return FormatMs(PlayerPrefs.GetInt(SteadyKey, -1));
        }
    }

    private static string FormatMs(int ms)
    {
        if (ms < 0)
            return SeriesRun.Dash;
        return SeriesRun.FormatSeconds(ms / 1000f);
    }
}

public enum LeaderboardKind
{
    Sure = 0,
    Fastest = 1,
    Steady = 2
}
