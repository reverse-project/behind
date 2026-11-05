define(["text!components/twofactorchallenge.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return params.challenge;
            }
        },
        template: template
    };
});
