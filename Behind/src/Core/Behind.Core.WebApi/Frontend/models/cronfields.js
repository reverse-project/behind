define(function () {
    var names = ["minute", "hour", "dayOfMonth", "month", "dayOfWeek"];

    var parseField = function (value) {
        if (value === "*") {
            return { mode: "every", value: "" };
        }
        if (/^\*\/\d+$/.test(value)) {
            return { mode: "interval", value: value.slice(2) };
        }
        if (/^\d+(?:,\d+)+$/.test(value)) {
            return { mode: "specific", value: value };
        }
        if (/^\d+-\d+$/.test(value)) {
            return { mode: "range", value: value };
        }
        if (/^\d+$/.test(value)) {
            return { mode: "specific", value: value };
        }
        return null;
    };

    var parse = function (expression) {
        var fields = String(expression || "").trim().split(/\s+/);

        if (fields.length !== 5) {
            return null;
        }

        var parsed = {};
        for (var index = 0; index < names.length; index++) {
            var field = parseField(fields[index]);
            if (!field) {
                return null;
            }
            parsed[names[index]] = field;
        }
        return parsed;
    };

    var compose = function (fields) {
        return names.map(function (name) {
            var field = fields[name];
            if (field.mode === "every") {
                return "*";
            }
            if (field.mode === "interval") {
                return "*/" + String(field.value).trim();
            }
            return String(field.value).trim();
        }).join(" ");
    };

    return { names: names, parse: parse, compose: compose };
});
