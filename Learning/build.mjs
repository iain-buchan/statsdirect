import {build} from 'esbuild';
import {readFile,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {selectionPort} from './selection-port.mjs';
import {recoveryPort,replayLinks} from './recovery-port.mjs';
const local=path=>fileURLToPath(new URL(path,import.meta.url));
await build({
  entryPoints:[local('./upstream/Learn/app.mjs')],bundle:true,format:'iife',target:'safari17',
  outfile:local('../StatsDirectUI/Assets/Learn/app.js'),
  plugins:[{name:'windows-learning-selection',setup(b){
    b.onLoad({filter:/[\\/]upstream[\\/]Learn[\\/]app\.mjs$/},async({path})=>({contents:recoveryPort(selectionPort(await readFile(path,'utf8'))),loader:'js'}));
    b.onLoad({filter:/[\\/]upstream[\\/]Learn[\\/]rich-text\.mjs$/},async({path})=>({contents:replayLinks(await readFile(path,'utf8')),loader:'js'}));
    b.onResolve({filter:/^\.\.\/Grid\/node_modules\/marked\/lib\/marked\.esm\.js$/},()=>({path:local('./node_modules/marked/lib/marked.esm.js')}));
    b.onResolve({filter:/^\.\.\/Content\/Learn\/lessons\.json$/},()=>({path:local('../StatsDirectUI/Assets/Learn/lessons.json')}));
  }}]
});
// Preserve the checked-in bundle's existing line endings for a focused diff.
const output=local('../StatsDirectUI/Assets/Learn/app.js');
await writeFile(output,(await readFile(output,'utf8')).replace(/\r\n?/g,'\n').replace(/\n/g,'\r\n'));
await build({
  entryPoints:[local('./conversation.mjs')],bundle:true,format:'iife',target:'chrome120',
  outfile:local('../StatsDirectUI/Assets/Tutor/conversation.js'),
  plugins:[{name:'windows-conversation',setup(b){
    b.onResolve({filter:/^\.\.\/Grid\/node_modules\/marked\/lib\/marked\.esm\.js$/},()=>({path:local('./node_modules/marked/lib/marked.esm.js')}));
    // Preserve the Windows transcript's explicit line breaks while sharing the
    // sanitized Markdown, equations and code renderer with Learning.
    b.onLoad({filter:/[\\/]rich-text\.mjs$/},async({path})=>({contents:(await readFile(path,'utf8')).replace('mangle:false','breaks:true,mangle:false'),loader:'js'}));
  }}]
});
const transcript=local('../StatsDirectUI/Assets/Tutor/conversation.js');
await writeFile(transcript,(await readFile(transcript,'utf8')).replace(/\r\n?/g,'\n').replace(/\n/g,'\r\n'));
