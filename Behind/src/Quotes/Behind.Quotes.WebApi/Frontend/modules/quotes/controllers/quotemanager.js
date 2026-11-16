define(["controllers/controller", "text!modules/quotes/views/quotemanager.html", "modules/quotes/viewmodels/quotemanager"], function (createController, view, QuoteManagerViewModel) {
    return createController(view, QuoteManagerViewModel);
});
