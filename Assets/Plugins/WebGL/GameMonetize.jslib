mergeIntoSingleFile: true

var GameMonetizeLib = {
    GameMonetize_IsAvailable__proxy: "sync",
    GameMonetize_IsAvailable: function () {
        return (typeof sdk !== "undefined" && sdk && typeof sdk.showBanner === "function") ? 1 : 0;
    },

    GameMonetize_ShowBanner__proxy: "sync",
    GameMonetize_ShowBanner: function () {
        try {
            if (typeof sdk !== "undefined" && sdk && typeof sdk.showBanner === "function")
                sdk.showBanner();
        } catch (e) {
            console.warn("GameMonetize showBanner failed", e);
        }
    }
};

mergeInto(LibraryManager.library, GameMonetizeLib);
