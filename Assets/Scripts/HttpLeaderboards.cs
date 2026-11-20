using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cloudflare Worker leaderboards and persistent delivery of completed city records.
/// </summary>
public class HttpLeaderboards : MonoBehaviour
{
    public const string SureBoard = "richest";
    public const string RichestBoard = "richest";
    public const string FastestBoard = "fastest";
    public const string SteadyBoard = "steady";

    private const string BaseUrl = "https://qdg2-leaderboards.s772068.workers.dev";

    public static HttpLeaderboards Instance { get; private set; }

    private static readonly string[] CityBoards =
    {
        LevelSystem.CityBoardId(0), LevelSystem.CityBoardId(1), LevelSystem.CityBoardId(2)
    };
    private static readonly string[] PendingBoards =
    {
        CityBoards[0], CityBoards[1], CityBoards[2], FastestBoard
    };
    private bool _flushing;

    private static string PendingKey(string board) => "QDG2_LeaderboardPending_" + board;
    private static string ConfirmedKey(string board) => "QDG2_LeaderboardConfirmed_" + board;

    private static void QueueRecord(string board, int score)
    {
        if (PlayerPrefs.GetInt(ConfirmedKey(board), -1) != score)
            PlayerPrefs.SetInt(PendingKey(board), score);
    }

    public static void QueueCityClear(int cityIndex)
    {
        for (int i = 0; i < CityBoards.Length; i++)
        {
            int best = CityProgress.GetBestDollars(i);
            if (i == cityIndex || best > 0)
                QueueRecord(CityBoards[i], best);
        }
        int fastest = CityProgress.GetFastestMs();
        if (fastest >= 0)
            QueueRecord(FastestBoard, fastest);
        PlayerPrefs.Save();
    }

    private IEnumerator Start()
    {
        while (true)
        {
            FlushPending();
            yield return new WaitForSecondsRealtime(30f);
        }
    }

    public void FlushPending()
    {
        if (!_flushing && PlayerIdentity.HasNickname)
            StartCoroutine(DeliverPending());
    }

    private IEnumerator DeliverPending()
    {
        _flushing = true;
        string nick = PlayerIdentity.Nickname;
        foreach (string board in PendingBoards)
        {
            string key = PendingKey(board);
            if (!PlayerPrefs.HasKey(key))
                continue;

            int score = PlayerPrefs.GetInt(key);
            bool acknowledged = false;
            yield return Submit(board, score, nick, ok => acknowledged = ok);
            if (!acknowledged)
                continue;

            PlayerPrefs.SetInt(ConfirmedKey(board), score);
            if (PlayerPrefs.GetInt(key) == score)
                PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            LeaderboardCache.Refresh(board);
            if (board != FastestBoard)
                LeaderboardCache.Refresh(RichestBoard);
        }
        _flushing = false;
    }

    [Serializable]
    private class BoardResponse
    {
        public bool ok;
        public int userRank;
        public BoardEntry[] entries;
    }

    [Serializable]
    private class BoardEntry
    {
        public string playerId;
        public string name;
        public int score;
        public int rank;
    }

    [Serializable]
    private class ScoreRequest
    {
        public string playerId;
        public string name;
        public int score;
    }

    [Serializable]
    private class ScoreResponse
    {
        public bool ok;
        public bool accepted;
        public int best;
        public int rank;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Ensure()
    {
        if (Instance != null)
            return;

        var go = new GameObject("HttpLeaderboards");
        DontDestroyOnLoad(go);
        go.AddComponent<HttpLeaderboards>();
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

    public static string BoardName(LeaderboardKind kind)
    {
        switch (kind)
        {
            case LeaderboardKind.Fastest: return FastestBoard;
            case LeaderboardKind.Steady: return SteadyBoard;
            default: return SureBoard;
        }
    }

    public static LeaderboardKind KindFromBoard(string board)
    {
        if (board == FastestBoard)
            return LeaderboardKind.Fastest;
        if (board == SteadyBoard)
            return LeaderboardKind.Steady;
        return LeaderboardKind.Sure;
    }

    public void Load(LeaderboardKind kind, Action<YandexLeaderboards.BoardPage> callback)
    {
        StartCoroutine(LoadRoutine(BoardName(kind), callback));
    }

    public void LoadBoard(string board, Action<YandexLeaderboards.BoardPage> callback)
    {
        StartCoroutine(LoadRoutine(board, callback));
    }

    public void LoadRank(string board, Action<int> callback)
    {
        StartCoroutine(LoadRoutine(board, page => callback?.Invoke(page.Ok ? page.UserRank : 0)));
    }

    public IEnumerator Submit(string board, int score, string name, Action<bool> completed = null)
    {
        string nick = PlayerIdentity.SanitizeName(name);
        if (string.IsNullOrEmpty(nick))
        {
            Debug.LogWarning("HttpLeaderboards submit skipped: bad name");
            yield break;
        }

        var body = new ScoreRequest
        {
            playerId = PlayerIdentity.PlayerId,
            name = nick,
            score = score
        };

        string url = BaseUrl + "/boards/" + board + "/score";
        byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        using (UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("HttpLeaderboards submit failed: " + req.error + " " + req.downloadHandler?.text);
                yield break;
            }

            ScoreResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<ScoreResponse>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("HttpLeaderboards submit parse: " + e.Message);
            }

            if (parsed == null || !parsed.ok)
                Debug.LogWarning("HttpLeaderboards submit rejected for " + board);
            completed?.Invoke(parsed != null && parsed.ok);
        }
    }

    private IEnumerator LoadRoutine(string board, Action<YandexLeaderboards.BoardPage> callback)
    {
        string url = BaseUrl + "/boards/" + board + "?playerId=" + UnityWebRequest.EscapeURL(PlayerIdentity.PlayerId);
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("HttpLeaderboards load failed: " + req.error);
                callback?.Invoke(new YandexLeaderboards.BoardPage(false, 0, null));
                yield break;
            }

            callback?.Invoke(ParseBoard(req.downloadHandler.text));
        }
    }

    private static YandexLeaderboards.BoardPage ParseBoard(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new YandexLeaderboards.BoardPage(false, 0, null);

        BoardResponse parsed;
        try
        {
            parsed = JsonUtility.FromJson<BoardResponse>(json);
        }
        catch
        {
            return new YandexLeaderboards.BoardPage(false, 0, null);
        }

        if (parsed == null || !parsed.ok)
            return new YandexLeaderboards.BoardPage(false, 0, null);

        BoardEntry[] src = parsed.entries ?? Array.Empty<BoardEntry>();
        var entries = new YandexLeaderboards.Entry[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            BoardEntry e = src[i];
            entries[i] = new YandexLeaderboards.Entry(
                e != null ? e.rank : 0,
                e != null ? e.name : string.Empty,
                e != null ? e.score : 0,
                string.Empty);
        }

        return new YandexLeaderboards.BoardPage(true, parsed.userRank, entries);
    }
}
