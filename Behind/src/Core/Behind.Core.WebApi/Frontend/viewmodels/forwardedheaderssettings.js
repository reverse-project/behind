define(["ko", "models/settings"], function (ko, settings) {
    var networkAligned = function (host, v6, prefix) {
        var groups = host.split(".");
        if (v6) {
            var sides = host.slice(1, -1).split("::");
            var left = sides[0] ? sides[0].split(":") : [];
            var right = sides[1] ? sides[1].split(":") : [];
            groups = left.concat(Array(8 - left.length - right.length).fill("0"), right);
        }
        var value = groups.reduce(function (total, group) {
            return (total << BigInt(v6 ? 16 : 8)) | BigInt(parseInt(group, v6 ? 16 : 10));
        }, 0n);
        return BigInt.asUintN((v6 ? 128 : 32) - prefix, value) === 0n;
    };

    var validEntry = function (entry) {
        var parts = entry.split("/");
        var address = parts[0];
        var v6 = address.includes(":");
        if (parts.length > 2 || !/^[a-fA-F0-9:.]+$/.test(address)) {
            return false;
        }
        try {
            var host = new URL("http://" + (v6 ? "[" + address + "]" : address)).hostname;
            if (!v6 && (!/^\d+\.\d+\.\d+\.\d+$/.test(address) || host !== address)) {
                return false;
            }
            if (host === "0.0.0.0" || host === "[::]" || host === "[::ffff:0:0]") {
                return false;
            }
            var prefix = Number(parts[1]);
            var minimum = v6 ? (host.startsWith("[::ffff:") ? 120 : 64) : 24;
            return parts.length === 1 || (/^\d+$/.test(parts[1]) && prefix >= minimum &&
                prefix <= (v6 ? 128 : 32) && networkAligned(host, v6, prefix));
        } catch {
            return false;
        }
    };

    return function ForwardedHeadersSettings(canEdit) {
        var self = this;
        var generation = 0;
        var original = ko.observable(null);
        self.enabled = ko.observable(false);
        self.addresses = ko.observable("");
        self.limit = ko.observable(1);
        self.request = function () {
            return {
                enabled: self.enabled(),
                knownForwarders: self.addresses().split(/\r?\n/).map(function (line) {
                    return line.trim();
                }).filter(Boolean),
                forwardLimit: Number(self.limit())
            };
        };
        self.valid = ko.pureComputed(function () {
            var request = self.request();
            return (!request.enabled || request.knownForwarders.length > 0) &&
                Number.isSafeInteger(request.forwardLimit) && request.forwardLimit > 0 &&
                request.forwardLimit <= 2147483647 && request.knownForwarders.every(validEntry);
        });
        self.canSave = ko.pureComputed(function () {
            return canEdit() && original() !== null && self.valid() && original() !== JSON.stringify(self.request());
        });
        self.invalidate = function () {
            generation++;
            original(null);
            self.enabled(false);
            self.addresses("");
            self.limit(1);
        };
        self.load = function () {
            self.invalidate();
            var current = generation;
            return settings.loadForwardedHeaders().then(function (result) {
                if (generation !== current) {
                    return { ok: true };
                }
                if (result.ok) {
                    self.enabled(result.data.enabled);
                    self.addresses(result.data.knownForwarders.join("\n"));
                    self.limit(result.data.forwardLimit);
                    original(JSON.stringify(self.request()));
                }
                return result;
            });
        };
    };
});
