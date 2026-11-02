define(function () {
    var genericFailure = "Something went wrong. Please try again.";

    var sessionExpired = "Your session has expired. Please sign in again.";

    return {
        sessionExpired: sessionExpired,
        create: function (conflictMessages) {
            var failure = function (response, messages) {
                return { ok: false, message: messages[response.status] || genericFailure };
            };

            var conflict = function (response, messages) {
                var reason = response.data && response.data.reason ? response.data.reason : "";

                return { ok: false, message: conflictMessages[reason] || messages[409] || genericFailure };
            };

            return {
                failure: failure,
                loaded: function (response, messages) {
                    return response.ok ? { ok: true, message: "", data: response.data } : failure(response, messages);
                },
                saved: function (response, done, messages) {
                    if (response.ok) {
                        return { ok: true, message: done, data: response.data };
                    }

                    return response.status === 409 ? conflict(response, messages) : failure(response, messages);
                }
            };
        }
    };
});
