define(["text!components/captcha.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return params.captcha;
            }
        },
        template: template
    };
});
