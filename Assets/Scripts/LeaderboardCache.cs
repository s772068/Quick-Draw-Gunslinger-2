using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Loads the main-menu boards once per page load, before the menu is shown.
/// </summary>
public class LeaderboardCache : MonoBehaviour
{
    public static bool IsReady { get; private set; }
    public static event Action Ready;

    private static readonly Dictionary<string, YandexLeaderboards.BoardPage> Pages =
        new Dictionary<string, YandexLeaderboards.BoardPage>();

    private static bool _bootStarted;
    private const float LoadTimeoutSeconds = 8f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (_bootStarted || IsReady)
            return;

        _bootStarted = true;
        var go = new GameObject("LeaderboardCache");
        DontDestroyOnLoad(go);
        go.AddComponent<LeaderboardCache>();
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void HideUnityLoadingBar();
#endif

    private void Start()
    {
        StartCoroutine(Prefetch());
    }

    public static bool TryGet(string board, out YandexLeaderboards.BoardPage page)
    {
        if (string.IsNullOrEmpty(board))
        {
            page = default;
            return false;
        }

        return Pages.TryGetValue(board, out page);
    }

    private IEnumerator Prefetch()
    {
        float waitForApi = 0f;
        while (HttpLeaderboards.Instance == null && waitForApi < 2f)
        {
            waitForApi += Time.unscaledDeltaTime;
            yield return null;
        }

        if (HttpLeaderboards.Instance == null)
        {
            Debug.LogWarning("Leaderboards API missing; opening the menu without boards.");
            MarkReady();
            yield break;
        }

        string[] boards =
        {
            LevelSystem.CityBoardId(0),
            LevelSystem.CityBoardId(1),
            LevelSystem.CityBoardId(2),
            HttpLeaderboards.RichestBoard,
            HttpLeaderboards.FastestBoard
        };

        int pending = 0;
        for (int i = 0; i < boards.Length; i++)
        {
            string id = boards[i];
            if (Pages.ContainsKey(id))
                continue;

            pending++;
            HttpLeaderboards.Instance.LoadBoard(id, page =>
            {
                Pages[id] = page;
                pending--;
            });
        }

        float wait = 0f;
        while (pending > 0 && wait < LoadTimeoutSeconds)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (pending > 0)
            Debug.LogWarning("Leaderboards timed out; opening the menu with what arrived.");

        MarkReady();
    }

    private static void MarkReady()
    {
        if (IsReady)
            return;

        IsReady = true;
        Ready?.Invoke();
        HideLoadingBar();
    }

    private static void HideLoadingBar()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            HideUnityLoadingBar();
        }
        catch (Exception e)
        {
            Debug.LogWarning("HideUnityLoadingBar failed: " + e.Message);
        }
#endif
    }
}
