define(["ko"], function (ko) {
    return function createDetail(load, apply) {
        var detail = {
            loading: ko.observable(true),
            message: ko.observable("")
        };

        detail.found = ko.pureComputed(function () {
            return !detail.loading() && detail.message() === "";
        });
        detail.missing = ko.pureComputed(function () {
            return !detail.loading() && detail.message() !== "";
        });

        detail.activate = function () {
            return load()
                .then(function (result) {
                    detail.message(result.message);

                    if (result.ok) {
                        apply(result.data);
                    }
                })
                .finally(function () {
                    detail.loading(false);
                });
        };

        return detail;
    };
});
