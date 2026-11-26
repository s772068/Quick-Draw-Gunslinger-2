using System.Runtime.InteropServices;

public static class PlatformGameplay
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CrazyGames_SetGameplayActive(int active);

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeCrazyGames()
    {
        CrazyGames_SetGameplayActive(0);
    }
#endif

    public static void Start()
    {
        YandexGamesSdk.GameplayStart();
#if UNITY_WEBGL && !UNITY_EDITOR
        CrazyGames_SetGameplayActive(1);
#endif
    }

    public static void Stop()
    {
        YandexGamesSdk.GameplayStop();
#if UNITY_WEBGL && !UNITY_EDITOR
        CrazyGames_SetGameplayActive(0);
#endif
    }
}
