define(["ko", "models/pager", "modules/quotes/models/quotes"], function (ko, createPager, quotes) {
    return function QuotesViewModel() {
        var self = this;
        var pageSize = 10;

        self.message = ko.observable("");
        self.items = ko.observableArray([]);
        self.pager = createPager(function () {
            return self.refresh();
        });
        self.hasItems = ko.pureComputed(function () {
            return self.items().length > 0;
        });

        self.refresh = function () {
            return quotes.list({ page: self.pager.page(), pageSize: pageSize }).then(function (result) {
                self.message(result.message);

                if (!result.ok) {
                    self.items([]);
                    return;
                }

                self.pager.update(result.data.quotes);
                self.items(result.data.quotes.items);
            });
        };

        self.activate = self.refresh;
    };
});
