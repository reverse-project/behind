define(["jquery", "models/session"], function ($, session) {
    var parse = function (text) {
        if (!text) {
            return null;
        }

        try {
            return JSON.parse(text);
        } catch (error) {
            return text;
        }
    };

    var sessionUrl = "/session";
    var pendingSession = null;
    var maxCachedResponses = 100;
    var responseCache = { owner: null, entries: new Map() };

    var cacheOf = function (token) {
        if (responseCache.owner !== token) {
            responseCache = { owner: token, entries: new Map() };
        }

        return responseCache.entries;
    };

    var isCacheable = function (method, url, extraHeaders) {
        return method === "GET" && url !== sessionUrl && $.isEmptyObject(extraHeaders || {});
    };

    var remember = function (entries, url, etag, text) {
        entries.delete(url);

        if (!etag) {
            return;
        }

        entries.set(url, { etag: etag, text: text });

        if (entries.size > maxCachedResponses) {
            entries.delete(entries.keys().next().value);
        }
    };

    var send = function (method, url, body, extraHeaders) {
        var token = session.accessToken();
        var hasBody = body !== undefined;
        var isForm = hasBody && body instanceof FormData;

        var entries = isCacheable(method, url, extraHeaders) ? cacheOf(token) : null;
        var cached = entries ? entries.get(url) : undefined;

        return new Promise(function (resolve) {
            $.ajax({
                url: url,
                method: method,
                dataType: "text",
                headers: $.extend(
                    {},
                    token ? { Authorization: "Bearer " + token } : {},
                    cached ? { "If-None-Match": cached.etag } : {},
                    extraHeaders
                ),
                contentType: hasBody && !isForm ? "application/json" : false,
                processData: !isForm,
                data: hasBody && !isForm ? JSON.stringify(body) : body
            }).then(
                function (data, status, xhr) {
                    if (cached && xhr.status === 304) {
                        resolve({ ok: true, status: 200, data: parse(cached.text) });
                        return;
                    }

                    if (entries) {
                        remember(entries, url, xhr.getResponseHeader("ETag"), data);
                    }

                    resolve({ ok: true, status: xhr.status, data: parse(data) });
                },
                function (xhr) {
                    if (entries) {
                        entries.delete(url);
                    }

                    resolve({ ok: false, status: xhr.status, data: parse(xhr.responseText) });
                }
            );
        });
    };

    var loadSession = function () {
        if (!pendingSession) {
            pendingSession = send("GET", sessionUrl, undefined, session.refreshHeaders())
                .then(function (response) {
                    if (response.status === 401) {
                        session.end();
                    }

                    if (response.ok && response.data.refreshed) {
                        session.renew(response.data.refreshed);
                    }

                    return response;
                })
                .finally(function () {
                    pendingSession = null;
                });
        }

        return pendingSession;
    };

    var renew = function () {
        return loadSession().then(function (response) {
            return response.ok && !!response.data.refreshed;
        });
    };

    var request = function (method, url, body, extraHeaders) {
        var wasSignedIn = session.isSignedIn();

        return send(method, url, body, extraHeaders).then(function (response) {
            if (response.status !== 401 || !wasSignedIn || url === sessionUrl) {
                return response;
            }

            return renew().then(function (renewed) {
                return renewed ? send(method, url, body, extraHeaders) : response;
            });
        });
    };

    return {
        loadSession: loadSession,
        get: function (url, headers) {
            return request("GET", url, undefined, headers);
        },
        post: function (url, body) {
            return request("POST", url, body);
        },
        put: function (url, body) {
            return request("PUT", url, body);
        },
        delete: function (url) {
            return request("DELETE", url);
        },
        postForm: function (url, form) {
            return request("POST", url, form);
        }
    };
});
