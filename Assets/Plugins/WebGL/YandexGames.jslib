mergeIntoSingleFile: true

var YandexGamesLib = {

    $YandexGamesState: {
        unityGameObject: "YandexGamesSdk",
        pauseCallback: "OnYandexPause",
        resumeCallback: "OnYandexResume"
    },

    YandexGames_IsAvailable__proxy: "sync",
    YandexGames_IsAvailable: function () {
        return (typeof window !== "undefined" && window.ysdk) ? 1 : 0;
    },

    YandexGames_GetLanguage__proxy: "sync",
    YandexGames_GetLanguage: function (buffer, bufferSize) {
        var lang = "en";
        try {
            if (window.ysdk && window.ysdk.environment && window.ysdk.environment.i18n && window.ysdk.environment.i18n.lang) {
                lang = String(window.ysdk.environment.i18n.lang);
            } else if (window.__yandexLang) {
                lang = String(window.__yandexLang);
            }
        } catch (e) {}
        stringToUTF8(lang, buffer, bufferSize);
    },

    YandexGames_LoadingReady__proxy: "sync",
    YandexGames_LoadingReady: function () {
        try {
            if (window.ysdk && window.ysdk.features && window.ysdk.features.LoadingAPI && typeof window.ysdk.features.LoadingAPI.ready === "function") {
                window.ysdk.features.LoadingAPI.ready();
            }
        } catch (e) {
            console.warn("Yandex LoadingAPI.ready failed", e);
        }
    },

    YandexGames_GameplayStart__proxy: "sync",
    YandexGames_GameplayStart: function () {
        try {
            if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI && typeof window.ysdk.features.GameplayAPI.start === "function") {
                window.ysdk.features.GameplayAPI.start();
            }
        } catch (e) {
            console.warn("Yandex GameplayAPI.start failed", e);
        }
    },

    YandexGames_GameplayStop__proxy: "sync",
    YandexGames_GameplayStop: function () {
        try {
            if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI && typeof window.ysdk.features.GameplayAPI.stop === "function") {
                window.ysdk.features.GameplayAPI.stop();
            }
        } catch (e) {
            console.warn("Yandex GameplayAPI.stop failed", e);
        }
    },

    YandexGames_BindUnityTarget__proxy: "sync",
    YandexGames_BindUnityTarget: function (gameObjectPtr, pauseMethodPtr, resumeMethodPtr) {
        YandexGamesState.unityGameObject = UTF8ToString(gameObjectPtr);
        YandexGamesState.pauseCallback = UTF8ToString(pauseMethodPtr);
        YandexGamesState.resumeCallback = UTF8ToString(resumeMethodPtr);

        if (!window.ysdk || typeof window.ysdk.on !== "function")
            return;

        try {
            window.ysdk.on("game_api_pause", function () {
                if (typeof SendMessage === "function") {
                    SendMessage(YandexGamesState.unityGameObject, YandexGamesState.pauseCallback);
                }
            });
            window.ysdk.on("game_api_resume", function () {
                if (typeof SendMessage === "function") {
                    SendMessage(YandexGamesState.unityGameObject, YandexGamesState.resumeCallback);
                }
            });
        } catch (e) {
            console.warn("Yandex pause/resume bind failed", e);
        }
    }
};

autoAddDeps(YandexGamesLib, "$YandexGamesState");
mergeInto(LibraryManager.library, YandexGamesLib);
