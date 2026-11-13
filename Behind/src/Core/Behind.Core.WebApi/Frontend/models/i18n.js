define(["ko", "models/http", "models/modules"], function (ko, http, modules) {
    var fallbackLocale = "en-US";
    var locale = ko.observable(fallbackLocale);
    var messages = ko.observable({});
    var browserKey = "behind.locale";
    var account = ko.observable("");
    var content = "";
    var siteDefault = "";
    var applied = null;
    var pending = Promise.resolve();
    var ticket = 0;

    var format = function (template, args) {
        return template.replace(/\{(\d+)\}/g, function (placeholder, index) {
            var value = args[Number(index)];

            return value === undefined || value === null ? placeholder : String(value);
        });
    };

    var lookup = function (key, args) {
        var template = messages()[key];

        return typeof template === "string" ? format(template, args || []) : null;
    };

    var translate = function (key, args) {
        var found = lookup(key, args);

        return found === null ? key : found;
    };

    var translateOr = function (key, fallback, args) {
        var found = lookup(key, args);

        return found === null ? fallback : found;
    };

    var formatDate = function (value, format) {
        return value ? format(new Date(value), locale()) : "";
    };

    var fetchBundle = function (url) {
        return http.get(url).then(function (response) {
            var data = response.data;

            return response.ok && data && typeof data.messages === "object" ? data.messages : null;
        });
    };

    var fetchModuleMessages = function (code) {
        return Promise.all(modules.localeUrls(code).map(fetchBundle)).then(function (bundles) {
            return Object.assign.apply(null, [{}].concat(bundles.filter(function (bundle) {
                return bundle !== null;
            })));
        });
    };

    var fetchMessages = function (code) {
        return Promise.all([
            fetchBundle("/locales/" + encodeURIComponent(code) + ".json"),
            fetchModuleMessages(code)
        ]).then(function (loaded) {
            return loaded[0] === null ? null : Object.assign({}, loaded[1], loaded[0]);
        });
    };

    var load = function (code) {
        var current = ++ticket;
        var requested = code || fallbackLocale;
        var specific = requested === fallbackLocale ? Promise.resolve(null) : fetchMessages(requested);

        return Promise.all([fetchMessages(fallbackLocale), specific]).then(function (loaded) {
            if (current !== ticket) {
                return;
            }

            var resolved = loaded[1] !== null || requested === fallbackLocale;

            messages(Object.assign({}, loaded[0], loaded[1]));
            locale(resolved ? requested : fallbackLocale);
            document.documentElement.setAttribute("lang", locale());
        });
    };

    var remembered = function () {
        try {
            return window.localStorage.getItem(browserKey) || "";
        } catch (error) {
            return "";
        }
    };

    var remember = function (code) {
        try {
            if (code) {
                window.localStorage.setItem(browserKey, code);
            } else {
                window.localStorage.removeItem(browserKey);
            }
        } catch (error) {
            return;
        }
    };

    var preferred = function () {
        return content || account() || remembered() || siteDefault || fallbackLocale;
    };

    var apply = function () {
        var wanted = preferred();

        if (wanted !== applied) {
            applied = wanted;
            pending = load(wanted);
        }

        return pending;
    };

    account.subscribe(apply);

    var describe = function (value) {
        var unwrapped = ko.unwrap(value);

        return typeof unwrapped === "string"
            ? { key: unwrapped, args: [] }
            : { key: ko.unwrap(unwrapped.key), args: (ko.unwrap(unwrapped.args) || []).map(ko.unwrap) };
    };

    ko.bindingHandlers.t = {
        update: function (element, valueAccessor) {
            var request = describe(valueAccessor());
            var found = lookup(request.key, request.args);

            if (found !== null) {
                element.textContent = found;
            }
        }
    };

    ko.bindingHandlers.tAttr = {
        update: function (element, valueAccessor) {
            var attributes = ko.unwrap(valueAccessor());

            Object.keys(attributes).forEach(function (name) {
                var request = describe(attributes[name]);
                var found = lookup(request.key, request.args);

                if (found !== null) {
                    element.setAttribute(name, found);
                }
            });
        }
    };

    return {
        fallbackLocale: fallbackLocale,
        locale: locale,
        remembered: remembered,
        apply: apply,
        useDefault: function (code) {
            siteDefault = code || "";
        },
        useAccount: function (code) {
            account(code || "");
        },
        useContent: function (code) {
            content = code || "";
            return apply();
        },
        choose: function (code) {
            remember(code);

            return apply();
        },
        t: translate,
        tOr: translateOr,
        banReason: function (reason) {
            return reason === "Misconduct" ? translate("wallofshame.reason.misconduct") : reason;
        },
        dateTime: function (value) {
            return formatDate(value, function (date, code) {
                return date.toLocaleString(code);
            });
        },
        date: function (value) {
            return formatDate(value, function (date, code) {
                return date.toLocaleDateString(code);
            });
        }
    };
});
