define(["models/i18n"], function (i18n) {
    var formatDate = i18n.dateTime;

    var pad = function (value) {
        return value < 10 ? "0" + value : String(value);
    };

    return {
        formatDate: formatDate,
        toLocalInput: function (value) {
            if (!value) {
                return "";
            }

            var date = new Date(value);

            return date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate())
                + "T" + pad(date.getHours()) + ":" + pad(date.getMinutes());
        },
        fromLocalInput: function (value) {
            return value ? new Date(value).toISOString() : null;
        },
        author: function (name) {
            return (name || "").trim() || i18n.t("content.anonymous");
        },
        stamp: function (item) {
            if (item.updatedAt) {
                return i18n.t("content.updated", [formatDate(item.updatedAt)]);
            }

            return item.createdAt ? i18n.t("content.created", [formatDate(item.createdAt)]) : "";
        },
        created: function (item) {
            return item.createdAt ? i18n.t("content.created", [formatDate(item.createdAt)]) : "";
        },
        schedule: function (item) {
            if (!item.startAt) {
                return "";
            }

            return item.endAt
                ? i18n.t("news.scheduleStartsEnds", [formatDate(item.startAt), formatDate(item.endAt)])
                : i18n.t("news.scheduleStarts", [formatDate(item.startAt)]);
        },
        version: function (item) {
            return i18n.t("content.versionSummary", [item.latestVersion, item.activeVersion === null ? i18n.t("content.none") : item.activeVersion]);
        }
    };
});
