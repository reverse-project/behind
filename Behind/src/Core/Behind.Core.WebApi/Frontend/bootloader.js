require(["config"], function () {
    require(["jquery", "ko", "domReady!"], function () {
        require(["app"], function (App) {
            window.App = App;
            window.App.start();
        });
    });
});
