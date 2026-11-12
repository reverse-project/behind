define(["ko", "viewmodels/header", "text!components/siteheader.html"], function (ko, HeaderViewModel, template) {
    var createViewModel = function (params, componentInfo) {
        if (componentInfo) {
            componentInfo.element.setAttribute("data-hamburger-position", ko.unwrap((params || {}).hamburgerPosition) || "default");
        }
        var viewModel = new HeaderViewModel((params || {}).context);
        viewModel.localeSwitch = (params || {}).showLocaleSwitch !== false;
        viewModel.localeSwitchView = ko.unwrap((params || {}).localeSwitchView) || "view";
        return viewModel;
    };

    var updateCollapseIds = function (element, id) {
        if (!element.querySelectorAll) {
            return;
        }

        var collapses = Array.prototype.slice.call(element.querySelectorAll(".navbar-collapse[id]"));
        var toggles = Array.prototype.slice.call(element.querySelectorAll("[data-bs-toggle='collapse']"));
        collapses.forEach(function (collapse, index) {
            var oldId = collapse.id;
            var newId = id + (index ? "-" + index : "");
            collapse.id = newId;
            toggles.forEach(function (toggle) {
                if (toggle.getAttribute("data-bs-target") === "#" + oldId) {
                    toggle.setAttribute("data-bs-target", "#" + newId);
                    toggle.setAttribute("aria-controls", newId);
                }
            });
        });
    };

    var mount = function (element, params) {
        params = params || {};
        if (!(element.childNodes || element.children).length) {
            element.innerHTML = params.template || template;
        }
        var viewModel = createViewModel(params, { element: element });
        if (params.template) {
            updateCollapseIds(element, viewModel.navigationId);
        }
        ko.applyBindings(viewModel, element);
        ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
            viewModel.dispose();
        });
    };

    return {
        viewModel: {
            createViewModel: function (params, componentInfo) {
                return createViewModel(params, componentInfo);
            }
        },
        template: template,
        mount: mount
    };
});
