define(["ko"], function (ko) {
    return function FormViewModel() {
        var self = this;

        self.email = ko.observable("");
        self.password = ko.observable("");
        self.busy = ko.observable(false);
        self.message = ko.observable("");

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.run = function (action) {
            self.busy(true);

            return action()
                .then(function (result) {
                    self.message(result.message);
                    return result;
                })
                .finally(function () {
                    self.busy(false);
                });
        };
    };
});
