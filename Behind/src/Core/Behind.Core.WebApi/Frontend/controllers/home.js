define(["jquery", "controllers/index", "controllers/site", "models/sites"], function ($, IndexController, SiteController, sites) {
    return function HomeController(context) {
        var self = this;

        self.exec = function () {
            return sites.loadIndex().then(function (result) {
                var Controller = result.ok ? SiteController : IndexController;
                var options = result.ok ? $.extend({}, context, { params: {}, loaded: result }) : context;

                new Controller(options).exec();
            });
        };
    };
});
