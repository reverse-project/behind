define(["jquery", "ko", "bootstrap"], function ($, ko, bootstrap) {
    var namespace = ".menuhover";
    var hoverMedia = window.matchMedia("(hover: hover)");
    var hoverCapable = {
        // Hover menus only apply when the navbar is expanded, i.e. its toggler is hidden.
        get matches() {
            if (!hoverMedia.matches) {
                return false;
            }

            var toggler = document.querySelector(".navbar-toggler");
            return !toggler || toggler.offsetParent === null;
        }
    };

    var dropdownOf = function ($parent) {
        return bootstrap.Dropdown.getOrCreateInstance($parent.find(".menu-parent-toggle")[0]);
    };

    ko.bindingHandlers.menuHover = {
        init: function (element, valueAccessor) {
            if (!ko.unwrap(valueAccessor())) {
                return;
            }

            var $parent = $(element);

            $parent.on("mouseenter" + namespace, function () {
                if (hoverCapable.matches) {
                    dropdownOf($parent).show();
                }
            });

            $parent.on("mouseleave" + namespace, function () {
                if (hoverCapable.matches) {
                    dropdownOf($parent).hide();
                }
            });

            ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
                $parent.off(namespace);
            });
        }
    };
});
