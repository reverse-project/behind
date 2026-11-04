define(["ko", "models/i18n"], function (ko, i18n) {
    var defaultVisiblePages = 5;

    return function createPager(onChange, visiblePages) {
        var size = visiblePages || defaultVisiblePages;
        var pager = {
            page: ko.observable(1),
            pageCount: ko.observable(0),
            total: ko.observable(0)
        };

        var moveTo = function (target) {
            var bounded = Math.min(Math.max(target, 1), Math.max(pager.pageCount(), 1));

            if (bounded === pager.page()) {
                return Promise.resolve();
            }

            pager.page(bounded);
            return onChange();
        };

        pager.hasItems = ko.pureComputed(function () {
            return pager.total() > 0;
        });
        pager.hasPrevious = ko.pureComputed(function () {
            return pager.page() > 1;
        });
        pager.hasNext = ko.pureComputed(function () {
            return pager.page() < pager.pageCount();
        });
        pager.pages = ko.pureComputed(function () {
            var count = pager.pageCount();
            var start = Math.max(1, Math.min(pager.page() - Math.floor(size / 2), count - size + 1));
            var end = Math.min(count, start + size - 1);
            var numbers = [];

            for (var number = start; number <= end; number++) {
                numbers.push({ number: number, isCurrent: number === pager.page() });
            }

            return numbers;
        });
        pager.label = ko.pureComputed(function () {
            return i18n.t("pager.summary", [pager.page(), Math.max(pager.pageCount(), 1), pager.total()]);
        });
        pager.first = function () {
            return moveTo(1);
        };
        pager.previous = function () {
            return moveTo(pager.page() - 1);
        };
        pager.next = function () {
            return moveTo(pager.page() + 1);
        };
        pager.last = function () {
            return moveTo(pager.pageCount());
        };
        pager.goTo = function (target) {
            return moveTo(target.number);
        };
        pager.update = function (data) {
            pager.pageCount(data.pageCount);
            pager.total(data.total);
            pager.page(data.page);
        };
        pager.isOutOfRange = function (data) {
            return data.page > Math.max(data.pageCount, 1);
        };
        pager.clampIfOutOfRange = function (data) {
            if (pager.isOutOfRange(data)) {
                pager.page(Math.max(data.pageCount, 1));
            }
        };

        return pager;
    };
});
