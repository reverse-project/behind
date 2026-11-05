define(["ko", "models/sites", "models/sitesections", "models/i18n", "bindings/richeditor"], function (ko, sites, model, i18n) {
    return function SectionsEditorViewModel(params) {
        var self = this;

        self.sections = params.sections;
        self.canWrite = params.canWrite;
        self.upload = params.upload;
        self.maxSections = params.maxSections;
        self.message = ko.observable("");
        self.busy = ko.observable(false);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });
        self.hasSections = ko.pureComputed(function () {
            return self.sections().length > 0;
        });
        self.canAdd = ko.pureComputed(function () {
            return self.canWrite() && self.sections().length < self.maxSections();
        });
        self.limitHelp = ko.pureComputed(function () {
            return self.maxSections() > 0 ? i18n.t("sections.editor.limit", [self.maxSections()]) : "";
        });

        self.add = function () {
            if (self.canAdd()) {
                self.sections.push(model.editable({}));
            }
        };

        var move = function (section, offset) {
            var items = self.sections().slice();
            var index = items.indexOf(section);

            items.splice(index, 1);
            items.splice(index + offset, 0, section);
            self.sections(items);
        };

        self.canMoveUp = function (section) {
            return self.canWrite() && self.sections.indexOf(section) > 0;
        };
        self.canMoveDown = function (section) {
            return self.canWrite() && self.sections.indexOf(section) < self.sections().length - 1;
        };
        self.moveUp = function (section) {
            move(section, -1);
        };
        self.moveDown = function (section) {
            move(section, 1);
        };
        self.remove = function (section) {
            self.sections.remove(section);
        };

        self.pickImage = function (section, event) {
            var input = event.target;
            var file = input.files && input.files[0];

            if (!file) {
                return;
            }

            self.busy(true);
            self.message("");

            sites.uploadImage(file)
                .then(function (result) {
                    if (result.ok) {
                        section.setImage(result.data);
                    } else {
                        self.message(result.message);
                    }
                })
                .finally(function () {
                    input.value = "";
                    self.busy(false);
                });
        };
    };
});
