define(["text!components/privaterecipients.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return params;
            }
        },
        template: template
    };
});
