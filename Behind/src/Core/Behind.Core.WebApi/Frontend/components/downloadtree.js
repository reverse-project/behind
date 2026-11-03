define(["text!components/downloadtree.html"], function (template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                return { nodes: params.nodes };
            }
        },
        template: template
    };
});
