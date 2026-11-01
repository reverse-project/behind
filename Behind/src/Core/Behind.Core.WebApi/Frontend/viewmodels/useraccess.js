define(["ko", "models/auth", "models/access"], function (ko, auth, access) {
    return function UserAccessViewModel(userId, save) {
        var self = this;

        self.isOpen = ko.observable(false);
        self.isLoading = ko.observable(false);
        self.message = ko.observable("");
        self.canEdit = ko.observable(false);
        self.availableRoles = ko.observableArray([]);
        self.selectedRoleIds = ko.observableArray([]);
        self.editor = access.create();

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.calculatedGrants = ko.pureComputed(function () {
            var chosen = self.availableRoles().filter(function (role) {
                return self.selectedRoleIds().indexOf(role.id) >= 0;
            });
            var selection = self.editor.request();

            return access.calculate(chosen, selection.grants, selection.restrictions);
        });

        self.hasCalculatedGrants = ko.pureComputed(function () {
            return self.calculatedGrants().length > 0;
        });

        self.load = function () {
            self.isLoading(true);

            return auth.loadUserAccess(userId)
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return;
                    }

                    var data = result.data;

                    self.canEdit(data.canEdit);
                    self.availableRoles(data.availableRoles);
                    self.selectedRoleIds(data.roles.map(function (role) {
                        return role.id;
                    }));
                    self.editor.load(data.availableGrants, data.grants, data.restrictions);
                })
                .finally(function () {
                    self.isLoading(false);
                });
        };

        self.toggle = function () {
            self.isOpen(!self.isOpen());

            return self.isOpen() ? self.load() : Promise.resolve();
        };

        self.save = function () {
            var selection = self.editor.request();

            return save({
                roleIds: self.selectedRoleIds(),
                grants: selection.grants,
                restrictions: selection.restrictions
            });
        };
    };
});
