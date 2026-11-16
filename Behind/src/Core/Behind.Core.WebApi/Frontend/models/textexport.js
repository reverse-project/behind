define(function () {
    var revokeDelay = 1000;

    return {
        copy: function (text) {
            if (!navigator.clipboard || typeof navigator.clipboard.writeText !== "function") {
                return Promise.resolve(false);
            }

            return navigator.clipboard.writeText(text).then(
                function () {
                    return true;
                },
                function () {
                    return false;
                }
            );
        },
        download: function (name, text) {
            var url = URL.createObjectURL(new Blob([text], { type: "text/plain" }));
            var link = document.createElement("a");

            link.href = url;
            link.download = name;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.setTimeout(function () {
                URL.revokeObjectURL(url);
            }, revokeDelay);
        }
    };
});
