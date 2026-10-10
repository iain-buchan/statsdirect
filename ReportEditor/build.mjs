// Keep the pinned Mac sources intact. The small host/platform adaptations are
// applied during bundling so that an upstream update has one reviewable boundary.
// The editor finds the Windows host bridge (window.statsDirectReportHost) and the
// platform's shortcut labels itself, and both sanitizers allow border longhands;
// what remains is the drag-copy modifier and the Windows report presentation.
import {build} from './upstream/Report/node_modules/esbuild/lib/main.js';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {windowsExportStyle} from './export-style.mjs';
const upstream=name=>fileURLToPath(new URL('./upstream/Report/'+name,import.meta.url));
const vendor=name=>fileURLToPath(new URL('../StatsDirectUI/Assets/ReportEditor/vendor/'+name,import.meta.url));
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

await bundle('import','StatsDirectReportImport',[]);
