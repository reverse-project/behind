define(function () {
    return function (view, ViewModel) {
        return function Controller(context) {
            var self = this;

            self.view = view;
            self.viewModel = new ViewModel(context);
            self.exec = function () {
                context.renderView(self.view, self.viewModel);

                if (typeof self.viewModel.activate === "function") {
                    self.viewModel.activate();
                }
            };
        };
    };
});
