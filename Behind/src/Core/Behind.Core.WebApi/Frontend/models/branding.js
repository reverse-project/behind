define(["ko", "models/about"], function (ko, about) {
    var title = ko.observable("Behind");
    var copyright = ko.observable("");

    return {
        title: title,
        copyright: copyright,

        load: function () {
            return about.loadSite().then(function (site) {
                if (site.title) {
                    title(site.title);
                }

                copyright(site.copyright || "");
            });
        }
    };
});
