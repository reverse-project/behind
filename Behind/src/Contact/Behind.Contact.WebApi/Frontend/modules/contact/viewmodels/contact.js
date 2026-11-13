define(["ko", "models/i18n", "modules/contact/models/contact"], function (ko, i18n, contact) {
    var instance = 0;

    return function ContactViewModel() {
        var self = this;
        var loadingFormId = null;
        var loadingFormPromise = null;
        var formSequence = 0;
        var activationSequence = 0;
        var submissionSequence = 0;
        var disposed = false;

        self.idPrefix = "contact-" + (++instance);
        self.forms = ko.observableArray([]);
        self.selectedFormId = ko.observable("");
        self.form = ko.observable(null);
        self.name = ko.observable("");
        self.email = ko.observable("");
        self.subject = ko.observable("");
        self.message = ko.observable("");
        self.fields = ko.observableArray([]);
        self.busy = ko.observable(false);
        self.notice = ko.observable("");
        self.error = ko.observable("");
        self.hasForms = ko.pureComputed(function () {
            return self.forms().length > 0;
        });
        self.subjectChoices = ko.pureComputed(function () {
            var form = self.form();
            return form && form.subjectMode === "choices" ? form.subjects || [] : [];
        });

        var reset = function () {
            self.name("");
            self.email("");
            var form = self.form();
            self.subject(form && form.subjectMode === "choices" && form.subjects && form.subjects.length
                ? form.subjects[0] : "");
            self.message("");
            self.fields(self.form() ? (self.form().fields || []).map(function (field) {
                return {
                    key: field.key,
                    label: field.label,
                    type: field.type,
                    required: !!field.required,
                    maxLength: field.maxLength || 2000,
                    value: ko.observable("")
                };
            }) : []);
        };

        self.selectForm = function (form) {
            if (disposed || !form || self.form() && self.form().id === form.id) {
                return Promise.resolve();
            }
            if (loadingFormId === form.id) {
                return loadingFormPromise || Promise.resolve();
            }

            var sequence = ++formSequence;
            loadingFormId = form.id;
            self.selectedFormId(form.id);
            self.busy(true);
            self.error("");
            self.notice("");
            self.form(null);

            loadingFormPromise = contact.form(form.id).then(function (result) {
                if (disposed || sequence !== formSequence) {
                    return;
                }

                if (!result.ok) {
                    self.error(result.message);
                    return;
                }

                self.form(result.data);
                reset();
            }).catch(function () {
                if (!disposed && sequence === formSequence) {
                    self.error(i18n.t("contact.error.loadFailed"));
                }
            }).finally(function () {
                if (!disposed && sequence === formSequence) {
                    loadingFormId = null;
                    loadingFormPromise = null;
                    self.busy(false);
                }
            });
            return loadingFormPromise;
        };

        self.submit = function () {
            var form = self.form();
            if (disposed || !form || self.busy()) {
                return Promise.resolve();
            }

            var sequence = ++submissionSequence;
            self.busy(true);
            self.error("");
            self.notice("");

            var extraFields = Object.create(null);
            self.fields().forEach(function (field) {
                extraFields[field.key] = field.value();
            });

            return contact.submit(form.id, {
                name: self.name().trim(),
                email: self.email().trim(),
                subject: self.subject().trim(),
                message: self.message().trim(),
                fields: extraFields
            }).then(function (result) {
                if (disposed || sequence !== submissionSequence) {
                    return;
                }

                if (!result.ok) {
                    self.error(result.message);
                    return;
                }

                var status = result.data && result.data.deliveryStatus;
                self.notice(status === "sent" ? "contact.done.submitted"
                    : status === "notRequested" ? "contact.done.savedToInbox"
                    : status === "failed" ? "contact.done.deliveryFailed"
                    : "contact.done.sending");
                reset();
            }).catch(function () {
                if (!disposed && sequence === submissionSequence) {
                    self.error(i18n.t("contact.error.submitFailed"));
                }
            }).finally(function () {
                if (!disposed && sequence === submissionSequence) {
                    self.busy(false);
                }
            });
        };

        self.activate = function () {
            if (disposed) {
                return Promise.resolve();
            }

            var sequence = ++activationSequence;
            self.busy(true);
            self.error("");

            return contact.forms().then(function (result) {
                if (disposed || sequence !== activationSequence) {
                    return;
                }

                if (!result.ok) {
                    self.error(result.message);
                    return;
                }

                self.forms(Array.isArray(result.data) ? result.data : []);
                var forms = self.forms();
                if (forms.length) {
                    var hash = window.location.hash || "";
                    var requested = hash.match(/[?&]formId=([^&]*)/);
                    var requestedId = requested ? decodeURIComponent(requested[1]) : "";
                    var selected = forms.find(function (item) { return item.id === requestedId; }) || forms[0];
                    return self.selectForm(selected);
                }
            }).catch(function () {
                if (!disposed && sequence === activationSequence) {
                    self.error(i18n.t("contact.error.loadFailed"));
                }
            }).finally(function () {
                if (!disposed && sequence === activationSequence) {
                    self.busy(false);
                }
            });
        };

        var selectionSubscription = self.selectedFormId.subscribe(function (id) {
            var selected = self.forms().find(function (item) { return item.id === id; });
            if (selected && self.form() && self.form().id !== id) {
                self.selectForm(selected);
            } else if (selected && !self.form()) {
                self.selectForm(selected);
            }
        });

        self.dispose = function () {
            disposed = true;
            activationSequence++;
            formSequence++;
            submissionSequence++;
            selectionSubscription.dispose();
            self.hasForms.dispose();
            self.subjectChoices.dispose();
        };
    };
});
