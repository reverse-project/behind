define(["ko", "quill", "models/i18n", "bindings/richeditorimages"], function (ko, Quill, i18n, images) {
    images.register();

    var toolbar = [
        [{ header: [1, 2, 3, false] }],
        ["bold", "italic", "underline", "strike"],
        [{ list: "ordered" }, { list: "bullet" }],
        [{ indent: "-1" }, { indent: "+1" }],
        [{ align: [] }],
        ["blockquote", "code-block"],
        ["link", "image"],
        ["clean"]
    ];

    var toolbarLabels = [
        [".ql-header", "richeditor.heading"],
        ["button.ql-bold", "richeditor.bold"],
        ["button.ql-italic", "richeditor.italic"],
        ["button.ql-underline", "richeditor.underline"],
        ["button.ql-strike", "richeditor.strike"],
        ["button.ql-list[value=\"ordered\"]", "richeditor.orderedList"],
        ["button.ql-list[value=\"bullet\"]", "richeditor.bulletList"],
        ["button.ql-indent[value=\"-1\"]", "richeditor.outdent"],
        ["button.ql-indent[value=\"+1\"]", "richeditor.indent"],
        [".ql-align", "richeditor.align"],
        ["button.ql-blockquote", "richeditor.blockquote"],
        ["button.ql-code-block", "richeditor.codeBlock"],
        ["button.ql-link", "richeditor.link"],
        ["button.ql-image", "richeditor.image"],
        ["button.ql-clean", "richeditor.clean"]
    ];

    var imageTypes = ["image/png", "image/jpeg", "image/gif", "image/webp"];

    var labelToolbar = function (container) {
        toolbarLabels.forEach(function (entry) {
            var label = i18n.t(entry[1]);

            Array.prototype.forEach.call(container.querySelectorAll(entry[0]), function (control) {
                control.setAttribute("title", label);
                control.setAttribute("aria-label", label);
            });
        });
    };

    var isEmpty = function (quill) {
        return quill.getText().trim() === "" && !quill.root.querySelector("img");
    };

    var read = function (quill) {
        return isEmpty(quill) ? "" : quill.getSemanticHTML();
    };

    var insertImages = function (quill, upload, index, files) {
        return Array.prototype.reduce.call(files, function (chain, file) {
            return chain.then(function (position) {
                return upload(file).then(function (url) {
                    if (!url) {
                        return position;
                    }

                    quill.insertEmbed(position, "image", url, "user");

                    return position + 1;
                });
            });
        }, Promise.resolve(index));
    };

    var pickImage = function (quill, upload) {
        var input = document.createElement("input");

        input.type = "file";
        input.accept = imageTypes.join(",");
        input.addEventListener("change", function () {
            var range = quill.getSelection(true);

            insertImages(quill, upload, range.index, input.files);
        });
        input.click();
    };

    ko.bindingHandlers.richEditor = {
        init: function (element, valueAccessor) {
            var options = valueAccessor();
            var upload = options.upload;
            var writing = false;

            var quill = new Quill(element, {
                theme: "snow",
                modules: {
                    toolbar: {
                        container: toolbar,
                        handlers: {
                            image: function () {
                                pickImage(quill, upload);
                            }
                        }
                    },
                    uploader: {
                        mimetypes: imageTypes,
                        handler: function (range, files) {
                            insertImages(quill, upload, range.index, files);
                        }
                    }
                }
            });

            images.attach(quill);

            var labels = ko.computed(function () {
                labelToolbar(quill.getModule("toolbar").container);
            });

            ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
                labels.dispose();
            });

            quill.on("text-change", function () {
                writing = true;
                options.value(read(quill));
                writing = false;
            });

            ko.utils.domData.set(element, "richEditor", {
                quill: quill,
                isWriting: function () {
                    return writing;
                }
            });
        },
        update: function (element, valueAccessor) {
            var options = valueAccessor();
            var editor = ko.utils.domData.get(element, "richEditor");
            var value = ko.unwrap(options.value) || "";

            editor.quill.enable(ko.unwrap(options.enabled) !== false);

            if (!editor.isWriting() && value !== read(editor.quill)) {
                editor.quill.setContents(editor.quill.clipboard.convert({ html: value }), "silent");
            }
        }
    };
});
