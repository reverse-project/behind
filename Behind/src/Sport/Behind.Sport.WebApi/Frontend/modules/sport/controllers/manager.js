define(["controllers/controller", "text!modules/sport/views/manager.html", "modules/sport/viewmodels/manager"], function (createController, view, ManagerViewModel) {
    return createController(view, ManagerViewModel);
});
