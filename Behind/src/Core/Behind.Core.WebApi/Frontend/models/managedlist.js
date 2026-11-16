define(["ko", "models/session", "models/pager", "models/letterpager"], function (ko, session, createPager, createLetterPager) {
    var pageSize = 10;

    return function createManagedList(config) {
        var list = {
            busy: ko.observable(false),
            message: ko.observable(""),
            search: ko.observable(""),
            showDeleted: ko.observable(false),
            items: ko.observableArray([])
        };

        list.pager = createPager(function () {
            return list.refresh();
        });
        list.letterPager = createLetterPager(function () {
            list.pager.page(1);
            return list.refresh();
        });
        list.canViewDeleted = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        list.hasMessage = ko.pureComputed(function () {
            return list.message() !== "";
        });
        list.hasItems = ko.pureComputed(function () {
            return list.items().length > 0;
        });

        list.showDeleted.subscribe(function () {
            list.letterPager.selected("");
            list.pager.page(1);
            list.refresh();
        });

        list.refresh = function () {
            if (!config.allowed()) {
                list.items([]);
                return Promise.resolve();
            }

            list.busy(true);

            return config.load({
                page: list.pager.page(),
                pageSize: pageSize,
                search: list.search().trim(),
                letter: list.letterPager.selected(),
                deleted: list.showDeleted()
            })
                .then(function (result) {
                    list.message(result.message);

                    if (!result.ok) {
                        list.items([]);
                        return undefined;
                    }

                    var page = config.pageOf(result.data);

                    list.letterPager.update(result.data.letters);

                    if (list.pager.isOutOfRange(page)) {
                        list.pager.clampIfOutOfRange(page);
                        return list.refresh();
                    }

                    list.pager.update(page);
                    list.items(page.items.map(function (item) {
                        return config.present(item, result.data);
                    }));

                    return undefined;
                })
                .finally(function () {
                    list.busy(false);
                });
        };

        list.find = function () {
            list.pager.page(1);
            return list.refresh();
        };

        list.perform = function (action) {
            list.busy(true);

            return action()
                .then(function (result) {
                    return (result.ok ? list.refresh() : Promise.resolve()).then(function () {
                        list.message(result.message);
                    });
                })
                .finally(function () {
                    list.busy(false);
                });
        };

        list.activate = function () {
            list.canViewDeleted.subscribe(function (allowed) {
                if (!allowed && list.showDeleted()) {
                    list.showDeleted(false);
                }
            });

            config.allowed.subscribe(function (allowed) {
                if (allowed) {
                    list.refresh();
                }
            });

            return list.refresh();
        };

        return list;
    };
});
