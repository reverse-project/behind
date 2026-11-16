define(["controllers/controller", "text!modules/contact/views/contact.html", "modules/contact/viewmodels/contact"], function (createController, view, ViewModel) {
    return createController(view, ViewModel);
});
