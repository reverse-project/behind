define(["ko", "models/auth", "models/pager", "models/i18n"], function (ko, auth, createPager, i18n) {
    var pageSize = 10;

    var present = function (item) {
        return {
            name: ko.pureComputed(function () {
                return (item.displayName || "").trim() || i18n.t("wallofshame.anonymous");
            }),
            reason: item.reason,
            expiresAt: ko.pureComputed(function () {
                return i18n.dateTime(item.expiresAt);
            }),
            isTemporary: !!item.expiresAt
        };
    };

    return function WallOfShameViewModel() {
        var self = this;

        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.entries = ko.observableArray([]);
        self.pager = createPager(function () {
            return self.refresh();
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.isEmpty = ko.pureComputed(function () {
            return self.entries().length === 0 && !self.busy();
        });

        self.refresh = function () {
            self.busy(true);

            return auth.loadWallOfShame({ page: self.pager.page(), pageSize: pageSize })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.entries([]);
                        return undefined;
                    }

                    if (self.pager.isOutOfRange(result.data)) {
                        self.pager.clampIfOutOfRange(result.data);
                        return self.refresh();
                    }

                    self.pager.update(result.data);
                    self.entries(result.data.items.map(present));
                    return undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.activate = function () {
            return self.refresh();
        };
    };
});
