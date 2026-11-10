// Bundle the recorded Mac source baseline plus the shared chart-alignment port.
// Apply Windows-only adaptations here so their review boundary stays explicit.
// The editor finds the Windows host bridge (window.statsDirectReportHost) and the
// platform's shortcut labels itself, and both sanitizers allow border longhands;
// what remains is the drag-copy modifier and the Windows report presentation.
import {build} from './upstream/Report/node_modules/esbuild/lib/main.js';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {windowsExportStyle} from './export-style.mjs';
const upstream=name=>fileURLToPath(new URL('./upstream/Report/'+name,import.meta.url));
const vendor=name=>fileURLToPath(new URL('../StatsDirectUI/Assets/ReportEditor/vendor/'+name,import.meta.url));
// Bundle only what the manifest states. A failed or skipped "pnpm install" leaves older packages in place,
// and esbuild bundles whatever is there: 5.3.9 shipped DOMPurify 3.4.16 that way, with package.json, the lock
// file and the third-party notice all at 3.4.17.
const manifest=JSON.parse(await readFile(upstream('package.json'),'utf8'));
for(const [name,stated] of Object.entries({...manifest.dependencies,...manifest.devDependencies})){
  const installed=await readFile(upstream('node_modules/'+name+'/package.json'),'utf8').then(text=>JSON.parse(text).version,()=>'nothing');
  if(installed!==stated)throw new Error('Refusing to bundle: package.json states '+name+' '+stated+', but '+installed+' is installed in upstream/Report/node_modules. Run "pnpm --dir ReportEditor/upstream/Report install --frozen-lockfile" and see that it succeeds.');
}

const bundle=(entry,globalName,plugins)=>build({entryPoints:[upstream(entry+'.mjs')],bundle:true,format:'iife',globalName,target:'chrome120',minify:true,legalComments:'eof',outfile:vendor(entry+'.js'),plugins});

// Windows copies a drag with Ctrl where macOS uses Option, and a Ctrl+click is an
// ordinary click (on macOS it opens the context menu).
await bundle('editor','StatsDirectReportEditor',[{name:'windows-report-transfer',setup(b){b.onLoad({filter:/transfer\.mjs$/},async ({path})=>{
  let contents=await readFile(path,'utf8');
  const drift=message=>{throw new Error('Review the Windows transfer adapter for the new upstream source: '+message);};
  if((contents.match(/event\.altKey/g)||[]).length!==3)drift('Option-drag copy tests');
  if(!contents.includes('||event.ctrlKey||event.metaKey||event.shiftKey'))drift('modified pointer guard');
  contents=contents.replace('||event.ctrlKey||event.metaKey||event.shiftKey','||event.metaKey||event.shiftKey').replaceAll('event.altKey','event.ctrlKey');
  return {contents,loader:'js'};
});}}]);

await bundle('export','StatsDirectReportExport',[{name:'windows-report-export-style',setup(b){b.onLoad({filter:/export\.mjs$/},async ({path})=>({contents:windowsExportStyle(await readFile(path,'utf8')),loader:'js'}));}}]);

// Windows imports inside the report WebView, where workspace.css also applies.
// Tag only the temporary container so plain imports use the host's chart defaults.
await bundle('import','StatsDirectReportImport',[{name:'windows-report-import-style',setup(b){b.onLoad({filter:/import\.mjs$/},async ({path})=>{
  let contents=await readFile(path,'utf8');
  const before="const container=document.createElement('div');container.append";
  if(!contents.includes(before))throw Error('Review the Windows import container adapter');
  contents=contents.replace(before,"const container=document.createElement('div');container.className='report-import';container.append");
  return {contents,loader:'js'};
});}}]);
