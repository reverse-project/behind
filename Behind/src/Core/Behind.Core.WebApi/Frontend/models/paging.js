define(function () {
    var paging = function (query) {
        return "page=" + query.page + "&pageSize=" + query.pageSize;
    };

    var managing = function (path, query) {
        return path + "?" + paging(query)
            + "&search=" + encodeURIComponent(query.search) + "&letter=" + encodeURIComponent(query.letter)
            + (query.deleted ? "&deleted=true" : "");
    };

    return {
        paging: paging,
        managing: managing
    };
});
