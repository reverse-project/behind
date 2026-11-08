define(["ko", "text!modules/sport/components/link.html", "modules/sport/models/links"], function (ko, template, links) {
    return {
        viewModel: {
            createViewModel: function (params) {
                var href = ko.pureComputed(function () {
                    return links.of(ko.unwrap(params.kind), ko.unwrap(params.id), ko.unwrap(params.season));
                });

                return {
                    href: href,
                    hasLink: ko.pureComputed(function () {
                        return href() !== null;
                    }),
                    text: ko.pureComputed(function () {
                        var shown = ko.unwrap(params.text);

                        return shown === null || shown === undefined ? "" : String(shown);
                    })
                };
            }
        },
        template: template
    };
});
