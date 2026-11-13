define(["models/http", "models/i18n"], function (http, i18n) {
    var error = function (response) {
        var data = response && response.data;
        return (data && (data.message || data.detail || data.title)) || i18n.t("privateMessages.requestFailed");
    };
    var query = function (values) {
        return Object.keys(values).map(function (key) {
            return encodeURIComponent(key) + "=" + encodeURIComponent(values[key]);
        }).join("&");
    };
    return {
        get: http.get,
        post: http.post,
        error: error,
        query: query
    };
});
