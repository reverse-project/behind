define(["text!components/carouseleditor.html", "viewmodels/carouseleditor", "bindings/carousel"], function (template, CarouselEditorViewModel) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return new CarouselEditorViewModel(params);
            }
        },
        template: template
    };
});
