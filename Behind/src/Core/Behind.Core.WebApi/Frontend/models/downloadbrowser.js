define(["ko", "models/downloads", "models/downloadtree", "models/filetypes", "models/newsmeta"], function (ko, downloads, tree, filetypes, meta) {
    var unavailableFolder = "(unavailable folder)";
    var expanded = {};

    var pathOf = function (params) {
        var splat = params && params.splat;

        return tree.segmentsOf(Array.isArray(splat) ? splat[0] : splat).join("/");
    };

    var flatten = function (nodes) {
        return nodes.reduce(function (all, node) {
            return all.concat([node], flatten(node.children));
        }, []);
    };

    return function createBrowser(config) {
        var browser = {};
        var requestedPath = pathOf(config.params);
        var folderIndex = { roots: [], byId: {} };

        var hrefOf = function (node) {
            return node ? config.rootHref + "/" + node.path : config.rootHref;
        };

        browser.rootHref = config.rootHref;
        browser.hrefOf = hrefOf;
        browser.loading = ko.observable(true);
        browser.message = ko.observable("");
        browser.missing = ko.observable(false);
        browser.maxFileBytes = ko.observable(0);
        browser.roots = ko.observableArray([]);
        browser.selected = ko.observable(null);
        browser.files = ko.observableArray([]);

        browser.hasMessage = ko.pureComputed(function () {
            return browser.message() !== "";
        });
        browser.found = ko.pureComputed(function () {
            return !browser.loading() && !browser.missing();
        });
        browser.hasFiles = ko.pureComputed(function () {
            return browser.files().length > 0;
        });
        browser.hasFolders = ko.pureComputed(function () {
            return browser.roots().length > 0;
        });
        browser.inFolder = ko.pureComputed(function () {
            return browser.selected() !== null;
        });
        browser.rootSelected = ko.pureComputed(function () {
            return browser.selected() === null;
        });
        browser.heading = ko.pureComputed(function () {
            return browser.selected() ? browser.selected().displayName : "Downloads";
        });
        browser.maxFileSize = ko.pureComputed(function () {
            return filetypes.sizeOf(browser.maxFileBytes());
        });
        browser.breadcrumbs = ko.pureComputed(function () {
            var current = browser.selected();

            return current
                ? tree.ancestorsOf(current.node).concat([current.node]).map(function (node) {
                    return { displayName: node.displayName, href: hrefOf(node) };
                })
                : [];
        });

        var presentNode = function (node) {
            var view = {
                id: node.id,
                node: node,
                slug: node.slug,
                path: node.path,
                displayName: node.displayName,
                href: hrefOf(node),
                children: node.children.map(presentNode),
                expanded: ko.observable(!!expanded[node.id])
            };

            view.hasChildren = view.children.length > 0;
            view.selected = ko.pureComputed(function () {
                return !!browser.selected() && browser.selected().id === view.id;
            });
            view.isOpen = ko.pureComputed(function () {
                return view.expanded() || view.selected();
            });
            view.toggle = function () {
                view.expanded(!view.expanded());
                expanded[view.id] = view.expanded();
            };

            return view;
        };

        browser.expand = function (id) {
            expanded[id] = true;
        };

        browser.presentFile = function (file) {
            var row = {
                id: file.id,
                folderId: file.folderId,
                name: file.displayName,
                fileName: file.fileName,
                showFileName: file.displayName !== file.fileName,
                icon: "fa-solid " + filetypes.iconOf(file.fileName, file.contentType),
                size: filetypes.sizeOf(file.length),
                uploadedAt: meta.formatDate(file.uploadedAt),
                uploadedBy: meta.author(file.uploadedBy),
                url: file.url
            };

            row.folderName = function () {
                var folder = folderIndex.byId[row.folderId];

                return folder ? folder.displayName : unavailableFolder;
            };

            return config.decorate ? config.decorate(row) : row;
        };

        var select = function (node) {
            tree.ancestorsOf(node).forEach(function (ancestor) {
                expanded[ancestor.id] = true;
            });

            browser.roots(folderIndex.roots.map(presentNode));

            var views = flatten(browser.roots());

            browser.selected(node ? views.filter(function (view) {
                return view.id === node.id;
            })[0] : null);
        };

        var loadTree = function (keep) {
            return downloads.folders().then(function (result) {
                if (!result.ok) {
                    browser.message(result.message);

                    return false;
                }

                folderIndex = tree.build(result.data.folders);
                browser.maxFileBytes(result.data.maxFileBytes);

                var wanted = keep ? folderIndex.byId[keep] : tree.find(folderIndex.roots, requestedPath);

                browser.missing(!keep && requestedPath !== "" && !wanted);
                select(wanted || null);

                return true;
            });
        };

        var loadFiles = function () {
            var current = browser.selected();

            if (!current) {
                browser.files([]);

                return Promise.resolve();
            }

            return downloads.files(current.id).then(function (result) {
                if (result.ok) {
                    browser.files(result.data.files.map(browser.presentFile));
                } else {
                    browser.message(result.message);
                }
            });
        };

        browser.refresh = function (keep, also) {
            return loadTree(keep).then(function (loaded) {
                return loaded ? Promise.all([loadFiles()].concat(also ? [also()] : [])) : null;
            });
        };

        browser.activate = function (also) {
            return browser.refresh(null, also).finally(function () {
                browser.loading(false);
            });
        };

        return browser;
    };
});
