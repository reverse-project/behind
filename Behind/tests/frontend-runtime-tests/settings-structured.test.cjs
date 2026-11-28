const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const core = path.resolve(__dirname, '../../src/Core/Behind.Core.WebApi/Frontend');
const ko = require(path.join(core, 'libraries/knockout.min.js'));

function setup(files, readOnly = false) {
    const saved = [];
    const grants = ko.observable(true);
    const settings = {
        load: async () => ({ ok: true, data: { readOnly, files } }),
        save: async (name, values) => { saved.push({ name, values }); return { ok: true, message: 'saved' }; },
        saveAppSettings: async (name, values) => { saved.push({ name, values }); return { ok: true, message: 'saved' }; }
    };
    function Editor() {
        this.loads = 0;
        this.invalidate = () => {};
        this.key = ko.observable("");
        this.load = async () => { this.loads++; return { ok: true }; };
        this.canSave = ko.observable(true);
        this.request = () => ({});
    }
    const deps = {
        ko, 'models/settings': settings, 'models/session': { isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => grants() },
        'models/i18n': { t: key => key }, 'viewmodels/emailsettings': Editor, 'viewmodels/loggingsettings': Editor,
        'viewmodels/opentelemetrysettings': Editor, 'viewmodels/oidcsettings': Editor, 'viewmodels/forwardedheaderssettings': Editor
    };
    let Settings;
    vm.runInNewContext(fs.readFileSync(path.join(core, 'viewmodels/settings.js'), 'utf8'), {
        define(names, factory) { Settings = factory(...names.map(name => deps[name])); }, Promise, URL,
        window: { location: { reload() {} } }
    });
    return { view: new Settings(), saved, grants };
}
const field = (path, type, value, sensitive = false) => ({ path, type, value, sensitive, editable: true, action: null });
const file = (name, fields, editor = null) => ({ name, fields, editor });
const plain = value => JSON.parse(JSON.stringify(value));

test('MCP limits and origins are validated before saving', async () => {
    const { view } = setup([file('backend.json', [
        field('Backend:Mcp:MaxRequestBytes', 'number', '1048576'),
        field('Backend:Mcp:AllowedOrigins', 'array', '[]')
    ])]);
    await view.refresh();
    const [limit, origins] = view.selected().fields;
    limit.value('0');
    assert.equal(view.selected().isValid(), false);
    limit.value('2');
    origins.structured.add();
    origins.structured.items()[0].value('https://example.com/path');
    assert.equal(view.selected().isValid(), false);
    origins.structured.items()[0].value('https://example.com');
    assert.equal(view.selected().isValid(), true);
});

test('list and connection editors add, edit, remove, preserve secrets and discard', async () => {
    const { view, saved } = setup([file('caching.json', [
        field('ConnectionStrings', 'object', '{"Main":"ab...yz","Other":"...."}', true),
        field('ConnectionStrings:Main', 'string', 'ab...yz', true),
        field('Frontend:StaticFiles:CompressedMimeTypes', 'array', '["text/plain","text/html"]')
    ])]);
    await view.refresh();
    assert.equal(view.selected().fields.length, 2);
    const [connections, list] = view.selected().fields;
    const map = connections.structured;
    assert.equal(map.items()[0].value(), '');
    map.items()[0].name('Renamed');
    map.remove(map.items()[1]);
    map.add();
    map.items()[1].name('New');
    map.items()[1].value('new secret');
    list.structured.items()[0].value('application/json');
    list.structured.remove(list.structured.items()[1]);
    list.structured.add();
    list.structured.items()[1].value('image/svg+xml');
    await view.save();
    assert.deepEqual(plain(saved[0].values), {
        ConnectionStrings: { Renamed: 'ab...yz', New: 'new secret' },
        'Frontend:StaticFiles:CompressedMimeTypes': ['application/json', 'image/svg+xml']
    });
    const fresh = view.selected().fields[0];
    fresh.structured.add();
    assert.equal(view.selected().isValid(), false);
    assert.equal(view.selected().isChanged(), true);
    view.discard();
    assert.equal(view.selected().isValid(), true);
    assert.equal(view.selected().isChanged(), false);
});

test('connection editor handles null values and rejects invalid names', async () => {
    const { view } = setup([file('database.json', [
        field('ConnectionStrings', 'object', '{"Main":null,"Other":"ab...yz"}', true)
    ])]);
    await view.refresh();
    const editor = view.selected().fields[0].structured;
    assert.equal(editor.visual(), true);
    assert.equal(JSON.parse(editor.current()).Main, null);
    editor.add();
    editor.items()[2].name('bad:name');
    assert.equal(editor.valid(), false);
    editor.items()[2].name('Main');
    assert.equal(editor.valid(), false);
    editor.items()[2].name('New');
    assert.equal(editor.valid(), true);
});

test('Serilog custom entries and arguments retain types and secrets with app settings validation', async () => {
    const initial = '["Console",{"Name":"Custom","Args":{"apiKey":"ab...yz","count":3,"nested":{"secret":"...."}},"CustomProperty":{"enabled":true}}]';
    const { view, saved } = setup([file('appsettings.json', [
        field('Serilog:WriteTo', 'array', initial), field('Serilog:Using', 'array', '["Library"]'),
        field('Serilog:Enrich', 'array', '["FromLogContext"]'),
        field('Serilog:AuditTo', 'array', '[]')
    ], 'logging')]);
    await view.refresh();
    const [sink, using, enrich, audit] = view.selected().fields;
    const editor = sink.structured;
    const named = editor.items()[1];
    assert.equal(named.args()[0].value(), '');
    assert.equal(named.args()[1].jsonSensitive(), false);
    assert.equal(named.args()[2].jsonSensitive(), true);
    named.args()[1].value('4');
    editor.addArgument(named);
    named.args()[3].name('enabled');
    named.args()[3].json(true);
    named.args()[3].value('true');
    using.structured.add(); using.structured.items()[1].value('Another');
    enrich.structured.addNamed(); enrich.structured.items()[1].name('WithCustom');
    audit.structured.addNamed(); audit.structured.items()[0].name('Audit');
    await view.saveAppSettings();
    assert.deepEqual(plain(saved[0].values.values), {
        'Serilog:WriteTo': ['Console', { Name: 'Custom', Args: { apiKey: 'ab...yz', count: 4, nested: { secret: '....' }, enabled: true }, CustomProperty: { enabled: true } }],
        'Serilog:Using': ['Library', 'Another'], 'Serilog:Enrich': ['FromLogContext', { Name: 'WithCustom' }],
        'Serilog:AuditTo': [{ Name: 'Audit' }]
    });
    const fresh = view.selected().fields[0];
    fresh.structured.items()[1].args()[1].value('{');
    assert.equal(view.canSaveAppSettings(), false);
    await view.saveAppSettings();
    assert.equal(saved.length, 1);
    await view.discardAppSettings();
    assert.equal(view.selected().isValid(), true);
    assert.equal(view.logging.loads, 3);
    assert.equal(view.openTelemetry.loads, 3);
});

test('unsupported structures use validated JSON fallback and permissions prevent saves', async () => {
    const { view, saved, grants } = setup([file('appsettings.json', [field('Serilog:WriteTo', 'array', '[{"Name":"Odd","Args":[1],"extra":true}]')], 'logging')]);
    await view.refresh();
    const row = view.selected().fields[0];
    assert.equal(row.structured.visual(), false);
    row.value('{');
    assert.equal(view.selected().isValid(), false);
    await view.saveAppSettings();
    assert.equal(saved.length, 0);
    row.value('[{"Name":"Odd","Args":[2],"extra":true}]');
    assert.equal(view.selected().isValid(), true);
    grants(false);
    await view.saveAppSettings();
    assert.equal(saved.length, 0);
    grants(true);
    view.busy(true);
    await view.saveAppSettings();
    assert.equal(saved.length, 0);
    view.busy(false);
    await view.saveAppSettings();
    assert.deepEqual(plain(saved[0].values.values['Serilog:WriteTo']), [{ Name: 'Odd', Args: [2], extra: true }]);
    const ro = setup([file('database.json', [field('ConnectionStrings', 'object', '{}', true)])], true);
    await ro.view.refresh();
    ro.view.selected().fields[0].structured.add();
    await ro.view.save();
    assert.equal(ro.saved.length, 0);
});

test('JSON fallback hides and retains blank secrets on uniquely named entries', async () => {
    const { view, saved } = setup([file('appsettings.json', [
        field('Serilog:WriteTo', 'array', '[{"Name":"First","Args":[1],"apiKey":"ab...yz"},{"Name":"Second","Args":[2],"password":"cd...wx"}]')
    ], 'logging')]);
    await view.refresh();
    const row = view.selected().fields[0];
    row.value('[{"Name":"Second","Args":[3],"password":""},{"Name":"First","Args":[1],"apiKey":"replacement-secret"}]');
    assert.doesNotMatch(row.structured.preview(), /replacement-secret|cd\.\.\.wx/);
    await view.saveAppSettings();
    assert.deepEqual(plain(saved[0].values.values['Serilog:WriteTo']), [
        { Name: 'Second', Args: [3], password: 'cd...wx' },
        { Name: 'First', Args: [1], apiKey: 'replacement-secret' }
    ]);
});
