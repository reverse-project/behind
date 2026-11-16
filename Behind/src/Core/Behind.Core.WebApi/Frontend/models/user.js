define(function () {
    return {
        label: function (user) {
            var name = (user.displayName || "").trim();
            var normalized = (user.id || "").replace(/-/g, "").toLowerCase();
            var value = 14695981039346656037n;

            if (!normalized) {
                return name || "user";
            }

            for (var index = 0; index < normalized.length; index++) {
                value = BigInt.asUintN(64, (value ^ BigInt(normalized.charCodeAt(index))) * 1099511628211n);
            }

            var hash = value.toString(36).padStart(8, "0").slice(-8);

            return (name || "user") + "#" + hash;
        },

        href: function (id) {
            return id ? "#/user/" + encodeURIComponent(id) : null;
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
