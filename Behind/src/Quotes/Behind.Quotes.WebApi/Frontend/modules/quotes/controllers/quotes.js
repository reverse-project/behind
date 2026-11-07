define(["controllers/controller", "text!modules/quotes/views/quotes.html", "modules/quotes/viewmodels/quotes"], function (createController, view, QuotesViewModel) {
    return createController(view, QuotesViewModel);
});
