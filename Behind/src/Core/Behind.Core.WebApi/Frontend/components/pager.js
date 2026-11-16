define(["text!components/pager.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return params.pager;
            }
        },
        template: template
    };
});
