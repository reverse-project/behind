define(["ko", "models/session", "models/i18n", "modules/contact/models/contact", "models/pager"], function (ko, session, i18n, contact, createPager) {
    var fieldTypes = ["text", "textarea", "email"];

    return function ContactManagerViewModel() {
        var self = this;
        var formVersion = null;
        var currentRequestId = null;
        var detailRevision = 0;
        var appliedSearch = "";

        self.activeSection = ko.observable("forms");
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.showDeleted = ko.observable(false);
        self.search = ko.observable("");
        self.hideRead = ko.observable(false);
        self.forms = ko.observableArray([]);
        self.requests = ko.observableArray([]);
        self.requestDetails = ko.observable(null);
        self.pager = createPager(function () {
            return self.loadRequests();
        });
        ["first", "previous", "next", "last", "goTo"].forEach(function (name) {
            var move = self.pager[name];
            self.pager[name] = function (target) {
                return self.busy() ? Promise.resolve() : move(target);
            };
        });
        self.editingFormId = ko.observable(null);
        self.formName = ko.observable("");
        self.description = ko.observable("");
        self.recipient = ko.observable("");
        self.subjectMode = ko.observable("choices");
        self.subjectsText = ko.observable("");
        self.formFields = ko.observableArray([]);
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Contact.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Contact.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Contact.Delete");
        });
        self.canViewDeleted = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.isEditingForm = ko.pureComputed(function () {
            return self.editingFormId() !== null;
        });
        self.formHeading = ko.pureComputed(function () {
            return i18n.t(self.isEditingForm() ? "contact.manager.form.edit" : "contact.manager.form.new");
        });
        self.fieldTypes = fieldTypes.map(function (value) {
            return { value: value, label: i18n.t("contact.fieldType." + value) };
        });
        self.statusLabel = function (status) {
            return i18n.t("contact.deliveryStatus." + status);
        };
        self.recipientLabel = function (recipient) {
            return recipient || i18n.t("contact.manager.inboxOnly");
        };
        self.createdAtLabel = function (createdAt) {
            return createdAt ? i18n.dateTime(createdAt) : i18n.t("contact.manager.creationAuditMissing");
        };
        self.canRetry = function (request) {
            return !!request && !request.isDeleted && request.deliveryStatus === "failed";
        };
        self.statusClass = function (status) {
            if (status === "failed") {
                return "text-bg-danger";
            }

            if (status === "sending") {
                return "text-bg-warning";
            }

            if (status === "sent") {
                return "text-bg-success";
            }

            return "text-bg-secondary";
        };

        var run = function (action) {
            if (self.busy()) {
                return Promise.resolve();
            }

            self.busy(true);
            self.message("");
            return action().catch(function (error) {
                self.message(error.message || i18n.t("common.error.generic"));
            }).finally(function () {
                self.busy(false);
            });
        };

        var arrayOf = function (data) {
            if (Array.isArray(data)) {
                return data;
            }

            if (data && Array.isArray(data.items)) {
                return data.items;
            }

            return [];
        };

        var newField = function (field) {
            field = field || {};
            return {
                key: ko.observable(field.key || ""),
                label: ko.observable(field.label || ""),
                type: ko.observable(field.type || "text"),
                required: ko.observable(!!field.required),
                maxLength: ko.observable(field.maxLength || 2000)
            };
        };

        var applyForm = function (form) {
            formVersion = form.version;
            self.editingFormId(form.id);
            self.formName(form.name || "");
            self.description(form.description || "");
            self.recipient(form.recipient || "");
            self.subjectMode(form.subjectMode || "choices");
            self.subjectsText((form.subjects || []).join("\n"));
            self.formFields((form.fields || []).map(newField));
        };

        var check = function (result, successMessage) {
            if (!result.ok) {
                self.message(result.message);
                return false;
            }

            if (successMessage) {
                self.message(i18n.t(successMessage));
            }

            return true;
        };

        self.switchSection = function (section) {
            self.activeSection(section);
            self.message("");
            if (section === "forms") {
                return self.loadForms();
            }
            return self.loadRequests();
        };

        var loadFormsData = function () {
            if (!self.canRead()) {
                self.forms([]);
                return Promise.resolve();
            }

            return contact.manageForms(self.showDeleted()).then(function (result) {
                if (check(result)) {
                    self.forms(arrayOf(result.data));
                }
            });
        };

        var clearRequest = function () {
            detailRevision++;
            currentRequestId = null;
            self.requestDetails(null);
        };

        var loadRequestsData = function () {
            detailRevision++;
            if (!self.canRead()) {
                self.requests([]);
                clearRequest();
                self.pager.update({ page: 1, pageCount: 0, total: 0 });
                return Promise.resolve();
            }

            return contact.manageRequests({
                page: self.pager.page(),
                pageSize: 10,
                search: appliedSearch,
                unreadOnly: self.hideRead(),
                deleted: self.showDeleted()
            }).then(function (result) {
                if (check(result)) {
                    if (self.pager.isOutOfRange(result.data)) {
                        self.pager.clampIfOutOfRange(result.data);
                        return loadRequestsData();
                    }

                    self.pager.update(result.data);
                    self.requests(result.data.items);
                    if (currentRequestId && self.requests().some(function (item) {
                        return item.id === currentRequestId;
                    })) {
                        return self.openRequest(currentRequestId);
                    }

                    clearRequest();
                } else {
                    clearRequest();
                }
            }).catch(function (error) {
                clearRequest();
                throw error;
            });
        };

        self.loadForms = function () {
            return run(loadFormsData);
        };

        self.loadRequests = function () {
            return run(loadRequestsData);
        };

        self.searchRequests = function () {
            return run(function () {
                appliedSearch = self.search().trim();
                self.pager.page(1);
                return loadRequestsData();
            });
        };

        self.deletedChanged = function () {
            return run(function () {
                self.pager.page(1);
                clearRequest();
                return self.activeSection() === "forms" ? loadFormsData() : loadRequestsData();
            });
        };

        self.editForm = function (form) {
            return run(function () {
                return contact.manageForm(form.id, self.showDeleted()).then(function (result) {
                    if (check(result)) {
                        applyForm(result.data);
                    }
                });
            });
        };

        self.resetForm = function () {
            formVersion = null;
            self.editingFormId(null);
            self.formName("");
            self.description("");
            self.recipient("");
            self.subjectMode("choices");
            self.subjectsText("");
            self.formFields([]);
        };

        self.addField = function () {
            if (self.formFields().length < 30) {
                self.formFields.push(newField());
            }
        };

        self.removeField = function (field) {
            self.formFields.remove(field);
        };

        self.saveForm = function () {
            var id = self.editingFormId();
            var fields = {
                name: self.formName().trim(),
                description: self.description().trim(),
                recipient: self.recipient().trim(),
                subjectMode: self.subjectMode(),
                subjects: self.subjectMode() === "choices"
                    ? self.subjectsText().split(/\r?\n/).map(function (subject) { return subject.trim(); }).filter(Boolean)
                    : [],
                fields: self.formFields().map(function (field) {
                    return {
                        key: field.key().trim(),
                        label: field.label().trim(),
                        type: field.type(),
                        required: field.required(),
                        maxLength: Number(field.maxLength())
                    };
                })
            };

            if (id) {
                fields.version = formVersion;
            }

            if (self.subjectMode() === "freeText") {
                fields.subjects = [];
            }

            return run(function () {
                return contact.saveForm(id, fields).then(function (result) {
                    if (check(result, "contact.manager.form.saved")) {
                        self.resetForm();
                        return loadFormsData();
                    }
                });
            });
        };

        var confirmAction = function (key) {
            return window.confirm(i18n.t(key));
        };

        self.removeForm = function (form) {
            if (!confirmAction("contact.manager.confirm.deleteForm")) {
                return Promise.resolve();
            }
            return run(function () {
                return contact.removeForm(form.id).then(function (result) {
                    if (check(result, "contact.manager.form.deleted")) {
                        return loadFormsData();
                    }
                });
            });
        };

        self.restoreForm = function (form) {
            return run(function () {
                return contact.restoreForm(form.id).then(function (result) {
                    if (check(result, "contact.manager.form.restored")) {
                        return loadFormsData();
                    }
                });
            });
        };

        self.hardDeleteForm = function (form) {
            if (!confirmAction("contact.manager.confirm.hardDeleteForm")) {
                return Promise.resolve();
            }
            return run(function () {
                return contact.hardDeleteForm(form.id).then(function (result) {
                    if (check(result, "contact.manager.form.permanentlyDeleted")) {
                        return loadFormsData();
                    }
                });
            });
        };

        self.openRequest = function (id) {
            var revision = ++detailRevision;
            var deleted = self.showDeleted();
            currentRequestId = id;
            self.requestDetails(null);
            return contact.request(id, deleted).then(function (result) {
                if (revision !== detailRevision || id !== currentRequestId || deleted !== self.showDeleted() || !self.canRead()) {
                    return;
                }

                if (check(result)) {
                    self.requestDetails(result.data);
                } else {
                    clearRequest();
                }
            }).catch(function (error) {
                if (revision === detailRevision) {
                    clearRequest();
                    throw error;
                }
            });
        };

        self.selectRequest = function (request) {
            return run(function () {
                return self.openRequest(request.id);
            });
        };

        self.setRead = function (read) {
            var request = self.requestDetails();
            if (!request) {
                return Promise.resolve();
            }

            return run(function () {
                return contact.setRequestRead(request.id, read).then(function (result) {
                    if (check(result, read ? "contact.manager.markedRead" : "contact.manager.markedUnread")) {
                        return loadRequestsData();
                    }
                });
            });
        };

        self.retryRequest = function () {
            var request = self.requestDetails();
            if (!self.canRetry(request)) {
                return Promise.resolve();
            }

            return run(function () {
                return contact.retryRequest(request.id).then(function (result) {
                    if (check(result, "contact.manager.request.retried")) {
                        return loadRequestsData();
                    }
                });
            });
        };

        self.removeRequest = function () {
            var request = self.requestDetails();
            if (!request || !confirmAction("contact.manager.confirm.deleteRequest")) {
                return Promise.resolve();
            }
            return run(function () {
                return contact.removeRequest(request.id).then(function (result) {
                    if (check(result, "contact.manager.request.deleted")) {
                        clearRequest();
                        return loadRequestsData();
                    }
                });
            });
        };

        self.restoreRequest = function () {
            var request = self.requestDetails();
            if (!request) {
                return Promise.resolve();
            }
            return run(function () {
                return contact.restoreRequest(request.id).then(function (result) {
                    if (check(result, "contact.manager.request.restored")) {
                        clearRequest();
                        return loadRequestsData();
                    }
                });
            });
        };

        self.hardDeleteRequest = function () {
            var request = self.requestDetails();
            if (!request || !confirmAction("contact.manager.confirm.hardDeleteRequest")) {
                return Promise.resolve();
            }
            return run(function () {
                return contact.hardDeleteRequest(request.id).then(function (result) {
                    if (check(result, "contact.manager.request.permanentlyDeleted")) {
                        clearRequest();
                        return loadRequestsData();
                    }
                });
            });
        };

        self.refresh = function () {
            return self.activeSection() === "forms" ? self.loadForms() : self.loadRequests();
        };

        self.activate = function () {
            return self.loadForms();
        };
    };
});
