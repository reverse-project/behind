define(["jquery", "models/session", "models/contentlocale"], function ($, session, contentLocale) {
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
    var stale = { ok: false, status: 0, data: null, stale: true };
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

    var send = function (method, url, body, extraHeaders, requestLocale, withCredentials) {
        var owner = session.generation();
        var token = session.accessToken();
        var hasBody = body !== undefined;
        var isForm = hasBody && body instanceof FormData;

        var locale = arguments.length > 4 ? requestLocale : contentLocale.current();
        var cacheKey = (locale || "") + "\n" + url;
        var entries = isCacheable(method, url, extraHeaders) ? cacheOf(token) : null;
        var cached = entries ? entries.get(cacheKey) : undefined;
        var headers = $.extend(
            {},
            token ? { Authorization: "Bearer " + token } : {},
            locale ? { "X-Content-Locale": locale } : {},
            cached ? { "If-None-Match": cached.etag } : {},
            extraHeaders
        );
        if (headers["X-Content-Locale"] === null) {
            delete headers["X-Content-Locale"];
        }

        return new Promise(function (resolve) {
            $.ajax({
                url: url,
                method: method,
                dataType: "text",
                headers: headers,
                contentType: hasBody && !isForm ? "application/json" : false,
                processData: !isForm,
                data: hasBody && !isForm ? JSON.stringify(body) : body,
                xhrFields: withCredentials ? { withCredentials: true } : undefined
            }).then(
                function (data, status, xhr) {
                    if (owner !== session.generation()) {
                        resolve(stale);
                        return;
                    }

                    if (cached && xhr.status === 304) {
                        resolve({ ok: true, status: 200, data: parse(cached.text) });
                        return;
                    }

                    if (entries) {
                        remember(entries, cacheKey, xhr.getResponseHeader("ETag"), data);
                    }

                    var parsed = parse(data);
                    if (isForm) {
                        contentLocale.trackMedia(parsed);
                    }
                    resolve({ ok: true, status: xhr.status, data: parsed });
                },
                function (xhr) {
                    if (owner !== session.generation()) {
                        resolve(stale);
                        return;
                    }

                    if (entries) {
                        entries.delete(cacheKey);
                    }

                    resolve({ ok: false, status: xhr.status, data: parse(xhr.responseText) });
                }
            );
        });
    };

    var loadSession = function () {
        var owner = session.generation();
        if (!pendingSession || pendingSession.owner !== owner) {
            var request = send("GET", sessionUrl, undefined, session.refreshHeaders())
                .then(function (response) {
                    if (owner !== session.generation()) {
                        return stale;
                    }

                    if (response.status === 401) {
                        session.end();
                    }

                    if (response.ok && response.data.refreshed) {
                        session.renew(response.data.refreshed);
                    }

                    return response;
                })
                .finally(function () {
                    if (pendingSession && pendingSession.request === request) {
                        pendingSession = null;
                    }
                });
            pendingSession = { owner: owner, request: request };
        }

        return pendingSession.request;
    };

    var renew = function () {
        return loadSession().then(function (response) {
            return response.ok && !!response.data.refreshed;
        });
    };

    var request = function (method, url, body, extraHeaders, withCredentials) {
        var owner = session.generation();
        var wasSignedIn = session.isSignedIn();
        var locale = contentLocale.current();

        return send(method, url, body, extraHeaders, locale, withCredentials).then(function (response) {
            if (owner !== session.generation()) {
                return stale;
            }

            if (response.status !== 401 || !wasSignedIn || url === sessionUrl) {
                return response;
            }

            return renew().then(function (renewed) {
                if (renewed && owner === session.generation()) {
                    return send(method, url, body, extraHeaders, locale, withCredentials);
                }

                return owner === session.generation() ? response : stale;
            });
        });
    };

    var backendUrl = function (path) {
        return new URL(path, window.location.origin).toString();
    };

    return {
        loadSession: loadSession,
        backendUrl: backendUrl,
        get: function (url, headers) {
            return request("GET", url, undefined, headers);
        },
        post: function (url, body) {
            return request("POST", url, body);
        },
        postWithCredentials: function (url, body) {
            return request("POST", url, body, undefined, true);
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
