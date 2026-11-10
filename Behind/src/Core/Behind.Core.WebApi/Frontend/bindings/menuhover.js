define(["jquery", "ko", "bootstrap"], function ($, ko, bootstrap) {
    var namespace = ".menuhover";
    var hoverCapable = window.matchMedia("(min-width: 768px) and (hover: hover)");

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
