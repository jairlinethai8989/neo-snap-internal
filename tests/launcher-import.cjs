const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),vm=require('node:vm');
const root=path.resolve(__dirname,'../src/SnapCraft/Assets');
const script=fs.readFileSync(path.join(root,'launcher.js'),'utf8');
const sent=[],element={};
// The launcher script wires this action before initializing other controls.
vm.runInNewContext(script.split('\n').slice(0,3).join('\n'),{window:{chrome:{webview:{postMessage:m=>sent.push(m)}}},document:{querySelector:()=>element}});
element.onclick();
assert.equal(sent[0].action,'browseImages','Launcher folder must browse images, not projects');
console.log('PASS launcher browse image action');
