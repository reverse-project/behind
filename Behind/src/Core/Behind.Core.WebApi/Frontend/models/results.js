define(["models/i18n"], function (i18n) {
    var genericFailure = "common.error.generic";

    var sessionExpired = "common.error.sessionExpired";

    var translate = function (message) {
        return i18n.tOr(message, message);
    };

    return {
        sessionExpired: sessionExpired,
        create: function (conflictMessages) {
            var failure = function (response, messages) {
                return { ok: false, message: translate(messages[response.status] || genericFailure) };
            };

            var conflict = function (response, messages) {
                var reason = response.data && response.data.reason ? response.data.reason : "";

                return { ok: false, message: translate(conflictMessages[reason] || messages[409] || genericFailure) };
            };

            return {
                failure: failure,
                loaded: function (response, messages) {
                    return response.ok ? { ok: true, message: "", data: response.data } : failure(response, messages);
                },
                saved: function (response, done, messages) {
                    if (response.ok) {
                        return { ok: true, message: translate(done), data: response.data };
                    }

                    return response.status === 409 ? conflict(response, messages) : failure(response, messages);
                }
            };
        }
    };
});
