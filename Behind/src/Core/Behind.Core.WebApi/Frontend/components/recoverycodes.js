define(["viewmodels/recoverycodes", "text!components/recoverycodes.html"], function (RecoveryCodesViewModel, template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return new RecoveryCodesViewModel(params);
            }
        },
        template: template
    };
});
