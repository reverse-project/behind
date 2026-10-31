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

    var request = function (method, url, body) {
        var token = session.accessToken();
        var hasBody = body !== undefined;

        return new Promise(function (resolve) {
            $.ajax({
                url: url,
                method: method,
                dataType: "text",
                headers: token ? { Authorization: "Bearer " + token } : {},
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

    return {
        get: function (url) {
            return request("GET", url);
        },
        post: function (url, body) {
            return request("POST", url, body);
        }
    };
});
