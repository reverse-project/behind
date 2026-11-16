define(["ko", "models/http", "models/results"], function (ko, http, results) {
    var localeKey = "locale";
    var values = ko.observable({});
    var outcome = results.create({});

    var failures = {
        400: "usersettings.invalid",
        401: results.sessionExpired,
        404: "usersettings.unknown"
    };
    var withValue = function (current, key, value) {
        var next = Object.assign({}, current);

        if (value === "") {
            delete next[key];
        } else {
            next[key] = value;
        }

        return next;
    };

    return {
        values: values,
        locale: ko.pureComputed(function () {
            return values()[localeKey] || "";
        }),

        load: function () {
            return http.get("/profile/settings").then(function (response) {
                var loaded = response.ok && response.data && typeof response.data === "object" ? response.data : {};

                values(loaded);

                return { ok: response.ok };
            });
        },

        clear: function () {
            values({});

            return Promise.resolve({ ok: true });
        },

        save: function (key, value) {
            return http.put("/profile/settings/" + encodeURIComponent(key), { value: value }).then(function (response) {
                if (response.ok) {
                    values(withValue(values(), key, value));
                }

                return outcome.saved(response, "", failures);
            });
        },

        saveLocale: function (locale) {
            return this.save(localeKey, locale);
        }
    };
});
