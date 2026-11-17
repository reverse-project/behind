define(["controllers/controller", "text!views/schedules.html", "viewmodels/schedules"], function (createController, view, SchedulesViewModel) {
    return createController(view, SchedulesViewModel);
});
