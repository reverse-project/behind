define(["modules/sport/models/listing", "modules/sport/models/format", "modules/sport/models/links"], function (createListing, format, links) {
    return function SeasonsViewModel() {
        var self = this;

        self.links = links;
        self.format = format;
        self.list = createListing({ resource: "seasons", pageSize: 20 });

        self.activate = self.list.refresh;
    };
});
