define(["ko", "modules/forum/models/bbcode", "models/i18n", "bindings/richeditor"], function (ko, bbcode, i18n) {
    var attachLinks = function (element, selector, attribute) {
        var open = function (event) {
            var link = event.target.closest(selector);
            if (!link || !element.contains(link)) {
                return;
            }
            if (event.type === "keydown" && event.key !== "Enter") {
                return;
            }
            event.preventDefault();
            event.stopPropagation();
            if (event.type === "auxclick" && event.button !== 1) {
                return;
            }
            var url = link.getAttribute(attribute);
            if (window.confirm(i18n.t("forum.openLink") + "\n\n" + url)) {
                window.open(url, "_blank", "noopener,noreferrer");
            }
        };
        ["click", "auxclick", "keydown"].forEach(function (name) {
            element.addEventListener(name, open, true);
        });
        ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
            ["click", "auxclick", "keydown"].forEach(function (name) {
                element.removeEventListener(name, open, true);
            });
        });
    };
    ko.bindingHandlers.bbcode = {
        init: function (element) {
            attachLinks(element, "a[data-forum-url]", "data-forum-url");
        },
        update: function (element, valueAccessor) {
            element.innerHTML = bbcode.render(ko.unwrap(valueAccessor()));
            element.querySelectorAll("a[href]").forEach(function (link) {
                link.setAttribute("data-forum-url", link.getAttribute("href"));
                link.setAttribute("title", link.getAttribute("href"));
                link.removeAttribute("href");
                link.setAttribute("role", "link");
                link.setAttribute("tabindex", "0");
            });
        }
    };
    ko.bindingHandlers.bbcodeEditor = {
        init: function (element, valueAccessor) {
            var options = valueAccessor();
            var value = ko.pureComputed({
                read: function () { return bbcode.render(options.value()); },
                write: function (html) { options.value(bbcode.fromHtml(html)); }
            });
            var editorOptions = {
                value: value,
                enabled: options.enabled,
                linkTargets: false,
                formats: ["bold", "italic", "underline", "strike", "blockquote", "code-block", "list", "link"],
                toolbar: [
                    ["bold", "italic", "underline", "strike"],
                    ["blockquote", "code-block"],
                    [{ list: "ordered" }, { list: "bullet" }],
                    ["link", "clean"]
                ]
            };
            ko.utils.domData.set(element, "bbcodeEditor", editorOptions);
            ko.bindingHandlers.richEditor.init(element, function () { return editorOptions; });
            attachLinks(element, "a[href]", "href");
            var input = element.querySelector(".ql-editor");
            input.setAttribute("role", "textbox");
            input.setAttribute("aria-multiline", "true");
            input.setAttribute("aria-labelledby", options.labelId);
            ko.utils.domNodeDisposal.addDisposeCallback(element, function () { value.dispose(); });
        },
        update: function (element) {
            ko.bindingHandlers.richEditor.update(element, function () {
                return ko.utils.domData.get(element, "bbcodeEditor");
            });
        }
    };
});
