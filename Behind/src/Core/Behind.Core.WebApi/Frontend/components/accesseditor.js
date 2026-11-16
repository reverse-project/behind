define(["ko", "models/i18n", "text!components/accesseditor.html"], function (ko, i18n, template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return {
                    rows: params.access.rows,
                    choices: ko.pureComputed(function () {
                        return params.access.choices.map(function (choice) {
                            return { value: choice.value, label: i18n.t(choice.label) };
                        });
                    }),
                    enabled: ko.pureComputed(function () {
                        return !!ko.unwrap(params.enabled);
                    })
                };
            }
        },
        template: template
    };
});
