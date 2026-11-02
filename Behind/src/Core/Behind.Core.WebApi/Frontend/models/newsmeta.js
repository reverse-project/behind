define(function () {
    var anonymous = "Anonymous";

    var formatDate = function (value) {
        return value ? new Date(value).toLocaleString() : "";
    };

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
            return (name || "").trim() || anonymous;
        },
        stamp: function (item) {
            if (item.updatedAt) {
                return "Updated " + formatDate(item.updatedAt);
            }

            return item.createdAt ? "Created " + formatDate(item.createdAt) : "";
        },
        created: function (item) {
            return item.createdAt ? "Created " + formatDate(item.createdAt) : "";
        }
    };
});
