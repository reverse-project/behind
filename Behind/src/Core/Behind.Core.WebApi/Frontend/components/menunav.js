define(["ko", "models/menus", "text!components/menunav.html"], function (ko, menus, template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                var menu = menus.follow(params.name);
                var items = ko.pureComputed(function () {
                    return menu().items;
                });

                return {
                    items: items,
                    dispose: function () {
                        items.dispose();
                        menu.dispose();
                    }
                };
            }
        },
        template: template
    };
});
