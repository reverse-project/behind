define(["models/i18n"], function (i18n) {
    var unknown = "fa-file";

    var icons = {
        "fa-file-pdf": ["pdf"],
        "fa-file-word": ["doc", "docx", "odt", "rtf"],
        "fa-file-excel": ["xls", "xlsx", "ods"],
        "fa-file-csv": ["csv", "tsv"],
        "fa-file-powerpoint": ["ppt", "pptx", "odp"],
        "fa-file-zipper": ["zip", "rar", "7z", "tar", "gz", "tgz", "bz2", "xz"],
        "fa-file-lines": ["txt", "md", "log"],
        "fa-file-code": ["js", "json", "xml", "html", "css", "cs", "py", "java", "sh", "yml", "yaml", "sql"],
        "fa-file-image": ["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "ico", "tif", "tiff"],
        "fa-file-audio": ["mp3", "wav", "ogg", "flac", "m4a", "aac"],
        "fa-file-video": ["mp4", "webm", "mkv", "mov", "avi", "wmv"]
    };

    var byExtension = {};

    Object.keys(icons).forEach(function (icon) {
        icons[icon].forEach(function (extension) {
            byExtension[extension] = icon;
        });
    });

    var byContentType = [
        { prefix: "image/", icon: "fa-file-image" },
        { prefix: "audio/", icon: "fa-file-audio" },
        { prefix: "video/", icon: "fa-file-video" }
    ];

    var units = ["downloads.unit.b", "downloads.unit.kb", "downloads.unit.mb", "downloads.unit.gb", "downloads.unit.tb"];

    var extensionOf = function (fileName) {
        var name = String(fileName || "");
        var dot = name.lastIndexOf(".");

        return dot < 0 ? "" : name.substring(dot + 1).toLowerCase();
    };

    return {
        unknown: unknown,

        iconOf: function (fileName, contentType) {
            var known = byExtension[extensionOf(fileName)];

            if (known) {
                return known;
            }

            var type = String(contentType || "").toLowerCase();
            var matched = byContentType.filter(function (candidate) {
                return type.indexOf(candidate.prefix) === 0;
            })[0];

            return matched ? matched.icon : unknown;
        },

        sizeOf: function (bytes) {
            var value = Number(bytes) || 0;
            var unit = 0;

            while (value >= 1024 && unit < units.length - 1) {
                value /= 1024;
                unit++;
            }

            var rounded = unit === 0 || value >= 10 ? Math.round(value) : Math.round(value * 10) / 10;

            return rounded.toLocaleString(i18n.locale()) + " " + i18n.t(units[unit]);
        }
    };
});
