define(["ko", "models/layouts", "models/invalidation", "models/restriction", "models/i18n"], function (ko, layouts, invalidation, restriction, i18n) {
    var main = null;
    var host = null;
    var route = "#/";
    var mounted = [];
    var error = null;
    var generation = 0;

    var clear = function () {
        if (host) {
            host.parentNode.insertBefore(main, host);
            main.classList.remove("behind-layout-content");
            mounted.forEach(function (element) {
                ko.cleanNode(element);
            });
            mounted = [];
            host.remove();
            host = null;
        }
    };

    var showError = function () {
        if (!error) {
            error = document.createElement("div");
            error.className = "container alert alert-warning";
            error.setAttribute("role", "alert");
            var anchor = host || main;
            anchor.parentNode.insertBefore(error, anchor);
        }
        error.textContent = i18n.t("layouts.error.load");
    };

    var render = function () {
        if (!main) {
            return;
        }

        clear();

        if (restriction.restricted() || /^#\/(?:manage(?:\/|$)|administration$|settings$|sites(?:\/|$)|users$|roles$|sign[-\/]|profile(?:\/|$)|session)/.test(route)) {
            if (error) {
                error.remove();
                error = null;
            }
            return;
        }

        var configuration = layouts.current();
        var layout = configuration && layouts.select(configuration, route);

        if (!layout) {
            return;
        }

        var all = layout.sections.reduce(function (widgets, section) {
            return widgets.concat(section.widgets);
        }, []);

        if (all.length === 1 && all[0].type === "core.content") {
            return;
        }

        host = document.createElement("div");
        host.className = "container my-4 behind-layout";
        main.parentNode.insertBefore(host, main);
        main.classList.add("behind-layout-content");

        layout.sections.forEach(function (section) {
            var grid = document.createElement("section");
            grid.className = "behind-layout-section";
            grid.style.gridTemplateColumns = "repeat(" + section.columns + ", minmax(0, 1fr))";
            grid.style.gridTemplateRows = "repeat(" + section.rows + ", auto)";
            host.appendChild(grid);

            section.widgets.forEach(function (widget) {
                var cell = document.createElement("div");
                cell.className = "behind-layout-cell";
                cell.style.gridRow = widget.row + " / span " + widget.rowSpan;
                cell.style.gridColumn = widget.column + " / span " + widget.columnSpan;
                grid.appendChild(cell);

                if (widget.type === "core.content") {
                    cell.appendChild(main);
                } else {
                    mounted.push(cell);
                    ko.applyBindingsToNode(cell, {
                        component: { name: "site-widget", params: { widget: widget } }
                    });
                }
            });
        });
    };

    return {
        start: function () {
            main = document.getElementById("main-container");
            invalidation.subscribe("layouts", render);
            if (restriction.restricted()) {
                return Promise.resolve();
            }
            return layouts.load().then(render).catch(function (failure) {
                console.error("Layout configuration failed to load", failure);
                showError();
            });
        },
        route: function (value) {
            route = value;
            generation++;
            render();
            if (!restriction.restricted() && !layouts.current()) {
                return this.refresh();
            }
        },
        refresh: function () {
            var current = ++generation;
            return layouts.load().then(function () {
                if (current === generation) {
                    if (error) {
                        error.remove();
                        error = null;
                    }
                    render();
                }
            }).catch(function (failure) {
                console.error("Layout configuration failed to refresh", failure);
                showError();
            });
        }
    };
});
