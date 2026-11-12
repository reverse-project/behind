define(["ko", "models/http", "models/session", "models/i18n", "text!components/contentage.html"], function (ko, http, session, i18n, template) {
    return {
        viewModel: function (params) {
            var self = this;
            self.open = ko.observable(false);
            self.busy = ko.observable(false);
            self.minimumAge = ko.observable(0);
            self.enabled = ko.observable(false);
            self.message = ko.observable("");
            self.canManage = ko.pureComputed(function () {
                return !!ko.unwrap(params.id) && session.hasGrant("RawDatabase.Manage");
            });
            self.toggle = function () {
                self.open(!self.open());
                if (!self.open()) {
                    return Promise.resolve();
                }

                self.busy(true);
                return http.get("/content-restrictions/" + encodeURIComponent(ko.unwrap(params.entityType)) + "/" + encodeURIComponent(ko.unwrap(params.id)))
                    .then(function (response) {
                        if (response.ok) {
                            self.minimumAge(response.data.minimumAge);
                            self.enabled(response.data.enabled);
                            self.message("");
                        } else {
                            self.message(i18n.t("contentRestrictions.failed"));
                        }
                    }).finally(function () {
                        self.busy(false);
                    });
            };
            self.save = function () {
                var age = Number(self.minimumAge());
                if (!Number.isInteger(age) || age < 0 || age > 150 || self.busy()) {
                    self.message(i18n.t("contentRestrictions.failed"));
                    return Promise.resolve();
                }

                self.busy(true);
                return http.post("/content-restrictions", {
                    entityType: ko.unwrap(params.entityType),
                    entityId: ko.unwrap(params.id),
                    minimumAge: age
                }).then(function (response) {
                    self.message(i18n.t(response.ok ? "contentRestrictions.saved" : "contentRestrictions.failed"));
                }).finally(function () {
                    self.busy(false);
                });
            };
            self.dispose = function () {
                self.canManage.dispose();
            };
        },
        template: template
    };
});
