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
    },

    YandexGames_QueryAuth: function (gameObjectPtr, methodPtr) {
        var go = UTF8ToString(gameObjectPtr);
        var method = UTF8ToString(methodPtr);
        var ysdk = (typeof window !== "undefined") ? window.ysdk : null;
        if (!ysdk || typeof ysdk.getPlayer !== "function") {
            SendMessage(go, method, "0");
            return;
        }

        ysdk.getPlayer({ signed: false }).then(function (player) {
            var ok = player && typeof player.isAuthorized === "function" && player.isAuthorized();
            SendMessage(go, method, ok ? "1" : "0");
        }).catch(function () {
            SendMessage(go, method, "0");
        });
    },

    YandexGames_OpenAuth: function (gameObjectPtr, methodPtr) {
        var go = UTF8ToString(gameObjectPtr);
        var method = UTF8ToString(methodPtr);
        var ysdk = (typeof window !== "undefined") ? window.ysdk : null;
        if (!ysdk || !ysdk.auth || typeof ysdk.auth.openAuthDialog !== "function") {
            SendMessage(go, method, "0");
            return;
        }

        ysdk.auth.openAuthDialog().then(function () {
            SendMessage(go, method, "1");
        }).catch(function () {
            SendMessage(go, method, "0");
        });
    },

    YandexGames_SubmitScore: function (boardPtr, score, gameObjectPtr, okPtr, failPtr) {
        var board = UTF8ToString(boardPtr);
        var go = UTF8ToString(gameObjectPtr);
        var ok = UTF8ToString(okPtr);
        var fail = UTF8ToString(failPtr);
        var ysdk = (typeof window !== "undefined") ? window.ysdk : null;
        if (!ysdk || !ysdk.leaderboards || typeof ysdk.leaderboards.setScore !== "function") {
            SendMessage(go, fail, "no sdk");
            return;
        }

        ysdk.leaderboards.setScore(board, score).then(function () {
            SendMessage(go, ok, board);
        }).catch(function (e) {
            SendMessage(go, fail, e && e.message ? String(e.message) : "setScore failed");
        });
    },

    YandexGames_LoadEntries: function (boardPtr, gameObjectPtr, methodPtr) {
        var board = UTF8ToString(boardPtr);
        var go = UTF8ToString(gameObjectPtr);
        var method = UTF8ToString(methodPtr);
        var ysdk = (typeof window !== "undefined") ? window.ysdk : null;
        if (!ysdk || !ysdk.leaderboards || typeof ysdk.leaderboards.getEntries !== "function") {
            SendMessage(go, method, "fail");
            return;
        }

        function pack(page) {
            var lines = [];
            var userRank = page && page.userRank ? page.userRank : 0;
            lines.push(String(userRank));
            var entries = page && page.entries ? page.entries : [];
            for (var i = 0; i < entries.length; i++) {
                var e = entries[i];
                var name = "";
                var avatar = "";
                if (e.player) {
                    name = e.player.publicName ? String(e.player.publicName) : "";
                    try {
                        if (typeof e.player.getAvatarSrc === "function")
                            avatar = String(e.player.getAvatarSrc("small") || "");
                    } catch (err) {}
                }
                name = name.replace(/\|/g, " ").replace(/\n/g, " ");
                avatar = avatar.replace(/\|/g, " ").replace(/\n/g, " ");
                lines.push(String(e.rank || 0) + "|" + name + "|" + String(e.score || 0) + "|" + avatar);
            }
            return lines.join("\n");
        }

        function load(includeUser) {
            return ysdk.leaderboards.getEntries(board, {
                quantityTop: 3,
                includeUser: includeUser,
                quantityAround: 1
            });
        }

        load(true).catch(function () {
            return load(false);
        }).then(function (page) {
            SendMessage(go, method, pack(page));
        }).catch(function () {
            SendMessage(go, method, "fail");
        });
    }
};

autoAddDeps(YandexGamesLib, "$YandexGamesState");
mergeInto(LibraryManager.library, YandexGamesLib);
