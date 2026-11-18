mergeIntoSingleFile: true

var BootLoadingLib = {
    HideUnityLoadingBar: function () {
        if (typeof window.hideUnityLoadingBar === "function")
            window.hideUnityLoadingBar();
    }
};

mergeInto(LibraryManager.library, BootLoadingLib);
