define(["controllers/controller", "text!views/scheduleruns.html", "viewmodels/scheduleruns"], function (createController, view, ScheduleRunsViewModel) {
    return createController(view, ScheduleRunsViewModel);
});
