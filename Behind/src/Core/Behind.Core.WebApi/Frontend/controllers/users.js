define(["controllers/controller", "text!views/users.html", "viewmodels/users"], function (createController, view, UsersViewModel) {
    return createController(view, UsersViewModel);
});
