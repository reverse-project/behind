define([], function () {
    var topics = new Map();

    var requireTopic = function (topic) {
        if (typeof topic !== "string" || topic.trim() === "") {
            throw new TypeError("An invalidation topic is required.");
        }
    };

    return {
        subscribe: function (topic, handler) {
            requireTopic(topic);

            if (typeof handler !== "function") {
                throw new TypeError("An invalidation handler is required.");
            }

            var subscriptions = topics.get(topic);

            if (!subscriptions) {
                subscriptions = new Set();
                topics.set(topic, subscriptions);
            }

            var subscription = { handler: handler };
            subscriptions.add(subscription);

            return {
                dispose: function () {
                    subscriptions.delete(subscription);

                    if (subscriptions.size === 0 && topics.get(topic) === subscriptions) {
                        topics.delete(topic);
                    }
                }
            };
        },

        invalidate: function (topic) {
            requireTopic(topic);

            var subscriptions = topics.get(topic);
            var pending = Array.from(subscriptions || []).map(function (subscription) {
                return Promise.resolve().then(function () {
                    if (subscriptions.has(subscription)) {
                        return subscription.handler();
                    }
                });
            });

            return Promise.allSettled(pending).then(function (results) {
                var errors = results.filter(function (result) {
                    return result.status === "rejected";
                }).map(function (result) {
                    return result.reason;
                });

                if (errors.length > 0) {
                    throw new AggregateError(errors, "Failed to invalidate " + topic + ".");
                }
            });
        }
    };
});
