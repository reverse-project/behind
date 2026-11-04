define(["ko", "models/about"], function (ko, about) {
    var title = ko.observable("Behind");
    var copyright = ko.observable("");
    var locale = ko.observable("");

    return {
        title: title,
        copyright: copyright,
        locale: locale,

        load: function () {
            return about.loadSite().then(function (site) {
                if (site.title) {
                    title(site.title);
                }

                copyright(site.copyright || "");
                locale(site.defaultLocale || "");
            });
        }
    };
});
