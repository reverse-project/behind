define(["models/i18n"], function (i18n) {
    var firstNameKey = /^(.*?)firstname$/i;

    var person = function (firstName, lastName) {
        return i18n.t("sport.personName", [firstName, lastName]).trim();
    };

    var isObject = function (value) {
        return value !== null && typeof value === "object";
    };

    var named = function (prefix, word) {
        return prefix === "" ? word : word.charAt(0).toUpperCase() + word.slice(1);
    };

    var partsOf = function (item) {
        return Object.keys(item)
            .map(function (key) {
                return { key: key, match: firstNameKey.exec(key) };
            })
            .filter(function (candidate) {
                return candidate.match !== null;
            })
            .map(function (candidate) {
                var prefix = candidate.match[1];

                return {
                    first: candidate.key,
                    last: prefix + named(prefix, "lastName"),
                    full: prefix + named(prefix, "name")
                };
            })
            .filter(function (parts) {
                return typeof item[parts.first] === "string" && typeof item[parts.last] === "string";
            });
    };

    var localize = function (value) {
        if (Array.isArray(value)) {
            return value.map(localize);
        }

        if (!isObject(value)) {
            return value;
        }

        var result = {};

        Object.keys(value).forEach(function (key) {
            result[key] = localize(value[key]);
        });

        partsOf(value).forEach(function (parts) {
            result[parts.full] = person(value[parts.first], value[parts.last]);
        });

        return result;
    };

    return {
        person: person,
        localize: localize
    };
});
