const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");

test("shared folder browser preserves download links and supports callback navigation", () => {
    function component(name) {
        let result;
        const file = path.join(core, "components", name + ".js");
        vm.runInNewContext(fs.readFileSync(file, "utf8"), { define: (_, factory) => { result = factory(""); } }, { filename: file });
        return result;
    }
    const tree = component("downloadtree"), nav = component("folderbrowser");
    const navigation = [], node = { id: "folder" };
    const downloadTree = tree.viewModel.createViewModel({ nodes: [] });
    assert.equal(downloadTree.select(node), true);
    const graphTree = tree.viewModel.createViewModel({ nodes: [], selectFolder: item => navigation.push(item.id) });
    assert.equal(graphTree.select(node), false);
    const browser = nav.viewModel.createViewModel({ roots: [], rootLinks: [], hasFolders: () => false });
    assert.equal(browser.selectRoot({ href: "#/downloads" }), true);
    assert.equal(browser.selectRoot({ select: () => navigation.push("root") }), false);
    assert.deepEqual(navigation, ["folder", "root"]);
});
