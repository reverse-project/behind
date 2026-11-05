define(["ko", "models/textexport", "models/i18n"], function (ko, textexport, i18n) {
    var fileName = "recovery-codes.txt";

    return function RecoveryCodesViewModel(params) {
        var self = this;

        self.codes = params.codes;
        self.done = params.done;
        self.doneLabel = params.doneLabel;
        self.status = ko.observable("");

        self.hasStatus = ko.pureComputed(function () {
            return self.status() !== "";
        });

        var text = function () {
            return self.codes().join("\n") + "\n";
        };

        self.copy = function () {
            return textexport.copy(text()).then(function (copied) {
                self.status(i18n.t(copied ? "twoFactor.codes.copied" : "twoFactor.codes.copyFailed"));
            });
        };

        self.download = function () {
            textexport.download(fileName, text());
        };
    };
});
