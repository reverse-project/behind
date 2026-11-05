define(["ko", "models/captcha"], function (ko, captcha) {
    return function CaptchaViewModel() {
        var self = this;
        var id = "";

        self.image = ko.observable("");
        self.answer = ko.observable("");
        self.loading = ko.observable(false);

        self.hasImage = ko.pureComputed(function () {
            return self.image() !== "";
        });

        self.response = function () {
            return { id: id, answer: self.answer() };
        };

        self.refresh = function () {
            self.loading(true);
            self.answer("");

            return captcha.load()
                .then(function (challenge) {
                    id = challenge.id;
                    self.image(challenge.image);
                })
                .finally(function () {
                    self.loading(false);
                });
        };
    };
});
