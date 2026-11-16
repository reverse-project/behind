define(["text!components/folderbrowser.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return {
                    roots: params.roots,
                    rootLinks: params.rootLinks,
                    hasFolders: params.hasFolders,
                    label: params.label,
                    emptyLabel: params.emptyLabel,
                    selectFolder: params.selectFolder || null,
                    selectRoot: function (item) {
                        if (item.select) { item.select(); return false; }
                        return true;
                    }
                };
            }
        },
        template: template
    };
});
