define(function () {
    var separator = "/";

    var pathOf = function (node) {
        return node.parent ? pathOf(node.parent) + separator + node.slug : node.slug;
    };

    var build = function (folders) {
        var byId = {};
        var roots = [];

        folders.forEach(function (folder) {
            byId[folder.id] = {
                id: folder.id,
                parentId: folder.parentId,
                slug: folder.slug,
                displayName: folder.displayName,
                parent: null,
                children: []
            };
        });

        folders.forEach(function (folder) {
            var node = byId[folder.id];
            var parent = folder.parentId ? byId[folder.parentId] : null;

            if (parent) {
                node.parent = parent;
                parent.children.push(node);
            } else if (!folder.parentId) {
                roots.push(node);
            }
        });

        Object.keys(byId).forEach(function (id) {
            byId[id].path = pathOf(byId[id]);
        });

        return { roots: roots, byId: byId };
    };

    var segmentsOf = function (path) {
        return String(path || "").split(separator).filter(function (segment) {
            return segment !== "";
        });
    };

    var find = function (roots, path) {
        var segments = segmentsOf(path);

        if (segments.length === 0) {
            return null;
        }

        return segments.reduce(function (found, segment) {
            var level = found === undefined ? roots : found ? found.children : [];
            var match = level.filter(function (node) {
                return node.slug === segment;
            })[0];

            return match || null;
        }, undefined);
    };

    var ancestorsOf = function (node) {
        return node && node.parent ? ancestorsOf(node.parent).concat([node.parent]) : [];
    };

    return {
        build: build,
        find: find,
        ancestorsOf: ancestorsOf,
        segmentsOf: segmentsOf
    };
});
