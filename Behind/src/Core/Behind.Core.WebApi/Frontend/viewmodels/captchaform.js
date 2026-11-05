define(["viewmodels/form", "viewmodels/captcha"], function (FormViewModel, CaptchaViewModel) {
    return function CaptchaFormViewModel() {
        var self = this;
        FormViewModel.call(self);

        var run = self.run;

        self.captcha = new CaptchaViewModel();

        self.run = function (action) {
            return run(function () {
                return action(self.captcha.response());
            }).finally(self.captcha.refresh);
        };

        self.activate = function () {
            return self.captcha.refresh();
        };
    };
});
