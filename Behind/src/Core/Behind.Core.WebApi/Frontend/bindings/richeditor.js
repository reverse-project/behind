define(["ko", "quill", "bindings/richeditorimages"], function (ko, Quill, images) {
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

    var imageTypes = ["image/png", "image/jpeg", "image/gif", "image/webp"];

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
