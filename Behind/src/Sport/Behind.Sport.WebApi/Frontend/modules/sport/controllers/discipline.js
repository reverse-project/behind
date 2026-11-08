define(["controllers/controller", "text!modules/sport/views/discipline.html", "modules/sport/viewmodels/discipline"], function (createController, view, DisciplineViewModel) {
    return createController(view, DisciplineViewModel);
});
