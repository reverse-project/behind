define(["controllers/controller", "text!views/rawdatabase.html", "viewmodels/rawdatabase"], function (createController, view, RawDatabaseViewModel) {
    return createController(view, RawDatabaseViewModel);
});
