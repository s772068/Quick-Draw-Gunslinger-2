using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Yandex leaderboard bridge. Technical names stay on the *temp boards until 1.0.
/// setScore is limited to one call per second.
/// </summary>
public class YandexLeaderboards : MonoBehaviour
{
    public const string SureBoard = "surehandtemp";
    public const string FastestBoard = "fastesthandtemp";
    public const string SteadyBoard = "steadyhandtemp";

    public static YandexLeaderboards Instance { get; private set; }

    public readonly struct Entry
    {
        public readonly int Rank;
        public readonly string Name;
        public readonly int Score;
        public readonly string AvatarUrl;

        public Entry(int rank, string name, int score, string avatarUrl)
        {
            Rank = rank;
            Name = name;
            Score = score;
            AvatarUrl = avatarUrl;
        }
    }

    public readonly struct BoardPage
    {
        public readonly bool Ok;
        public readonly int UserRank;
        public readonly Entry[] Entries;

        public BoardPage(bool ok, int userRank, Entry[] entries)
        {
            Ok = ok;
            UserRank = userRank;
            Entries = entries ?? Array.Empty<Entry>();
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void YandexGames_QueryAuth(string gameObject, string method);
    [DllImport("__Internal")] private static extern void YandexGames_OpenAuth(string gameObject, string method);
    [DllImport("__Internal")] private static extern void YandexGames_SubmitScore(string board, int score, string gameObject, string okMethod, string failMethod);
    [DllImport("__Internal")] private static extern void YandexGames_LoadEntries(string board, string gameObject, string method);
#endif

    private Action<bool> _authCallback;
    private Action<bool> _scoreCallback;
    private Action<BoardPage> _entriesCallback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Ensure()
    {
        if (Instance != null)
            return;

        var go = new GameObject("YandexLeaderboards");
        DontDestroyOnLoad(go);
        go.AddComponent<YandexLeaderboards>();
    }

    public static string BoardName(LeaderboardKind kind)
    {
        switch (kind)
        {
            case LeaderboardKind.Fastest: return FastestBoard;
            case LeaderboardKind.Steady: return SteadyBoard;
            default: return SureBoard;
        }
    }

    public static bool IsTimeBoard(LeaderboardKind kind)
    {
        return kind != LeaderboardKind.Sure;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void QueryAuthorized(Action<bool> callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!YandexGamesSdk.SdkAvailable)
        {
            callback?.Invoke(false);
            return;
        }

        _authCallback = callback;
        try
        {
            YandexGames_QueryAuth(gameObject.name, nameof(OnAuthResult));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Yandex auth query failed: " + e.Message);
            _authCallback = null;
            callback?.Invoke(false);
        }
#else
        callback?.Invoke(false);
#endif
    }

    public void OpenAuthDialog(Action<bool> callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!YandexGamesSdk.SdkAvailable)
        {
            callback?.Invoke(false);
            return;
        }

        _authCallback = callback;
        try
        {
            YandexGames_OpenAuth(gameObject.name, nameof(OnAuthResult));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Yandex auth dialog failed: " + e.Message);
            _authCallback = null;
            callback?.Invoke(false);
        }
#else
        callback?.Invoke(false);
#endif
    }

    public void OnAuthResult(string value)
    {
        Action<bool> cb = _authCallback;
        _authCallback = null;
        cb?.Invoke(value == "1");
    }

    public IEnumerator Submit(string board, int score)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!YandexGamesSdk.SdkAvailable)
            yield break;

        bool done = false;
        bool ok = false;
        _scoreCallback = success =>
        {
            ok = success;
            done = true;
        };

        try
        {
            YandexGames_SubmitScore(board, score, gameObject.name, nameof(OnScoreOk), nameof(OnScoreFail));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Yandex setScore failed: " + e.Message);
            _scoreCallback = null;
            yield break;
        }

        float wait = 0f;
        while (!done && wait < 8f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        _scoreCallback = null;
        if (!ok)
            Debug.LogWarning("Yandex setScore rejected for " + board);

        yield return new WaitForSecondsRealtime(1.1f);
#else
        yield break;
#endif
    }

    public void OnScoreOk(string board)
    {
        Action<bool> cb = _scoreCallback;
        _scoreCallback = null;
        cb?.Invoke(true);
    }

    public void OnScoreFail(string error)
    {
        Debug.LogWarning("Yandex setScore: " + error);
        Action<bool> cb = _scoreCallback;
        _scoreCallback = null;
        cb?.Invoke(false);
    }

    public void Load(LeaderboardKind kind, Action<BoardPage> callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!YandexGamesSdk.SdkAvailable)
        {
            callback?.Invoke(new BoardPage(false, 0, null));
            return;
        }

        _entriesCallback = callback;
        try
        {
            YandexGames_LoadEntries(BoardName(kind), gameObject.name, nameof(OnEntries));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Yandex getEntries failed: " + e.Message);
            _entriesCallback = null;
            callback?.Invoke(new BoardPage(false, 0, null));
        }
#else
        callback?.Invoke(new BoardPage(false, 0, null));
#endif
    }

    public void OnEntries(string payload)
    {
        Action<BoardPage> cb = _entriesCallback;
        _entriesCallback = null;
        cb?.Invoke(Parse(payload));
    }

    public void LoadRank(string board, Action<int> callback)
    {
        LeaderboardKind kind = board == FastestBoard
            ? LeaderboardKind.Fastest
            : board == SteadyBoard ? LeaderboardKind.Steady : LeaderboardKind.Sure;

        Load(kind, page => callback?.Invoke(page.Ok ? page.UserRank : 0));
    }

    private static BoardPage Parse(string payload)
    {
        if (string.IsNullOrEmpty(payload) || payload == "fail")
            return new BoardPage(false, 0, null);

        string[] lines = payload.Split('\n');
        int userRank = 0;
        if (lines.Length > 0)
            int.TryParse(lines[0], out userRank);

        var list = new List<Entry>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
                continue;

            string[] parts = lines[i].Split('|');
            if (parts.Length < 3)
                continue;

            int.TryParse(parts[0], out int rank);
            int.TryParse(parts[2], out int score);
            string avatar = parts.Length > 3 ? parts[3] : string.Empty;
            list.Add(new Entry(rank, parts[1], score, avatar));
        }

        return new BoardPage(true, userRank, list.ToArray());
    }
}
