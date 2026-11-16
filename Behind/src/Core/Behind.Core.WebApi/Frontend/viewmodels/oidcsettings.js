define(["ko", "models/settings"], function (ko, settings) {
    var entry = function (value) {
        return { value: ko.observable(value) };
    };

    var mapping = function (external, roles) {
        var row = {
            external: ko.observable(external),
            roles: ko.observableArray(roles.map(entry))
        };
        row.addRole = function () {
            row.roles.push(entry(""));
        };
        row.removeRole = function (role) {
            row.roles.remove(role);
        };
        return row;
    };

    var provider = function (data) {
        var row = {
            id: ko.observable(data.id || ""),
            displayName: ko.observable(data.displayName || ""),
            enabled: ko.observable(data.enabled !== false),
            authority: ko.observable(data.authority || ""),
            clientId: ko.observable(data.clientId || ""),
            clientSecret: ko.observable(""),
            clearClientSecret: ko.observable(false),
            scopes: ko.observableArray((data.scopes || []).map(entry)),
            autoCreateUsers: ko.observable(!!data.autoCreateUsers),
            linkByVerifiedEmail: ko.observable(!!data.linkByVerifiedEmail),
            roleClaimType: ko.observable(data.roleClaimType || ""),
            roleMappings: ko.observableArray(Object.keys(data.roleMappings || {}).map(function (external) {
                return mapping(external, data.roleMappings[external]);
            })),
            autoCreateRoles: ko.observable(!!data.autoCreateRoles),
            autoCreateRolePrefix: ko.observable(data.autoCreateRolePrefix || "")
        };
        row.hasClientSecret = ko.pureComputed(function () {
            return !!data.hasClientSecret && row.id().trim() === data.id;
        });
        row.addScope = function () {
            row.scopes.push(entry(""));
        };
        row.removeScope = function (scope) {
            row.scopes.remove(scope);
        };
        row.addMapping = function () {
            row.roleMappings.push(mapping("", []));
        };
        row.removeMapping = function (item) {
            row.roleMappings.remove(item);
        };
        return row;
    };

    return function OidcSettingsEditor(canEdit) {
        var self = this;
        var original = null;
        var loadGeneration = 0;
        self.loaded = ko.observable(false);
        self.providers = ko.observableArray([]);
        self.ticketLifetime = "";
        self.authenticationTimeout = "";
        self.maxPendingTickets = 0;

        self.apply = function (data) {
            original = JSON.parse(JSON.stringify(data));
            self.ticketLifetime = data.ticketLifetime;
            self.authenticationTimeout = data.authenticationTimeout;
            self.maxPendingTickets = data.maxPendingTickets;
            self.providers(data.providers.map(provider));
            self.loaded(true);
        };
        self.load = function () {
            var generation = ++loadGeneration;
            self.loaded(false);
            return settings.loadOidc().then(function (result) {
                if (result.ok && generation === loadGeneration) {
                    self.apply(result.data);
                }
                return result;
            });
        };
        self.discard = function () {
            if (original && canEdit()) {
                self.apply(original);
            }
        };
        self.add = function () {
            if (canEdit()) {
                self.providers.push(provider({
                    enabled: true, scopes: ["openid", "profile", "email"],
                    autoCreateUsers: true, roleClaimType: "roles"
                }));
            }
        };
        self.remove = function (item) {
            if (canEdit()) {
                self.providers.remove(item);
            }
        };
        self.isValid = ko.pureComputed(function () {
            var ids = [];
            return self.providers().every(function (item) {
                var id = item.id().trim();
                var mappings = [];
                var validMappings = item.roleMappings().every(function (row) {
                    var external = row.external().trim();
                    if (!external || mappings.indexOf(external) >= 0 || !row.roles().length ||
                        row.roles().some(function (role) { return !role.value().trim(); })) {
                        return false;
                    }
                    mappings.push(external);
                    return true;
                });
                if (!/^[a-zA-Z0-9_-]+$/.test(id) || ids.indexOf(id.toLowerCase()) >= 0) {
                    return false;
                }
                ids.push(id.toLowerCase());
                if (!item.enabled()) {
                    return true;
                }
                var authority;
                try {
                    authority = new URL(item.authority().trim());
                } catch (error) {
                    return false;
                }
                return authority.protocol === "https:" && !authority.username && !authority.password &&
                    !authority.search && !authority.hash && !!item.displayName().trim() &&
                    !!item.clientId().trim() && !!item.roleClaimType().trim() && validMappings &&
                    item.scopes().some(function (scope) { return scope.value().trim() === "openid"; }) &&
                    !item.scopes().some(function (scope) { return !scope.value().trim(); });
            });
        });
        self.canSave = ko.pureComputed(function () {
            return self.loaded() && canEdit() && self.isValid();
        });
        self.request = function () {
            return {
                ticketLifetime: self.ticketLifetime,
                authenticationTimeout: self.authenticationTimeout,
                maxPendingTickets: self.maxPendingTickets,
                providers: self.providers().map(function (item) {
                    var mappings = Object.create(null);
                    item.roleMappings().forEach(function (row) {
                        mappings[row.external().trim()] = row.roles().map(function (role) { return role.value().trim(); });
                    });
                    return {
                        id: item.id().trim(),
                        displayName: item.displayName().trim(),
                        enabled: item.enabled(),
                        authority: item.authority().trim(),
                        clientId: item.clientId().trim(),
                        clientSecret: item.clearClientSecret() ? "" : item.clientSecret(),
                        clearClientSecret: item.clearClientSecret(),
                        scopes: item.scopes().map(function (scope) { return scope.value().trim(); }),
                        autoCreateUsers: item.autoCreateUsers(),
                        linkByVerifiedEmail: item.linkByVerifiedEmail(),
                        roleClaimType: item.roleClaimType().trim(),
                        roleMappings: mappings,
                        autoCreateRoles: item.autoCreateRoles(),
                        autoCreateRolePrefix: item.autoCreateRolePrefix().trim() || null
                    };
                })
            };
        };
        self.save = function () {
            return settings.saveOidc(self.request());
        };
    };
});
