define(["controllers/controller", "text!views/menuitems.html", "viewmodels/menuitems"], function (createController, view, MenuItemsViewModel) {
    return createController(view, MenuItemsViewModel);
});
