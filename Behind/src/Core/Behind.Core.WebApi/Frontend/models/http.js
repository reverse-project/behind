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
    var pendingRenewal = null;

    var send = function (method, url, body, extraHeaders) {
        var token = session.accessToken();
        var hasBody = body !== undefined;

        return new Promise(function (resolve) {
            $.ajax({
                url: url,
                method: method,
                dataType: "text",
                headers: $.extend({}, token ? { Authorization: "Bearer " + token } : {}, extraHeaders),
                contentType: hasBody ? "application/json" : false,
                data: hasBody ? JSON.stringify(body) : undefined
            }).then(
                function (data, status, xhr) {
                    resolve({ ok: true, status: xhr.status, data: parse(data) });
                },
                function (xhr) {
                    resolve({ ok: false, status: xhr.status, data: parse(xhr.responseText) });
                }
            );
        });
    };

    var renew = function () {
        if (!pendingRenewal) {
            pendingRenewal = send("GET", sessionUrl, undefined, session.refreshHeaders())
                .then(function (response) {
                    if (response.status === 401) {
                        session.end();
                    }

                    var renewed = response.ok && !!response.data.refreshed;

                    if (renewed) {
                        session.renew(response.data.refreshed);
                    }

                    return renewed;
                })
                .finally(function () {
                    pendingRenewal = null;
                });
        }

        return pendingRenewal;
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
        get: function (url, headers) {
            return request("GET", url, undefined, headers);
        },
        post: function (url, body) {
            return request("POST", url, body);
        },
        put: function (url, body) {
            return request("PUT", url, body);
        }
    };
});
