define(["ko", "models/i18n", "modules/markdownviewer/models/markdownviewer"], function (ko, i18n, markdownviewer) {
    return function DocsViewModel(context) {
        var self = this;
        var requested = context && context.params && context.params.path ? context.params.path : null;
        var explicitLocale = context && context.params && context.params.locale ? context.params.locale : null;

        var wantedLocale = function () {
            return explicitLocale || i18n.locale();
        };

        self.message = ko.observable("");
        self.configured = ko.observable(true);
        self.title = ko.observable("");
        self.entries = ko.observableArray([]);
        self.sections = ko.observableArray([]);
        self.homeHref = ko.observable(null);
        self.homeActive = ko.pureComputed(function () {
            return self.current() !== null && self.homeHref() === markdownviewer.documentRoute(self.current().path, explicitLocale);
        });
        self.current = ko.observable(null);
        self.listLocale = ko.observable(null);

        self.hasEntries = ko.pureComputed(function () {
            return self.entries().length > 0;
        });
        self.html = ko.pureComputed(function () {
            return self.current() ? self.current().html : "";
        });
        self.shownLocale = ko.pureComputed(function () {
            return self.current() && self.current().locale ? self.current().locale : self.listLocale();
        });
        self.fallbackNotice = ko.pureComputed(function () {
            var shown = self.shownLocale();

            return shown && shown.toLowerCase() !== wantedLocale().toLowerCase()
                ? i18n.t("markdownviewer.fallback", [shown])
                : "";
        });

        var isCurrent = function (path) {
            return ko.pureComputed(function () {
                return path !== null && self.current() !== null && self.current().path === path;
            });
        };

        var presentEntry = function (entry, general) {
            var depth = Math.max(entry.path.split("/").length - (general ? 1 : 2), 0);

            return {
                title: entry.title,
                href: markdownviewer.documentRoute(entry.path, explicitLocale),
                indent: depth + "rem",
                active: isCurrent(entry.path)
            };
        };

        var presentSection = function (section) {
            var general = section.title === "";

            return {
                title: section.title,
                hasTitle: !general,
                href: section.path ? markdownviewer.documentRoute(section.path, explicitLocale) : null,
                active: isCurrent(section.path),
                entries: section.entries.map(function (entry) {
                    return presentEntry(entry, general);
                })
            };
        };

        var choose = function (data) {
            var paths = data.entries.map(function (entry) {
                return entry.path;
            });

            if (requested !== null) {
                return requested;
            }

            return paths.indexOf(data.defaultDocument) >= 0 ? data.defaultDocument : paths[0];
        };

        var open = function (path) {
            return markdownviewer.page(path, wantedLocale()).then(function (result) {
                self.message(result.message);
                self.current(result.ok ? result.data : null);
            });
        };

        self.follow = function (data, event) {
            var anchor = event.target.closest ? event.target.closest("a") : null;
            var href = anchor ? anchor.getAttribute("href") : null;

            if (href === null || href.charAt(0) !== "#" || href.charAt(1) === "/") {
                return true;
            }

            var target = document.getElementById(decodeURIComponent(href.substring(1)));

            if (target) {
                target.scrollIntoView();
            }

            return false;
        };

        var load = function () {
            return markdownviewer.list(wantedLocale()).then(function (result) {
                self.message(result.message);

                if (!result.ok) {
                    return Promise.resolve();
                }

                self.configured(result.data.configured);
                self.listLocale(result.data.locale);
                self.title(result.data.title);
                self.entries(result.data.entries);
                self.sections(result.data.sections.map(presentSection));
                self.homeHref(result.data.home ? markdownviewer.documentRoute(result.data.home, explicitLocale) : null);

                if (result.data.entries.length === 0) {
                    self.current(null);

                    return Promise.resolve();
                }

                return open(choose(result.data));
            });
        };

        self.activate = function () {
            i18n.locale.subscribe(load);

            return load();
        };
    };
});
