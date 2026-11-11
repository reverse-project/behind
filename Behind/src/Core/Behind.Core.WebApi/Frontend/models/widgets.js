define(["models/modules"], function (modules) {
    var core = [
        { key: "core.content", name: "widgets.content" },
        { key: "core.news", name: "widgets.news" },
        { key: "core.articles", name: "widgets.articles" },
        { key: "core.downloads", name: "widgets.downloads" },
        { key: "core.menu-horizontal", name: "widgets.menuHorizontal" },
        { key: "core.menu-vertical", name: "widgets.menuVertical" },
        { key: "core.custom", name: "widgets.custom" },
        { key: "core.chat", name: "widgets.chat" }
    ];

    return {
        catalog: function () {
            return core.concat(modules.widgets());
        }
    };
});
