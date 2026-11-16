define(["text!modules/sport/components/browser.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return params.browser;
            }
        },
        template: template
    };
});
