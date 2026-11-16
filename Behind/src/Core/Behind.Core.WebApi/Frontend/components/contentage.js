define(["ko", "models/http", "models/session", "models/i18n", "text!components/contentage.html"], function (ko, http, session, i18n, template) {
    return {
        viewModel: function (params) {
            var self = this;
            var disposed = false;
            var sequence = 0;
            self.open = ko.observable(false);
            self.busy = ko.observable(false);
            self.minimumAge = ko.observable(0);
            self.options = ko.observableArray([]);
            self.enabled = ko.observable(false);
            self.message = ko.observable("");
            self.loadedId = ko.observable(null);
            self.loadedType = ko.observable(null);
            self.canManage = ko.pureComputed(function () {
                return !!ko.unwrap(params.id) && session.isSignedIn()
                    && session.hasGrant("ContentRestriction.Write")
                    && (params.allowed === undefined || ko.unwrap(params.allowed));
            });
            self.canSave = ko.pureComputed(function () {
                return self.canManage() && !self.busy() && self.loadedId() === ko.unwrap(params.id)
                    && self.loadedType() === ko.unwrap(params.entityType)
                    && self.options().some(function (option) { return option.age === Number(self.minimumAge()); });
            });
            self.toggle = function () {
                var ticket = ++sequence;
                self.open(!self.open());
                if (!self.open()) {
                    self.busy(false);
                    return Promise.resolve();
                }
                var entityType = ko.unwrap(params.entityType);
                var id = ko.unwrap(params.id);
                self.loadedId(null);
                self.loadedType(null);
                self.busy(true);
                return Promise.all([
                    http.get("/content-restrictions/" + encodeURIComponent(entityType) + "/" + encodeURIComponent(id)),
                    http.get("/content-age-options")
                ]).then(function (responses) {
                    if (disposed || ticket !== sequence || !self.open() || id !== ko.unwrap(params.id) || entityType !== ko.unwrap(params.entityType)) {
                        return;
                    }
                    if (!responses[0].ok || !responses[1].ok) {
                        self.message(i18n.t("contentRestrictions.failed"));
                        return;
                    }
                    var currentAge = responses[0].data.minimumAge;
                    var options = [{ age: 0, label: i18n.t("contentAgeOptions.unrestricted") }].concat(
                        responses[1].data.items.map(function (item) {
                            return { age: item.age, label: i18n.t("contentAgeOptions.optionLabel", [item.name, item.age]) };
                        }));
                    if (currentAge > 0 && !options.some(function (item) { return item.age === currentAge; })) {
                        options.push({ age: currentAge, label: i18n.t("contentAgeOptions.removedOption", [currentAge]), removed: true });
                    }
                    self.options(options);
                    self.minimumAge(currentAge);
                    self.enabled(responses[0].data.enabled);
                    self.loadedId(id);
                    self.loadedType(entityType);
                    self.message("");
                }).catch(function () {
                    if (!disposed && ticket === sequence) {
                        self.message(i18n.t("contentRestrictions.failed"));
                    }
                }).finally(function () {
                    if (!disposed && ticket === sequence) {
                        self.busy(false);
                    }
                });
            };
            self.save = function () {
                if (!self.canSave()) {
                    return Promise.resolve();
                }
                var ticket = ++sequence;
                var age = Number(self.minimumAge());
                self.busy(true);
                return http.post("/content-restrictions", {
                    entityType: ko.unwrap(params.entityType),
                    entityId: ko.unwrap(params.id),
                    minimumAge: age
                }).then(function (response) {
                    if (!disposed && ticket === sequence) {
                        if (response.ok) {
                            self.options(self.options().filter(function (option) { return !option.removed || option.age === age; }));
                        }
                        self.message(i18n.t(response.ok ? "contentRestrictions.saved" : "contentRestrictions.failed"));
                    }
                }).catch(function () {
                    if (!disposed && ticket === sequence) {
                        self.message(i18n.t("contentRestrictions.failed"));
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
                self.canManage.dispose();
                self.canSave.dispose();
            };
        },
        template: template
    };
});
