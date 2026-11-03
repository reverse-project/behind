define(["ko", "models/session", "models/news", "models/newsmeta", "models/pager", "models/comments"], function (ko, session, news, meta, createPager, comments) {
    var commentPageSize = 10;

    return function NewsArticleViewModel(context) {
        var self = this;
        var newsId = context.params.id;

        self.isSignedIn = session.isSignedIn;
        self.loading = ko.observable(true);
        self.message = ko.observable("");
        self.title = ko.observable("");
        self.content = ko.observable("");
        self.author = ko.observable("");
        self.stamp = ko.observable("");
        self.created = ko.observable("");
        self.categoryName = ko.observable("");
        self.imageUrl = ko.observable(null);
        self.isHighlighted = ko.observable(false);

        self.busy = ko.observable(false);
        self.commentsEnabled = ko.observable(true);
        self.commentMessage = ko.observable("");
        self.newComment = ko.observable("");
        self.comments = ko.observableArray([]);
        self.pager = createPager(function () {
            return self.refreshComments();
        });

        self.found = ko.pureComputed(function () {
            return !self.loading() && self.message() === "";
        });

        self.missing = ko.pureComputed(function () {
            return !self.loading() && self.message() !== "";
        });

        self.hasCategory = ko.pureComputed(function () {
            return self.categoryName() !== "";
        });

        self.hasImage = ko.pureComputed(function () {
            return self.imageUrl() !== null;
        });

        self.hasCommentMessage = ko.pureComputed(function () {
            return self.commentMessage() !== "";
        });

        self.hasComments = ko.pureComputed(function () {
            return self.comments().length > 0;
        });

        self.canPost = ko.pureComputed(function () {
            return self.newComment().trim() !== "" && !self.busy();
        });

        var presentComment = function (item) {
            var row = {
                id: item.id,
                author: meta.author(item.author),
                content: item.content,
                createdAt: meta.formatDate(item.createdAt),
                editedAt: item.updatedAt ? "edited " + meta.formatDate(item.updatedAt) : "",
                isEdited: !!item.updatedAt,
                canDelete: item.canDelete,
                canEdit: item.canEdit,
                isEditing: ko.observable(false),
                draft: ko.observable(item.content)
            };

            row.canSaveEdit = ko.pureComputed(function () {
                return row.draft().trim() !== "" && !self.busy();
            });
            row.startEdit = function () {
                row.draft(row.content);
                row.isEditing(true);
            };
            row.cancelEdit = function () {
                row.isEditing(false);
            };
            row.saveEdit = function () {
                if (!row.canSaveEdit()) {
                    return Promise.resolve();
                }

                return self.perform(function () {
                    return news.editComment(row.id, row.draft().trim());
                });
            };

            row.remove = function () {
                if (!window.confirm("Delete this comment?")) {
                    return Promise.resolve();
                }

                return self.perform(function () {
                    return news.removeComment(row.id);
                });
            };

            return row;
        };

        self.refreshComments = function () {
            self.busy(true);

            return news.comments(newsId, { page: self.pager.page(), pageSize: commentPageSize })
                .then(function (result) {
                    if (!result.ok) {
                        self.commentMessage(result.message);
                        self.comments([]);
                        return undefined;
                    }

                    if (self.pager.isOutOfRange(result.data)) {
                        self.pager.clampIfOutOfRange(result.data);
                        return self.refreshComments();
                    }

                    self.pager.update(result.data);
                    self.comments(result.data.items.map(presentComment));

                    return undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.perform = function (action) {
            self.busy(true);

            return action()
                .then(function (result) {
                    return (result.ok ? self.refreshComments() : Promise.resolve()).then(function () {
                        self.commentMessage(result.message);

                        return result;
                    });
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.post = function () {
            if (!self.canPost()) {
                return Promise.resolve();
            }

            return self.perform(function () {
                return news.addComment(newsId, self.newComment().trim());
            }).then(function (result) {
                if (!result.ok) {
                    return undefined;
                }

                self.newComment("");

                return self.pager.last();
            });
        };

        self.activate = function () {
            return news.get(newsId)
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return undefined;
                    }

                    var category = result.data.category;

                    self.isHighlighted(result.data.isHighlighted);
                    self.title(result.data.title);
                    self.content(result.data.displayContent);
                    self.author(meta.author(result.data.author));
                    self.stamp(meta.stamp(result.data));
                    self.created(meta.created(result.data));
                    self.categoryName(category ? category.name : "");
                    self.imageUrl(category && category.headerImageUrl ? category.headerImageUrl : null);

                    return comments.settings().then(function (settings) {
                        self.commentsEnabled(settings.news);

                        return settings.news ? self.refreshComments() : undefined;
                    });
                })
                .finally(function () {
                    self.loading(false);
                });
        };
    };
});
