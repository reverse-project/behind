define(["ko", "models/settings"], function (ko, settings) {
    var commonSources = ["Behind", "Microsoft", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "System"];

    return function LoggingSettingsEditor(canEdit) {
        var self = this;

        self.loaded = ko.observable(false);
        self.levels = ko.observableArray([]);
        self.defaultLevel = ko.observable("");
        self.overrides = ko.observableArray([]);
        self.sources = commonSources;

        var presentOverride = function (source, level) {
            return { source: ko.observable(source), level: ko.observable(level) };
        };

        self.hasOverrides = ko.pureComputed(function () {
            return self.overrides().length > 0;
        });
        self.isValid = ko.pureComputed(function () {
            var names = self.overrides().map(function (item) {
                return item.source().trim().toLowerCase();
            });

            return names.every(function (name, index) {
                return name !== "" && names.indexOf(name) === index;
            });
        });
        self.canSave = ko.pureComputed(function () {
            return self.loaded() && canEdit() && self.isValid();
        });

        self.apply = function (data) {
            self.levels(data.levels);
            self.defaultLevel(data.default || "");
            self.overrides(data.overrides.map(function (item) {
                return presentOverride(item.source, item.level);
            }));
            self.loaded(true);
        };

        self.load = function (file) {
            return settings.loadLogging(file).then(function (result) {
                if (result.ok) {
                    self.apply(result.data);
                }

                return result;
            });
        };

        self.add = function () {
            self.overrides.push(presentOverride("", self.defaultLevel() || self.levels()[0]));
        };

        self.remove = function (item) {
            self.overrides.remove(item);
        };

        self.request = function () {
            return {
                default: self.defaultLevel(),
                overrides: self.overrides().map(function (item) {
                    return { source: item.source().trim(), level: item.level() };
                })
            };
        };

        self.save = function (file) {
            return settings.saveLogging(file, self.request());
        };
    };
});
