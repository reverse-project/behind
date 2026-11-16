define(["ko", "text!modules/sport/components/thumbnail.html"], function (ko, template) {
    var icons = {
        team: "fa-shirt",
        player: "fa-user"
    };

    var defaultSize = 2.5;

    return {
        viewModel: {
            createViewModel: function (params) {
                var url = ko.pureComputed(function () {
                    return ko.unwrap(params.url) || null;
                });

                var size = ko.pureComputed(function () {
                    return ko.unwrap(params.size) || defaultSize;
                });

                return {
                    url: url,
                    hasUrl: ko.pureComputed(function () {
                        return url() !== null;
                    }),
                    alt: ko.pureComputed(function () {
                        return ko.unwrap(params.alt) || "";
                    }),
                    icon: ko.pureComputed(function () {
                        return icons[ko.unwrap(params.kind)] || icons.player;
                    }),
                    frame: ko.pureComputed(function () {
                        return { width: size() + "rem", height: size() + "rem", fontSize: size() / 2 + "rem" };
                    })
                };
            }
        },
        template: template
    };
});
