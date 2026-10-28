using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Routes leaderboards: Yandex SDK when available, else HTTP (CG / Editor / local WebGL).
/// </summary>
public static class PlatformLeaderboards
{
    public static bool UseHttp
    {
        get
        {
#if CRAZY_GAMES
            return true;
#else
            return !YandexGamesSdk.SdkAvailable;
#endif
        }
    }

    public static string SureBoard => UseHttp ? HttpLeaderboards.SureBoard : YandexLeaderboards.SureBoard;
    public static string FastestBoard => UseHttp ? HttpLeaderboards.FastestBoard : YandexLeaderboards.FastestBoard;
    public static string SteadyBoard => UseHttp ? HttpLeaderboards.SteadyBoard : YandexLeaderboards.SteadyBoard;

    public static bool IsTimeBoard(LeaderboardKind kind) => YandexLeaderboards.IsTimeBoard(kind);

    public static void Load(LeaderboardKind kind, Action<YandexLeaderboards.BoardPage> callback)
    {
        if (UseHttp)
        {
            if (HttpLeaderboards.Instance == null)
            {
                callback?.Invoke(new YandexLeaderboards.BoardPage(false, 0, null));
                return;
            }

            HttpLeaderboards.Instance.Load(kind, callback);
            return;
        }

        if (YandexLeaderboards.Instance == null)
        {
            callback?.Invoke(new YandexLeaderboards.BoardPage(false, 0, null));
            return;
        }

        YandexLeaderboards.Instance.Load(kind, callback);
    }

    public static void LoadRank(string board, Action<int> callback)
    {
        if (UseHttp)
        {
            if (HttpLeaderboards.Instance == null)
            {
                callback?.Invoke(0);
                return;
            }

            HttpLeaderboards.Instance.LoadRank(board, callback);
            return;
        }

        if (YandexLeaderboards.Instance == null)
        {
            callback?.Invoke(0);
            return;
        }

        YandexLeaderboards.Instance.LoadRank(board, callback);
    }

    public static IEnumerator Submit(string board, int score, string nickname)
    {
        if (UseHttp)
        {
            if (HttpLeaderboards.Instance == null)
                yield break;
            yield return HttpLeaderboards.Instance.Submit(board, score, nickname);
            yield break;
        }

        if (YandexLeaderboards.Instance == null)
            yield break;
        yield return YandexLeaderboards.Instance.Submit(board, score);
    }
}
