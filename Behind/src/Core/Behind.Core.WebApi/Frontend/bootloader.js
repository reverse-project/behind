require(["config"], function () {
    require(["jquery", "ko", "bootstrap", "domReady!"], function () {
        require(["app"], function (App) {
            window.App = App;
            window.App.start();
        });
    });
});
