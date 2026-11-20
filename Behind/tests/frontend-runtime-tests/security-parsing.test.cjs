const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const { performance } = require('node:perf_hooks');
const frontend = path.resolve(__dirname, '../../src/Core/Behind.Core.WebApi/Frontend');
let bbcode;
vm.runInNewContext(fs.readFileSync(path.join(frontend, 'models/bbcode.js'), 'utf8'), {
    define: (dependencies, factory) => { bbcode = factory(); }
});

test('malformed arguments and unmatched code delimiters render in bounded time', () => {
    for (const input of ['[url='.repeat(10000), '[code]'.repeat(10000)]) {
        const start = performance.now();
        assert.equal(bbcode.render(input), input);
        assert.ok(performance.now() - start < 1000);
    }
});

test('BBCode preserves supported tags and escapes markup', () => {
    assert.equal(bbcode.render('[b]bold[/b] [code]<script>[/code]'), '<strong>bold</strong> <pre><code>&lt;script&gt;</code></pre>');
    assert.equal(bbcode.render('[url=javascript:alert(1)]click[/url]'), 'click');
    assert.equal(bbcode.render('[code]a[/code][code]b[/code]'), '<pre><code>a</code></pre><pre><code>b</code></pre>');
});

test('rich editor serialization uses inert content and excludes video', () => {
    const source = fs.readFileSync(path.join(frontend, 'bindings/richeditor.js'), 'utf8');
    assert.match(source, /createElement\("template"\)/);
    assert.match(source, /container\.content\.querySelectorAll/);
    assert.match(source, /format !== "video"/);
});
