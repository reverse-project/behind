define(["ko", "text!modules/contact/views/contactwidget.html", "modules/contact/viewmodels/contact"], function (ko, view, ContactViewModel) {
    if (!ko.components.isRegistered("contact-form")) {
        ko.components.register("contact-form", {
            viewModel: function () {
                var viewModel = new ContactViewModel();
                viewModel.activate();
                return viewModel;
            },
            template: view
        });
    }

    return {
        load: function () {
            return {
                component: {
                    name: "contact-form",
                    params: {}
                }
            };
        }
    };
});
