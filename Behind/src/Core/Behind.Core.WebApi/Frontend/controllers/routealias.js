define(["models/routealiases"], function (routeAliases) {
    return function RouteAliasController(context) {
        var self = this;

        self.exec = function () {
            return routeAliases.resolve(context.params.slug).then(function (result) {
                if (!result.ok || !context.follow(result.data.target)) {
                    context.fallback(context.params);
                }
            });
        };
    };
});
