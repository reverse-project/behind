const fs = require('fs');
const vm = require('vm');
const assert = require('assert/strict');
const path = require('path');
const source = fs.readFileSync(path.resolve(__dirname, '../../../src/Core/Behind.Core.WebApi/Frontend/models/auth.js'),'utf8');
(async () => {
 for (const allowed of [false, true]) {
  let auth, started = 0, confirmed = false;
  vm.runInNewContext(source, {
   define: (_, factory) => { auth = factory(
    {post: async () => ({ok:true,data:{accessToken:'temporary',refreshToken:'refresh'}})},
    {start: () => { assert(confirmed); started++; }},
    {label: u => u.displayName+'#'+u.id},
    {t: key => key === 'magiclink.confirmAccount' ? 'Sign in as {account}?' : key}, {}); },
   fetch: async (path, options) => {
    assert.equal(path,'/session'); assert.equal(options.headers.Authorization,'Bearer temporary');
    return {ok:true,json:async()=>({user:{id:'account',displayName:'Ada'}})};
   },
   window: {confirm: text => {assert.equal(text,'Sign in as Ada#account?'); confirmed=true; return allowed;} }
  });
  await auth.signInWithMagicLink('link');
  assert.equal(started, allowed ? 1 : 0);
 }
 console.log('PASS: magic-link confirmation identifies account and gates session installation');
})();
