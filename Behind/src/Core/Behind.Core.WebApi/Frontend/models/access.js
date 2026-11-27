define(["ko", "models/modules"], function (ko, modules) {
    var choices = [
        { value: "none", label: "access.choice.none" },
        { value: "grant", label: "access.choice.grant" },
        { value: "restrict", label: "access.choice.restrict" }
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
            var hiddenGrants = [];
            var hiddenRestrictions = [];

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
                rows: ko.pureComputed(function () {
                    var visible = modules.visibleGrants(rows().map(function (row) { return row.name; }));
                    return rows().filter(function (row) { return visible.indexOf(row.name) >= 0; });
                }),
                load: function (available, grants, restrictions) {
                    var visible = modules.visibleGrants(available);
                    hiddenGrants = grants.filter(function (name) { return visible.indexOf(name) < 0; });
                    hiddenRestrictions = restrictions.filter(function (name) { return visible.indexOf(name) < 0; });
                    rows(visible.map(function (name) {
                        return { name: name, state: ko.observable(stateOf(name, grants, restrictions)) };
                    }));
                },
                request: function () {
                    return { grants: pick("grant").concat(hiddenGrants), restrictions: pick("restrict").concat(hiddenRestrictions) };
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
