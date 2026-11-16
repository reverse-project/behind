define(["controllers/controller", "text!modules/contact/views/contactmanager.html", "modules/contact/viewmodels/contactmanager"], function (createController, view, ViewModel) {
    return createController(view, ViewModel);
});
