define(function () {
    return {
        label: function (user) {
            var name = (user.displayName || "").trim();
            var emails = user.emails || [];

            return name || (emails.length > 0 ? emails[0] : "");
        }
    };
});
