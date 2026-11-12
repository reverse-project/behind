define(["ko", "models/http", "models/i18n", "models/contentlocale", "models/session"], function (ko, http, i18n, contentLocale, session) {
    var pending = [];
    var scheduled = false;
    var cache = new Map();
    var cacheRevision = -1;
    var cacheToken;

    var flush = function () {
        scheduled = false;
        var batch = pending.splice(0, 100);
        if (pending.length) {
            scheduled = true;
            Promise.resolve().then(flush);
        }
        var unique = new Map();
        batch.forEach(function (item) {
            unique.set(item.type + ":" + item.id, { entityType: item.type, entityId: item.id });
        });
        return http.post("/content-locales/query", {
            items: Array.from(unique.values())
        }).then(function (response) {
            batch.forEach(function (item) {
                var match = response.ok && response.data.items.find(function (result) {
                    return result.entityType === item.type && result.entityId === item.id;
                });
                if (match && match.status === 200) {
                    item.resolve(match);
                } else {
                    item.reject(new Error("contentLocale.error"));
                }
            });
        }).catch(function (error) {
            batch.forEach(function (item) { item.reject(error); });
        });
    };

    var lookup = function (type, id, revision, token) {
        if (revision !== cacheRevision || token !== cacheToken) {
            cache.clear();
            cacheRevision = revision;
            cacheToken = token;
        }
        var key = type + ":" + id;
        if (!cache.has(key)) {
            var promise = new Promise(function (resolve, reject) {
                pending.push({ type: type, id: id, resolve: resolve, reject: reject });
                if (!scheduled) {
                    scheduled = true;
                    Promise.resolve().then(flush);
                }
            });
            cache.set(key, promise);
            promise.catch(function () {
                if (cache.get(key) === promise) {
                    cache.delete(key);
                }
            });
        }
        return cache.get(key);
    };

    ko.bindingHandlers.contentLocaleScope = {
        init: function (element, valueAccessor) {
            var disposed = false;
            var sequence = 0;
            var warning = document.createElement(element.tagName.toLowerCase() === "tr" ? "td" : "div");
            warning.className = "text-danger small";
            warning.setAttribute("role", "alert");
            warning.hidden = true;
            element.insertBefore(warning, element.firstChild);
            var request = ko.computed(function () {
                var options = valueAccessor();
                ko.unwrap(options.revision);
                var revision = contentLocale.assignmentRevision();
                var token = session.accessToken();
                var id = ko.unwrap(options.id);
                var type = ko.unwrap(options.entityType);
                var allowed = options.allowed === undefined || ko.unwrap(options.allowed);
                var ticket = ++sequence;
                warning.hidden = true;
                if (!allowed) {
                    element.removeAttribute("data-content-locale");
                    return;
                }
                element.setAttribute("data-content-locale", "");
                if (!id) {
                    return;
                }
                lookup(type, id, revision, token)
                    .then(function (response) {
                        if (disposed || ticket !== sequence) {
                            return;
                        }
                        element.setAttribute("data-content-locale", response.effectiveLocale || "");
                    }).catch(function () {
                        if (!disposed && ticket === sequence) {
                            warning.textContent = i18n.t("contentLocale.error");
                            warning.hidden = false;
                        }
                    });
            });
            ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
                disposed = true;
                sequence++;
                request.dispose();
            });
        }
    };
});
