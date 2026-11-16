define(["ko", "models/contentlocale", "models/i18n", "models/session", "models/usersettings", "text!components/localeswitch.html"], function (ko, contentLocale, i18n, session, userSettings, template) {
    return {
        viewModel: function (params) {
            var self = this;
            var disposed = false;
            var sequence = 0;
            var views = ["view", "flag-only", "flag-and-text", "text-only"];
            var requestedView = ko.unwrap((params || {}).view);
            self.view = views.indexOf(requestedView) >= 0 ? requestedView : "view";
            var preferredLocale = function () {
                return contentLocale.defaultLocale([userSettings.locale(), i18n.remembered()]) || "";
            };
            self.value = ko.observable(contentLocale.current() || preferredLocale());
            self.busy = ko.observable(false);
            self.error = ko.observable("");
            self.loadFailed = contentLocale.loadFailed;
            self.options = ko.pureComputed(function () {
                return contentLocale.locales().map(function (locale) {
                    return { code: locale.code, name: locale.name, flag: locale.flag || locale.code };
                });
            });
            self.optionName = function (option) {
                return option.name;
            };
            self.optionLabel = function (option) {
                if (self.view === "flag-only") {
                    return option.flag;
                }
                if (self.view === "flag-and-text") {
                    return option.flag + " " + option.name;
                }
                return option.name;
            };
            self.selectLocale = function (option) {
                if (self.busy()) {
                    return;
                }
                self.value(option.code);
                return self.change();
            };
            var subscription = contentLocale.current.subscribe(function (value) {
                self.value(value || preferredLocale());
            });
            self.change = function () {
                var desired = self.value();
                var ticket = ++sequence;
                self.busy(true);
                self.error("");
                var save = session.isSignedIn()
                        ? userSettings.saveLocale(desired).then(function (result) {
                            if (!result.ok) {
                                throw new Error(result.message || i18n.t("contentLocale.error"));
                            }
                        })
                        : i18n.choose(desired);
                return save.then(function () {
                    if (!disposed && ticket === sequence) {
                        contentLocale.current(desired);
                    }
                }).catch(function (failure) {
                    if (!disposed && ticket === sequence) {
                        self.value(contentLocale.current() || preferredLocale());
                        self.error(failure.message || i18n.t("contentLocale.error"));
                    }
                }).finally(function () {
                    if (!disposed && ticket === sequence) {
                        self.busy(false);
                    }
                });
            };
            self.dispose = function () {
                disposed = true;
                sequence++;
                subscription.dispose();
                self.options.dispose();
            };
        },
        template: template
    };
});
