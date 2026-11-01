define(["ko"], function (ko) {
    var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ#".split("");

    return function createLetterPager(onChange) {
        var pager = {
            selected: ko.observable(""),
            available: ko.observableArray([])
        };

        pager.letters = ko.pureComputed(function () {
            var present = pager.available();
            var selected = pager.selected();

            return alphabet.map(function (letter) {
                return {
                    label: letter,
                    value: letter,
                    isCurrent: letter === selected,
                    isAvailable: present.indexOf(letter) >= 0 || letter === selected
                };
            });
        });
        pager.isAll = ko.pureComputed(function () {
            return pager.selected() === "";
        });
        pager.select = function (item) {
            if (item.value === pager.selected()) {
                return Promise.resolve();
            }

            pager.selected(item.value);
            return onChange();
        };
        pager.all = function () {
            return pager.select({ value: "" });
        };
        pager.update = function (letters) {
            pager.available(letters);
        };

        return pager;
    };
});
