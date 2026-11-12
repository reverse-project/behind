define(["ko", "models/layouts", "models/invalidation", "models/restriction", "models/i18n", "components/siteheader", "components/sitefooter"], function (ko, layouts, invalidation, restriction, i18n, siteHeader, siteFooter) {
    var shell = null;
    var main = null;
    var header = null;
    var footer = null;
    var headerTemplate = "";
    var footerTemplate = "";
    var headerSlot = null;
    var footerSlot = null;
    var context = null;
    var route = "#/";
    var host = null;
    var mounted = [];
    var error = null;
    var generation = 0;
    var activeRefresh = 0;
    var fixedBindings = false;
    var managementRoute = /^#\/(?:manage(?:\/|$)|administration$|settings$|sites(?:\/|$)|users$|roles$|audit-log$|profile(?:\/|$)|session|sign(?:[-/]|$))/;

    var addTokens = function (element, value) {
        if (typeof value !== "string") {
            return;
        }

        value.trim().split(/\s+/).filter(Boolean).forEach(function (token) {
            element.classList.add(token);
        });
    };

    var applyLayer = function (element, value) {
        var layer = Number(value);
        if (!isNaN(layer) && layer !== 0) {
            element.style.zIndex = layer;
        }
    };

    var restoreRoot = function (element, slot) {
        if (slot && slot.parentNode && element.parentNode !== slot.parentNode) {
            slot.parentNode.insertBefore(element, slot.nextSibling);
        }
    };

    var restoreRoots = function () {
        restoreRoot(header, headerSlot);
        restoreRoot(footer, footerSlot);
        if (header) {
            header.hidden = false;
        }
        if (footer) {
            footer.hidden = false;
        }
    };

    var clearError = function () {
        if (error) {
            error.remove();
            error = null;
        }
    };

    var clear = function () {
        if (host) {
            if (main.parentNode) {
                host.parentNode.insertBefore(main, host);
            }
            main.classList.remove("behind-layout-content");
            mounted.forEach(function (element) {
                ko.cleanNode(element);
                if (element.parentNode) {
                    element.remove();
                }
            });
            mounted = [];
            host.remove();
            host = null;
        }
        restoreRoots();
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

    var mountFixedShell = function () {
        restoreRoots();
        if (!fixedBindings) {
            if (header) {
                siteHeader.mount(header, { context: context, template: headerTemplate });
            }
            if (footer) {
                siteFooter.mount(footer, { template: footerTemplate });
            }
            fixedBindings = true;
        }
    };

    var preparePublicShell = function () {
        if (fixedBindings) {
            if (header) {
                ko.cleanNode(header);
            }
            if (footer) {
                ko.cleanNode(footer);
            }
            fixedBindings = false;
        }
        if (header) {
            header.remove();
        }
        if (footer) {
            footer.remove();
        }
    };

    var mountChromeWidget = function (widget, cell, used) {
        var isHeader = widget.type === "core.header";
        var element;
        if (!used[widget.type]) {
            element = isHeader ? header : footer;
            used[widget.type] = true;
        }
        if (!element) {
            element = document.createElement(isHeader ? "header" : "footer");
            element.classList.add(isHeader ? "behind-page-header" : "behind-page-footer");
        }

        cell.appendChild(element);
        mounted.push(element);
        if (isHeader) {
            siteHeader.mount(element, { context: context, template: headerTemplate, hamburgerPosition: widget.hamburgerPosition });
        } else {
            siteFooter.mount(element, { template: footerTemplate });
        }
    };

    var hasWidget = function (section, type) {
        return section.widgets.some(function (widget) {
            return widget.type === type;
        });
    };

    var renderBadges = function (badges) {
        if (!badges || !badges.length) {
            return;
        }
        var overlay = document.createElement("div");
        overlay.className = "behind-layout-badges";
        host.appendChild(overlay);
        ["top-left", "top-right", "bottom-left", "bottom-right"].forEach(function (position) {
            var corner = document.createElement("div");
            corner.className = "behind-layout-badge-corner behind-layout-badge-" + position;
            badges.filter(function (badge) {
                return badge.position === position;
            }).forEach(function (badge) {
                var element = document.createElement(badge.link ? "a" : "div");
                element.className = "behind-layout-badge";
                element.style.width = badge.width + "px";
                if (badge.link) {
                    element.setAttribute("href", badge.link);
                }
                if (badge.image) {
                    var image = document.createElement("img");
                    image.setAttribute("src", badge.image);
                    image.setAttribute("alt", badge.text);
                    image.setAttribute("decoding", "async");
                    element.appendChild(image);
                } else {
                    element.classList.add("behind-layout-badge-text");
                    element.textContent = badge.text;
                }
                corner.appendChild(element);
            });
            if (corner.children.length) {
                overlay.appendChild(corner);
            }
        });
    };

    var render = function () {
        if (!main) {
            return;
        }

        clear();

        if (restriction.restricted() || managementRoute.test(route)) {
            mountFixedShell();
            return;
        }

        var configuration = layouts.current();
        var layout = configuration && layouts.select(configuration, route);
        if (!layout) {
            mountFixedShell();
            return;
        }

        preparePublicShell();
        host = document.createElement("div");
        host.className = "behind-layout";
        shell.insertBefore(host, main);
        main.classList.add("behind-layout-content");

        var usedChrome = {};
        var hasContent = false;
        layout.sections.forEach(function (section, index) {
            var full = section.width === "full";
            var band = document.createElement("div");
            band.className = full ? "behind-layout-band" : "container behind-layout-band";
            band.style.marginTop = hasWidget(section, "core.footer")
                ? "auto"
                : index ? -(section.overlap || 0) + "px" : "0px";
            applyLayer(band, section.layer);
            host.appendChild(band);

            var grid = document.createElement("section");
            grid.className = "behind-layout-section " + (full ? "behind-layout-section-full" : "behind-layout-section-site");
            addTokens(grid, section.cssClass);
            var overlaps = section.widgets.some(function (widget, widgetIndex) {
                return section.widgets.slice(widgetIndex + 1).some(function (other) {
                    return widget.row < other.row + other.rowSpan && other.row < widget.row + widget.rowSpan
                        && widget.column < other.column + other.columnSpan && other.column < widget.column + widget.columnSpan;
                });
            });
            if (overlaps) {
                grid.classList.add("behind-layout-section-overlapping");
            }
            grid.style.gridTemplateColumns = "repeat(" + section.columns + ", minmax(0, 1fr))";
            grid.style.gridTemplateRows = "repeat(" + section.rows + ", auto)";
            band.appendChild(grid);

            section.widgets.forEach(function (widget) {
                var cell = document.createElement("div");
                cell.className = "behind-layout-cell";
                addTokens(cell, widget.cssClass);
                if (widget.borderless) {
                    cell.classList.add("behind-layout-cell-borderless");
                }
                if (widget.noMargin) {
                    cell.classList.add("behind-layout-cell-no-margin");
                }
                if (widget.noPadding) {
                    cell.classList.add("behind-layout-cell-no-padding");
                }
                cell.style.gridRow = widget.row + " / span " + widget.rowSpan;
                cell.style.gridColumn = widget.column + " / span " + widget.columnSpan;
                applyLayer(cell, widget.layer);
                grid.appendChild(cell);

                if (widget.type === "core.content") {
                    hasContent = true;
                    cell.appendChild(main);
                } else if (widget.type === "core.header" || widget.type === "core.footer") {
                    mountChromeWidget(widget, cell, usedChrome);
                } else {
                    mounted.push(cell);
                    ko.applyBindingsToNode(cell, {
                        component: { name: "site-widget", params: { widget: widget } }
                    });
                }
            });
        });

        if (!hasContent) {
            host.appendChild(main);
        }
        renderBadges(layout.badges);
    };

    var refresh = function () {
        var current = ++generation;
        activeRefresh = current;
        return layouts.load().then(function () {
            if (current === activeRefresh) {
                activeRefresh = 0;
                clearError();
                render();
            }
        }).catch(function (failure) {
            if (current === activeRefresh) {
                activeRefresh = 0;
                console.error("Layout configuration failed to load", failure);
                try {
                    render();
                } catch (renderFailure) {
                    console.error("Layout configuration failed to render", renderFailure);
                    clear();
                    mountFixedShell();
                }
                showError();
            }
        });
    };

    return {
        isManagementRoute: function (value) {
            return managementRoute.test(value || "");
        },
        start: function (appContext) {
            context = appContext;
            shell = document.getElementById("application-shell");
            main = document.getElementById("main-container");
            header = document.getElementById("header-container");
            footer = document.getElementById("footer-container");
            if (header === main) {
                header = null;
            }
            if (footer === main) {
                footer = null;
            }
            shell = shell || main.parentNode;
            if (header) {
                headerSlot = document.createComment("header");
                shell.insertBefore(headerSlot, header);
            }
            if (footer) {
                footerSlot = document.createComment("footer");
                shell.insertBefore(footerSlot, footer);
            }
            headerTemplate = header ? header.innerHTML : "";
            footerTemplate = footer ? footer.innerHTML : "";
            invalidation.subscribe("layouts", refresh);
            if (restriction.restricted()) {
                mountFixedShell();
                return Promise.resolve();
            }
            return refresh();
        },
        route: function (value) {
            route = value;
            generation++;
            render();
            if (!restriction.restricted() && !layouts.current()) {
                return refresh();
            }
        },
        refresh: refresh
    };
});
