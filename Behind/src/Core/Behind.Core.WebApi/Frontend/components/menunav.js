define(["ko", "models/menus", "text!components/menunav.html"], function (ko, menus, template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                var menu = menus.follow(params.name);

                return {
                    items: ko.pureComputed(function () {
                        return menu().items;
                    })
                };
            }
        },
        template: template
    };
});
