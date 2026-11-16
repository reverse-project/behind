define(["controllers/controller", "text!views/reports.html", "viewmodels/reports", "bindings/bbcode"], function (createController, view, ViewModel) {
    return createController(view, ViewModel);
});
