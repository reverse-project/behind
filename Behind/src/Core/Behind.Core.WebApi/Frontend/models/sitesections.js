define(["ko"], function (ko) {
    var themes = ["Normal", "Accented"];
    var positions = ["Left", "Right"];

    var present = function (data) {
        var hasImage = !!data.imageUrl;
        var classes = ["behind-section", "behind-section-" + data.theme.toLowerCase()];

        if (hasImage) {
            classes.push("behind-section-with-image", "behind-section-image-" + data.imagePosition.toLowerCase());
        }

        return {
            title: data.title,
            hasTitle: data.title !== "",
            content: data.content,
            hasContent: data.content !== "",
            imageUrl: data.imageUrl || "",
            imageAlt: data.imageAlt || "",
            hasImage: hasImage,
            cssClass: classes.join(" ")
        };
    };

    var editable = function (data) {
        var section = {
            theme: ko.observable(data.theme || themes[0]),
            title: ko.observable(data.title || ""),
            content: ko.observable(data.content || ""),
            imageId: ko.observable(data.imageId || null),
            imageUrl: ko.observable(data.imageUrl || ""),
            imageAlt: ko.observable(data.imageAlt || ""),
            imagePosition: ko.observable(data.imagePosition || positions[0])
        };

        section.hasImage = ko.pureComputed(function () {
            return section.imageId() !== null;
        });
        section.isFilled = ko.pureComputed(function () {
            return section.title().trim() !== "" || section.content() !== "" || section.hasImage();
        });
        section.isAccented = ko.pureComputed(function () {
            return section.theme() === "Accented";
        });
        section.setImage = function (image) {
            section.imageId(image.id);
            section.imageUrl(image.url);
        };
        section.clearImage = function () {
            section.imageId(null);
            section.imageUrl("");
            section.imageAlt("");
        };

        return section;
    };

    var toRequest = function (section) {
        return {
            theme: section.theme(),
            title: section.title().trim(),
            content: section.content(),
            imageId: section.imageId(),
            imageAlt: section.imageAlt().trim(),
            imagePosition: section.imagePosition()
        };
    };

    return {
        themes: themes,
        positions: positions,
        present: present,
        editable: editable,
        toRequest: toRequest
    };
});
