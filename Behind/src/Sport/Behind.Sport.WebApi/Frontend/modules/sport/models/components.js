define(["ko"], function (ko) {
    var components = {
        "sport-link": "modules/sport/components/link",
        "sport-thumbnail": "modules/sport/components/thumbnail",
        "sport-leaderboard": "modules/sport/components/leaderboard",
        "sport-stats": "modules/sport/components/stats",
        "sport-browser": "modules/sport/components/browser"
    };

    Object.keys(components).forEach(function (name) {
        if (!ko.components.isRegistered(name)) {
            ko.components.register(name, { require: components[name] });
        }
    });

    return components;
});
