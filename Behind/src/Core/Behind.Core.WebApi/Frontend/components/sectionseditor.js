define(["text!components/sectionseditor.html", "viewmodels/sectionseditor"], function (template, SectionsEditorViewModel) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return new SectionsEditorViewModel(params);
            }
        },
        template: template
    };
});
