define(function () {
    var maxLength = 100;

    var normalize = function (text) {
        return String(text || "")
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, "-");
    };

    var trim = function (text) {
        return text.replace(/^-+|-+$/g, "");
    };

    return {
        maxLength: maxLength,
        create: function (text) {
            return trim(trim(normalize(text)).substring(0, maxLength));
        },
        clean: function (text) {
            return normalize(text).substring(0, maxLength);
        }
    };
});
