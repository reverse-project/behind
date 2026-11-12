define(function () {
    var paging = function (query) {
        return "page=" + query.page + "&pageSize=" + query.pageSize;
    };

    var managing = function (path, query) {
        return path + "?" + paging(query)
            + "&search=" + encodeURIComponent(query.search) + "&letter=" + encodeURIComponent(query.letter)
            + (query.deleted ? "&deleted=true" : "");
    };

    var all = function (load, pageOf) {
        var read = function (page, items) {
            return load({ page: page, pageSize: 100, search: "", letter: "", deleted: false }).then(function (result) {
                if (!result.ok) {
                    return result;
                }
                var data = pageOf(result.data);
                var combined = items.concat(data.items);
                return page < data.pageCount ? read(page + 1, combined) : { ok: true, data: combined };
            });
        };
        return read(1, []);
    };

    return {
        paging: paging,
        managing: managing,
        all: all
    };
});
