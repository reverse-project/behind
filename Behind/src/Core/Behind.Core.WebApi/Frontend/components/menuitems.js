define(["jquery", "bootstrap", "text!components/menuitems.html", "bindings/menuhover"], function ($, bootstrap, template) {
    // Collapse the responsive navbar after a menu item is chosen.
    $(document).on("click.menuitems", "#navigation a, #navigation .dropdown-item", function () {
        var navigation = document.getElementById("navigation");

        if (navigation && navigation.classList.contains("show")) {
            bootstrap.Collapse.getOrCreateInstance(navigation, { toggle: false }).hide();
        }
    });

    var variants = {
        nav: { list: "nav", item: "nav-item", link: "nav-link link-secondary" },
        navbar: { list: "navbar-nav mb-2 mb-md-0", item: "nav-item", link: "nav-link" },
        dropdown: { list: "list-unstyled mb-0", item: "", link: "dropdown-item" },
        vertical: { list: "list-unstyled mb-0 menu-vertical", item: "", link: "dropdown-item text-wrap" }
    };

    return {
        viewModel: {
            createViewModel: function (params) {
                var variant = variants[params.variant] || variants.nav;

                return {
                    items: params.items,
                    isDropdown: params.variant === "dropdown",
                    isVertical: params.variant === "vertical",
                    hover: params.hover === true,
                    listClass: variant.list,
                    itemClass: variant.item,
                    linkClass: variant.link
                };
            }
        },
        template: template
    };
});
