define(["ko", "models/settings"], function (ko, settings) {
    var instrumentDefinitions = [
        { name: "AspNetCoreInstrumentation", label: "settings.opentelemetry.instrumentAspNetCore" },
        { name: "HttpClientInstrumentation", label: "settings.opentelemetry.instrumentHttpClient" },
        { name: "RuntimeInstrumentation", label: "settings.opentelemetry.instrumentRuntime" },
        { name: "OtlpExporter", label: "settings.opentelemetry.instrumentOtlp" }
    ];

    var isValidEndpoint = function (value, required) {
        var endpoint = String(value || "").trim();

        if (endpoint === "") {
            return !required;
        }

        try {
            var uri = new URL(endpoint);
            return uri.protocol === "http:" || uri.protocol === "https:";
        } catch {
            return false;
        }
    };

    return function OpenTelemetrySettingsEditor(canEdit) {
        var self = this;

        self.loaded = ko.observable(false);
        self.serviceName = ko.observable("");
        self.isEnabled = ko.observable(false);
        self.protocols = ko.observableArray([]);
        self.traces = ko.observable(null);
        self.metrics = ko.observable(null);
        self.logging = ko.observable(null);

        var presentExporter = function (data, logging) {
            var instruments = instrumentDefinitions.map(function (definition) {
                return {
                    name: definition.name,
                    label: definition.label,
                    selected: ko.observable(data.instruments.indexOf(definition.name) >= 0)
                };
            });

            var exporter = {
                isEnabled: ko.observable(data.isEnabled),
                endpoint: ko.observable(data.endpoint || ""),
                protocol: ko.observable(data.protocol || ""),
                instruments: instruments
            };

            exporter.hasOtlpExporter = ko.pureComputed(function () {
                return instruments.some(function (item) {
                    return item.name === "OtlpExporter" && item.selected();
                });
            });
            exporter.isValid = ko.pureComputed(function () {
                return isValidEndpoint(exporter.endpoint(), exporter.isEnabled() && exporter.hasOtlpExporter());
            });

            if (logging) {
                exporter.includeFormattedMessageOriginal = data.includeFormattedMessage;
                exporter.includeScopesOriginal = data.includeScopes;
                exporter.parseStateValuesOriginal = data.parseStateValues;
                exporter.includeFormattedMessage = ko.observable(data.includeFormattedMessage === true);
                exporter.includeScopes = ko.observable(data.includeScopes === true);
                exporter.parseStateValues = ko.observable(data.parseStateValues === true);
            }

            return exporter;
        };

        self.isValid = ko.pureComputed(function () {
            return (!self.isEnabled() || self.serviceName().trim() !== "")
                && self.traces() !== null
                && self.traces().isValid()
                && self.metrics() !== null
                && self.metrics().isValid()
                && self.logging() !== null
                && self.logging().isValid();
        });

        self.canSave = ko.pureComputed(function () {
            return self.loaded() && canEdit() && self.isValid();
        });

        self.apply = function (data) {
            self.serviceName(data.serviceName || "");
            self.isEnabled(data.isEnabled);
            self.protocols(data.protocols);
            self.traces(presentExporter(data.traces, false));
            self.metrics(presentExporter(data.metrics, false));
            self.logging(presentExporter(data.logging, true));
            self.loaded(true);
        };

        self.load = function (file) {
            return settings.loadOpenTelemetry(file).then(function (result) {
                if (result.ok) {
                    self.apply(result.data);
                }

                return result;
            });
        };

        var exporterRequest = function (exporter) {
            return {
                isEnabled: exporter.isEnabled(),
                endpoint: exporter.endpoint().trim(),
                protocol: exporter.protocol() || null,
                instruments: exporter.instruments.filter(function (item) {
                    return item.selected();
                }).map(function (item) {
                    return item.name;
                })
            };
        };

        var optionalBoolean = function (value, original) {
            return value() === (original === true) ? original : value();
        };

        self.request = function () {
            var logging = exporterRequest(self.logging());

            logging.includeFormattedMessage = optionalBoolean(
                self.logging().includeFormattedMessage,
                self.logging().includeFormattedMessageOriginal);
            logging.includeScopes = optionalBoolean(
                self.logging().includeScopes,
                self.logging().includeScopesOriginal);
            logging.parseStateValues = optionalBoolean(
                self.logging().parseStateValues,
                self.logging().parseStateValuesOriginal);

            return {
                serviceName: self.serviceName().trim(),
                isEnabled: self.isEnabled(),
                traces: exporterRequest(self.traces()),
                metrics: exporterRequest(self.metrics()),
                logging: logging
            };
        };

    };
});
