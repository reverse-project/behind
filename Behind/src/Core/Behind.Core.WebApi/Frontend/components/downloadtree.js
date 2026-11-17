define(["text!components/downloadtree.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return {
                    nodes: params.nodes,
                    selectFolder: params.selectFolder,
                    select: function (node) {
                        if (params.selectFolder) { params.selectFolder(node); return false; }
                        return true;
                    }
                };
            }
        },
        template: template
    };
});
