define(["text!components/menuitems.html"], function (template) {
    var variants = {
        nav: { list: "nav", item: "nav-item", link: "nav-link link-secondary" },
        navbar: { list: "navbar-nav mb-2 mb-md-0", item: "nav-item", link: "nav-link" },
        dropdown: { list: "list-unstyled mb-0", item: "", link: "dropdown-item" }
    };

    return {
        viewModel: {
            createViewModel: function (params) {
                var variant = variants[params.variant] || variants.nav;

                return {
                    items: params.items,
                    isDropdown: params.variant === "dropdown",
                    listClass: variant.list,
                    itemClass: variant.item,
                    linkClass: variant.link
                };
            }
        },
        template: template
    };
});
