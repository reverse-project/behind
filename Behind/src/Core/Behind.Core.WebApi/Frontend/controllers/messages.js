define(["controllers/controller", "text!views/messages.html", "viewmodels/messages"], function (createController, view, MessagesViewModel) {
    return createController(view, MessagesViewModel);
});
