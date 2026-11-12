define(["ko", "models/http", "models/session", "models/contentlocale", "models/i18n", "text!components/contentlocale.html"], function (ko, http, session, contentLocale, i18n, template) {
    return {
        viewModel: function (params) {
            var self = this;
            var disposed = false;
            var sequence = 0;
            self.open = ko.observable(false);
            self.busy = ko.observable(false);
            self.value = ko.observable("");
            self.message = ko.observable("");
            self.loadFailed = contentLocale.loadFailed;
            self.loadedId = ko.observable(null);
            self.loadedType = ko.observable(null);
            self.canManage = ko.pureComputed(function () {
                return !!ko.unwrap(params.id) && session.isSignedIn() && (params.allowed === undefined || ko.unwrap(params.allowed));
            });
            self.options = ko.pureComputed(function () {
                var parents = ["SiteSection", "Carousel", "CarouselSlide", "GalleryItem", "MenuItem", "Download", "DownloadFolder", "News", "Article", "ForumBoard", "ForumPost", "Team", "Player", "Position", "StatType", "Season", "PlayerSeason", "Stat"];
                var label = parents.indexOf(ko.unwrap(params.entityType)) >= 0 ? "contentLocale.inherit" : "contentLocale.neutral";
                return [{ code: "", name: i18n.t(label) }].concat(contentLocale.locales());
            });
            self.canSave = ko.pureComputed(function () {
                return !self.busy() && !self.loadFailed() && self.loadedId() === ko.unwrap(params.id) && self.loadedType() === ko.unwrap(params.entityType);
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
                return http.get("/content-locales/" + encodeURIComponent(entityType) + "/" + encodeURIComponent(id))
                    .then(function (response) {
                        if (!disposed && ticket === sequence && self.open() && id === ko.unwrap(params.id) && entityType === ko.unwrap(params.entityType)) {
                            self.value(response.ok && response.data.locale || "");
                            self.loadedId(response.ok ? id : null);
                            self.loadedType(response.ok ? entityType : null);
                            self.message(response.ok ? "" : i18n.t("contentLocale.error"));
                        }
                    }).catch(function () {
                        if (!disposed && ticket === sequence) {
                            self.message(i18n.t("contentLocale.error"));
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
                self.busy(true);
                return http.post("/content-locales", {
                    entityType: ko.unwrap(params.entityType),
                    entityId: ko.unwrap(params.id),
                    locale: self.value() || null
                }).then(function (response) {
                    if (!disposed && ticket === sequence) {
                        if (response.ok) {
                            contentLocale.assignmentRevision(contentLocale.assignmentRevision() + 1);
                        }
                        self.message(i18n.t(response.ok ? "contentLocale.saved" : "contentLocale.error"));
                    }
                }).catch(function () {
                    if (!disposed && ticket === sequence) {
                        self.message(i18n.t("contentLocale.error"));
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
                self.options.dispose();
                self.canSave.dispose();
            };
        },
        template: template
    };
});
