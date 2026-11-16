define(["controllers/controller", "text!views/menus.html", "viewmodels/menus"], function (createController, view, MenusViewModel) {
    return createController(view, MenusViewModel);
});
