define(["controllers/controller", "text!views/licenses.html", "viewmodels/licenses"], function (createController, view, LicensesViewModel) {
    return createController(view, LicensesViewModel);
});
