define(["ko", "models/settings"], function (ko, settings) {
    var smtpProvider = "Smtp";
    var basicAuthentication = "Basic";
    var minPort = 1;
    var maxPort = 65535;

    return function EmailSettingsEditor(canEdit) {
        var self = this;

        self.loaded = ko.observable(false);
        self.provider = ko.observable("Logger");
        self.providers = ko.observableArray([]);
        self.authenticationTypes = ko.observableArray([]);
        self.server = ko.observable("");
        self.port = ko.observable("");
        self.ssl = ko.observable(true);
        self.from = ko.observable("");
        self.authentication = ko.observable("No");
        self.username = ko.observable("");
        self.password = ko.observable("");
        self.passwordMask = ko.observable("");
        self.templates = ko.observableArray([]);

        self.isSmtp = ko.pureComputed(function () {
            return self.provider() === smtpProvider;
        });
        self.isBasic = ko.pureComputed(function () {
            return self.authentication() === basicAuthentication;
        });
        self.hasStoredPassword = ko.pureComputed(function () {
            return self.passwordMask() !== "";
        });
        self.hasValidPort = ko.pureComputed(function () {
            var port = Number(self.port());

            return Number.isInteger(port) && port >= minPort && port <= maxPort;
        });
        self.hasValidAuthentication = ko.pureComputed(function () {
            return !self.isBasic() || (self.username().trim() !== "" && (self.password() !== "" || self.hasStoredPassword()));
        });
        self.isValid = ko.pureComputed(function () {
            return !self.isSmtp() || (self.server().trim() !== "" && self.hasValidPort() && self.hasValidAuthentication());
        });
        self.canSave = ko.pureComputed(function () {
            return self.loaded() && canEdit() && self.isValid();
        });

        var presentTemplates = function (smtp, defaults) {
            return Object.keys(defaults).map(function (name) {
                return { name: name, value: ko.observable(smtp.templates[name] || ""), placeholder: defaults[name] };
            });
        };

        self.apply = function (data) {
            self.providers(data.providers);
            self.authenticationTypes(data.authenticationTypes);
            self.provider(data.provider);
            self.server(data.smtp.server);
            self.port(String(data.smtp.port));
            self.ssl(data.smtp.ssl);
            self.from(data.smtp.from);
            self.authentication(data.smtp.authentication);
            self.username(data.smtp.username);
            self.password("");
            self.passwordMask(data.smtp.passwordMask);
            self.templates(presentTemplates(data.smtp, data.defaultTemplates));
            self.loaded(true);
        };

        self.load = function () {
            return settings.loadEmail().then(function (result) {
                if (result.ok) {
                    self.apply(result.data);
                }

                return result;
            });
        };

        self.request = function () {
            if (!self.isSmtp()) {
                return { provider: self.provider() };
            }

            var templates = {};

            self.templates().forEach(function (template) {
                if (template.value().trim() !== "") {
                    templates[template.name] = template.value();
                }
            });

            return {
                provider: self.provider(),
                smtp: {
                    server: self.server().trim(),
                    port: Number(self.port()),
                    ssl: self.ssl(),
                    from: self.from().trim(),
                    authentication: self.authentication(),
                    username: self.isBasic() ? self.username().trim() : "",
                    password: self.isBasic() ? self.password() : "",
                    templates: templates
                }
            };
        };

        self.save = function () {
            return settings.saveEmail(self.request());
        };
    };
});
