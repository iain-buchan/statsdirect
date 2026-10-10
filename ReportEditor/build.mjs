// Keep the pinned Mac sources intact. The small host/platform adaptations are
// applied during bundling so that an upstream update has one reviewable boundary.
import {build} from './upstream/Report/node_modules/esbuild/lib/main.js';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {windowsExportStyle} from './export-style.mjs';
import {safeBorderStyle} from './safe-border-style.mjs';
await build({entryPoints:[fileURLToPath(new URL('./upstream/Report/editor.mjs',import.meta.url))],bundle:true,format:'iife',globalName:'StatsDirectReportEditor',target:'chrome120',minify:true,legalComments:'eof',outfile:fileURLToPath(new URL('../StatsDirectUI/Assets/ReportEditor/vendor/editor.js',import.meta.url)),plugins:[{name:'windows-report-host',setup(b){b.onLoad({filter:/(editor|transfer)\.mjs$/},async ({path})=>{
  let contents=await readFile(path,'utf8');
  const replace=(from,to)=>{if(!contents.includes(from))throw new Error('Review the Windows selection adapter for the new upstream source: '+from);contents=contents.replace(from,to);};
  if(path.endsWith('editor.mjs')) {
    contents=contents.replace('window.webkit.messageHandlers.statsDirectReport.postMessage(message)','window.statsDirectReportHost.post(message)').replaceAll('⌘','Ctrl+');
    contents="import {installDeletion} from 'windows-report-deletion';\nexport {detachPictureSizing as detach} from './chart-size.mjs';\n"+contents;
    replace("installTransfer({isEditing:()=>editing,serialize,protect,detach:detachPictureSizing,post,remember});","installTransfer({isEditing:()=>editing,serialize,protect,detach:detachPictureSizing,post,remember});installDeletion({isEditing:()=>editing,serialize,post,remember});");
    // Windows reports open as editors, so the optional reading-mode toggle is absent.
    replace("const toggle=document.getElementById('report-edit-toggle');toggle.textContent=value?'Done':'Edit report';toggle.title=value?'Done editing':'Edit report';toggle.setAttribute('aria-label',toggle.title);toggle.setAttribute('aria-pressed',String(value));", "const toggle=document.getElementById('report-edit-toggle');if(toggle){toggle.textContent=value?'Done':'Edit report';toggle.title=value?'Done editing':'Edit report';toggle.setAttribute('aria-label',toggle.title);toggle.setAttribute('aria-pressed',String(value));}");
    replace("const button=document.createElement('button');button.textContent='Remove plot';", "wrapper.tabIndex=0;wrapper.setAttribute('aria-label','Chart');");
    replace("button.onclick=()=>post({action:'hideChart',resultID:body.dataset.resultId,index:chart});controls.append(button);", "");
    replace("img.replaceWith(wrapper);wrapper.append(controls,img);", "wrapper.tabIndex=0;wrapper.setAttribute('aria-label','Picture');img.replaceWith(wrapper);wrapper.append(controls,img);");
  } else {
    contents="export {segments,deletePiece,currentRange,selectRange};\n"+contents.replaceAll('event.altKey','event.ctrlKey').replace('||event.ctrlKey||event.metaKey','||event.metaKey');
  }
  return {contents,loader:'js'};
});b.onResolve({filter:/^windows-report-deletion$/},()=>({path:fileURLToPath(new URL('./deletion.mjs',import.meta.url))}));}},safeBorderStyle]});

// Word reads inline runs, not CSS selectors. Resolve report presentation in
// the detached export copy, using the same helper as the shared clipboard.
// Never change the live editor: exporting must not create edits or undo steps.
await build({entryPoints:[fileURLToPath(new URL('./upstream/Report/export.mjs',import.meta.url))],bundle:true,format:'iife',globalName:'StatsDirectReportExport',target:'chrome120',minify:true,legalComments:'eof',outfile:fileURLToPath(new URL('../StatsDirectUI/Assets/ReportEditor/vendor/export.js',import.meta.url)),plugins:[{name:'windows-report-export-style',setup(b){b.onLoad({filter:/export\.mjs$/},async ({path})=>{
  let contents=windowsExportStyle(await readFile(path,'utf8'));
  const marker='const clone=document.documentElement.cloneNode(true);';
  if(!contents.includes(marker))throw new Error('Review the export presentation adapter for the new upstream source.');
  contents="import {inlinePresentation} from './fragment.mjs';\n"+contents.replace(marker,marker+`
  const styledOriginals=[...document.body.querySelectorAll('*')];
  const styledCopies=[...clone.querySelector('body').querySelectorAll('*')];
  styledOriginals.forEach((element,index)=>{
    if(!element.closest('svg,.report-controls'))inlinePresentation(element,styledCopies[index]);
  });`);
  return {contents,loader:'js'};
});}},safeBorderStyle]});

await build({entryPoints:[fileURLToPath(new URL('./upstream/Report/import.mjs',import.meta.url))],bundle:true,format:'iife',globalName:'StatsDirectReportImport',target:'chrome120',minify:true,legalComments:'eof',outfile:fileURLToPath(new URL('../StatsDirectUI/Assets/ReportEditor/vendor/import.js',import.meta.url)),plugins:[safeBorderStyle]});
