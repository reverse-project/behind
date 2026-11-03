define(["ko", "models/session", "models/downloadbrowser"], function (ko, session, createBrowser) {
    return function DownloadsViewModel(context) {
        var browser = createBrowser({ rootHref: "#/downloads", params: context.params });

        browser.canManage = ko.pureComputed(function () {
            return session.hasGrant("Download.Read");
        });

        browser.manageHref = ko.pureComputed(function () {
            return browser.selected() ? "#/manage/downloads/" + browser.selected().path : "#/manage/downloads";
        });

        return browser;
    };
});
