define(["ko", "viewmodels/footer", "text!components/sitefooter.html"], function (ko, FooterViewModel, template) {
    var createViewModel = function () {
        var viewModel = new FooterViewModel();
        viewModel.activate().catch(function () {});
        return viewModel;
    };

    var mount = function (element, params) {
        params = params || {};
        if (!(element.childNodes || element.children).length) {
            element.innerHTML = params.template || template;
        }
        var viewModel = createViewModel();
        ko.applyBindings(viewModel, element);
        ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
            viewModel.dispose();
        });
    };

    return {
        viewModel: {
            createViewModel: createViewModel
        },
        template: template,
        mount: mount
    };
});
