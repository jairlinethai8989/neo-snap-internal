const assert = require('node:assert/strict');
const {translateText, normalizeLanguage} = require('../src/SnapCraft/Assets/i18n.js');

assert.equal(normalizeLanguage('th'), 'th');
assert.equal(normalizeLanguage('en'), 'en');
assert.equal(normalizeLanguage('fr'), 'en');
assert.equal(translateText('จับภาพยาว', 'en', 'Snapzy'), 'Scrolling capture');
assert.equal(translateText('Scrolling capture', 'th', 'Neo Snap'), 'จับภาพยาว');
assert.equal(translateText('เกี่ยวกับ Neo Snap', 'en', 'Snapzy'), 'About Snapzy');
assert.equal(translateText('About Snapzy', 'th', 'Neo Snap'), 'เกี่ยวกับ Neo Snap');
assert.equal(translateText('About SnapZy', 'th', 'SnapZy'), 'เกี่ยวกับ SnapZy');
assert.equal(translateText('เกี่ยวกับ SnapZy', 'en', 'SnapZy'), 'About SnapZy');
assert.equal(require('../src/SnapCraft/Assets/i18n.js').formatReleaseLabel('1.0.0', 'en'), "What's new in 1.0.0");
assert.equal(require('../src/SnapCraft/Assets/i18n.js').formatReleaseLabel('0.1.20', 'th'), 'มีอะไรใหม่ใน 0.1.20');
assert.equal(translateText('  100%  ', 'en', 'Snapzy'), '  100%  ');
console.log('localization: language normalization, round-trip text, product branding, and whitespace: pass');
