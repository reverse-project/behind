define(function () {
    return {
        label: function (user) {
            var name = (user.displayName || "").trim();
            var emails = user.emails || [];

            return name || (emails.length > 0 ? emails[0] : "");
        },

        grants: function (user) {
            if (Array.isArray(user.grants)) {
                return user.grants;
            }

            var names = [];

            (user.roles || []).forEach(function (role) {
                (role.grants || []).forEach(function (grant) {
                    if (names.indexOf(grant) < 0) {
                        names.push(grant);
                    }
                });
            });

            return names;
        }
    };
});
