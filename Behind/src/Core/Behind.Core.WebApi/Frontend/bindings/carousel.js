define(["ko", "bootstrap", "models/i18n"], function (ko, bootstrap, i18n) {
    var counter = 0;

    var element = function (tag, className, attributes) {
        var node = document.createElement(tag);

        if (className) {
            node.className = className;
        }

        Object.keys(attributes || {}).forEach(function (name) {
            node.setAttribute(name, attributes[name]);
        });

        return node;
    };

    var control = function (direction, id, label) {
        var button = element("button", "carousel-control-" + direction, {
            type: "button",
            "data-bs-target": "#" + id,
            "data-bs-slide": direction
        });
        var icon = element("span", "carousel-control-" + direction + "-icon", { "aria-hidden": "true" });
        var text = element("span", "visually-hidden");

        text.textContent = label;
        button.appendChild(icon);
        button.appendChild(text);

        return button;
    };

    var indicator = function (id, index, label) {
        return element("button", index === 0 ? "active" : "", {
            type: "button",
            "data-bs-target": "#" + id,
            "data-bs-slide-to": String(index),
            "aria-label": label,
            "aria-current": index === 0 ? "true" : "false"
        });
    };

    var slideOf = function (slide, index) {
        var item = element("div", index === 0 ? "carousel-item active" : "carousel-item");
        var image = element("img", "d-block w-100 behind-carousel-image", {
            src: slide.imageUrl,
            alt: slide.caption
        });
        var frame = slide.linkUrl
            ? element("a", "d-block", { href: slide.linkUrl, rel: "noopener noreferrer" })
            : item;

        frame.appendChild(image);

        if (frame !== item) {
            item.appendChild(frame);
        }

        if (slide.caption) {
            var caption = element("div", "carousel-caption d-block");
            var text = element("p", "mb-0 text-break");

            text.textContent = slide.caption;
            caption.appendChild(text);
            item.appendChild(caption);
        }

        return item;
    };

    var build = function (data, id) {
        var root = element("div", "carousel slide behind-carousel", {
            id: id,
            role: "region",
            "aria-roledescription": "carousel",
            "aria-label": i18n.t("carousel.label")
        });
        var inner = element("div", "carousel-inner");

        data.slides.forEach(function (slide, index) {
            inner.appendChild(slideOf(slide, index));
        });

        if (data.slides.length > 1) {
            var indicators = element("div", "carousel-indicators");

            data.slides.forEach(function (slide, index) {
                indicators.appendChild(indicator(id, index, i18n.t("carousel.goTo", [index + 1])));
            });

            root.appendChild(indicators);
        }

        root.appendChild(inner);

        if (data.slides.length > 1) {
            root.appendChild(control("prev", id, i18n.t("carousel.previous")));
            root.appendChild(control("next", id, i18n.t("carousel.next")));
        }

        return root;
    };

    var prefersReducedMotion = function () {
        return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    };

    var start = function (root, data) {
        var slides = data.slides.length;

        return new bootstrap.Carousel(root, {
            interval: data.intervalSeconds * 1000,
            ride: slides > 1 && !prefersReducedMotion() ? "carousel" : false,
            pause: "hover",
            touch: true,
            wrap: true,
            keyboard: true
        });
    };

    ko.bindingHandlers.carousel = {
        init: function (host, valueAccessor) {
            var instance = null;

            var stop = function () {
                if (instance) {
                    instance.dispose();
                    instance = null;
                }

                host.textContent = "";
            };

            var render = ko.computed(function () {
                var data = ko.unwrap(valueAccessor());

                stop();

                if (!data || !data.slides || data.slides.length === 0) {
                    return;
                }

                var root = build(data, "behind-carousel-" + (++counter));

                host.appendChild(root);
                instance = start(root, data);
            });

            ko.utils.domNodeDisposal.addDisposeCallback(host, function () {
                render.dispose();
                stop();
            });
        }
    };

    return { build: build };
});
