define(["models/modules"], function (modules) {
    var core = [
        { key: "core.header", name: "widgets.header" },
        { key: "core.content", name: "widgets.content" },
        { key: "core.footer", name: "widgets.footer" },
        { key: "core.news", name: "widgets.news" },
        { key: "core.articles", name: "widgets.articles" },
        { key: "core.downloads", name: "widgets.downloads" },
        { key: "core.menu-horizontal", name: "widgets.menuHorizontal" },
        { key: "core.menu-vertical", name: "widgets.menuVertical" },
        { key: "core.custom", name: "widgets.custom" },
        { key: "core.chat", name: "widgets.chat" },
        { key: "core.message", name: "widgets.message" }
    ];

    return {
        catalog: function () {
            return core.concat(modules.widgets());
        }
    };
});
