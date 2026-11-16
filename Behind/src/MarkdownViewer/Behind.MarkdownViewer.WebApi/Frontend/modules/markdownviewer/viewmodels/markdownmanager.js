define(["ko", "models/session", "modules/markdownviewer/models/markdownviewer"], function (ko, session, markdownviewer) {
    return function MarkdownManagerViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("MarkdownViewer.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("MarkdownViewer.Write");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        self.message = ko.observable("");
        self.busy = ko.observable(false);
        self.configured = ko.observable(false);
        self.documents = ko.observable(0);
        self.locales = ko.observable("");
        self.hasLocales = ko.pureComputed(function () {
            return self.locales() !== "";
        });

        self.canGenerate = ko.pureComputed(function () {
            return self.canWrite() && self.configured() && !self.busy();
        });

        self.refresh = function () {
            return markdownviewer.list().then(function (result) {
                self.configured(result.ok && result.data.configured);
                self.documents(result.ok ? result.data.entries.length : 0);
                self.locales(result.ok ? result.data.locales.join(", ") : "");
            });
        };

        self.generate = function () {
            if (!self.canGenerate()) {
                return Promise.resolve();
            }

            self.busy(true);

            return markdownviewer.generate().then(function (result) {
                self.message(result.message);
            }).finally(function () {
                self.busy(false);
            });
        };

        self.activate = self.refresh;
    };
});
