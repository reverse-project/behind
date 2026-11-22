define(["controllers/controller", "text!views/pinproof.html"], function (createController, view) {
    return createController(view, function (context) {
        this.code = context.params.pinProof || "";
        this.activate = function () {
            context.scrub("#/profile/pin-proof");
        };
    });
});
