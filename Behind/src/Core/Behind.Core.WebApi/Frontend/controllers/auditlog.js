define(["controllers/controller", "text!views/auditlog.html", "viewmodels/auditlog"], function (createController, view, AuditLogViewModel) {
    return createController(view, AuditLogViewModel);
});
