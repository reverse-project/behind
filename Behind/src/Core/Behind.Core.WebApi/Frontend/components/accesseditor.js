define(["ko", "text!components/accesseditor.html"], function (ko, template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return {
                    rows: params.access.rows,
                    choices: params.access.choices,
                    enabled: ko.pureComputed(function () {
                        return !!ko.unwrap(params.enabled);
                    })
                };
            }
        },
        template: template
    };
});
