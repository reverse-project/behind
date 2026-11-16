define(["modules/forum/models/forum", "models/i18n"], function (forum, i18n) {
    return {
        load: function (type, limit) {
            if (type === "forum.stats") {
                return Promise.all([
                    forum.get("boards?page=1"), forum.get("posts?page=1"), forum.get("widgets/posts?limit=1")
                ]).then(function (pages) {
                    return { stats: [
                        { label: "widgets.forumBoards", value: pages[0].total },
                        { label: "widgets.forumTopics", value: pages[1].total },
                        { label: "widgets.forumMessages", value: pages[2].total }
                    ] };
                });
            }

            return forum.get("widgets/posts?limit=" + limit).then(function (page) {
                return { items: page.items.slice(0, limit).map(function (post) {
                    return { title: post.title || post.author || i18n.t("comments.anonymous"), href: "#/forum" };
                }) };
            });
        }
    };
});
