mergeInto(LibraryManager.library, {
    CrazyGames_SetGameplayActive: function (active) {
        var host = window.location.hostname;
        if (host !== "crazygames.com" && !host.endsWith(".crazygames.com"))
            return;

        var state = window.qdgCrazyGamesState;
        if (!state) {
            state = window.qdgCrazyGamesState = {
                game: null,
                active: false,
                reported: false,
                report: function () {
                    if (!this.game || this.active === this.reported)
                        return;
                    try {
                        if (this.active)
                            this.game.gameplayStart();
                        else
                            this.game.gameplayStop();
                        this.reported = this.active;
                    } catch (error) {
                        console.warn("CrazyGames gameplay event failed", error);
                    }
                }
            };

            var init = function () {
                Promise.resolve().then(function () {
                    var sdk = window.CrazyGames.SDK;
                    return sdk.init().then(function () {
                        if (sdk.environment !== "disabled") {
                            state.game = sdk.game;
                            state.report();
                        }
                    });
                }).catch(function (error) {
                    console.warn("CrazyGames SDK initialization failed", error);
                });
            };

            if (window.CrazyGames && window.CrazyGames.SDK) {
                init();
            } else {
                var script = document.createElement("script");
                script.src = "https://sdk.crazygames.com/crazygames-sdk-v3.js";
                script.onload = init;
                script.onerror = function () {
                    console.warn("CrazyGames SDK could not be loaded");
                };
                document.head.appendChild(script);
            }
        }

        state.active = active !== 0;
        state.report();
    }
});
