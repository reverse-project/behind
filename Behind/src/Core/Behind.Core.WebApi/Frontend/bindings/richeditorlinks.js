define(["quill", "models/i18n"], function (Quill, i18n) {
    var newTab = "_blank";
    var formatName = "linkTarget";

    var BaseLink = Quill.import("formats/link");
    var parchment = Quill.import("parchment");
    var Delta = Quill.import("delta");

    class OptionalTargetLink extends BaseLink {
        static create(value) {
            var node = super.create(value);

            node.removeAttribute("target");

            return node;
        }
    }

    var register = function () {
        Quill.register("formats/link", OptionalTargetLink, true);
        Quill.register(new parchment.Attributor(formatName, "target", {
            scope: parchment.Scope.INLINE,
            whitelist: [newTab]
        }), true);
    };

    var attach = function (quill) {
        var tooltip = quill.theme.tooltip;

        if (!tooltip) {
            return;
        }

        var option = document.createElement("label");
        var checkbox = document.createElement("input");
        var text = document.createElement("span");

        option.className = "ql-link-target";
        checkbox.type = "checkbox";
        text.textContent = i18n.t("richeditor.linkNewTab");
        option.appendChild(checkbox);
        option.appendChild(text);
        tooltip.root.appendChild(option);

        var linkAt = function (index) {
            return quill.scroll.descendant(BaseLink, index)[0];
        };

        var opensInNewTab = function (link) {
            return !!link && link.domNode.getAttribute("target") === newTab;
        };

        var editedRange = null;

        quill.clipboard.addMatcher("a", function (node, delta) {
            return node.getAttribute("target") === newTab
                ? delta.compose(new Delta().retain(delta.length(), { [formatName]: newTab }))
                : delta;
        });

        checkbox.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                tooltip.save();
            }
        });

        var originalEdit = tooltip.edit.bind(tooltip);
        var originalSave = tooltip.save.bind(tooltip);

        tooltip.edit = function (mode, preview) {
            var range = tooltip.linkRange;

            editedRange = range || quill.getSelection();
            checkbox.checked = !!range && opensInNewTab(linkAt(range.index));
            originalEdit(mode, preview);
        };

        tooltip.save = function () {
            var range = editedRange;
            var value = checkbox.checked ? newTab : false;
            var isLink = tooltip.root.getAttribute("data-mode") === "link";

            originalSave();

            if (isLink && range) {
                quill.formatText(range.index, range.length, formatName, value, "user");
            }
        };
    };

    return { register: register, attach: attach };
});
