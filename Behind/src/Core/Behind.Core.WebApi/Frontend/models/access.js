define(["ko"], function (ko) {
    var choices = [
        { value: "none", label: "Not set" },
        { value: "grant", label: "Grant" },
        { value: "restrict", label: "Restrict" }
    ];

    var unique = function (values) {
        return values.filter(function (value, index) {
            return values.indexOf(value) === index;
        });
    };

    var stateOf = function (name, grants, restrictions) {
        if (restrictions.indexOf(name) >= 0) {
            return "restrict";
        }

        return grants.indexOf(name) >= 0 ? "grant" : "none";
    };

    return {
        choices: choices,

        create: function () {
            var rows = ko.observableArray([]);

            var pick = function (state) {
                return rows()
                    .filter(function (row) {
                        return row.state() === state;
                    })
                    .map(function (row) {
                        return row.name;
                    });
            };

            return {
                choices: choices,
                rows: rows,
                load: function (available, grants, restrictions) {
                    rows(available.map(function (name) {
                        return { name: name, state: ko.observable(stateOf(name, grants, restrictions)) };
                    }));
                },
                request: function () {
                    return { grants: pick("grant"), restrictions: pick("restrict") };
                }
            };
        },

        calculate: function (roles, grants, restrictions) {
            var granted = grants.slice();
            var restricted = restrictions.slice();

            roles.forEach(function (role) {
                granted = granted.concat(role.grants);
                restricted = restricted.concat(role.restrictions);
            });

            return unique(granted)
                .filter(function (name) {
                    return restricted.indexOf(name) < 0;
                })
                .sort();
        }
    };
});
