StatsDirectReportEditor.start({editing:true});
document.querySelector('[data-command="addText"]').onclick=()=>StatsDirectReportEditor.command('addText');
// Text and object deletion share the editor's selection-aware undo shortcuts.
// Keep browser Save Page/Refresh from bypassing the report's save/dirty logic.
document.addEventListener('keydown',e=>{
  const key=e.key.toLowerCase();
  if(key==='f5'||e.ctrlKey&&key==='r'){e.preventDefault();return;}
  if(key==='f1'){e.preventDefault();const body=document.activeElement?.closest('.report-body');const entry=entries.find(x=>x.id===body?.dataset.resultId);chrome.webview.postMessage({action:'help',context:entry?.helpContextId??0});return;}
  if(e.ctrlKey&&['s','o','p'].includes(key)){e.preventDefault();chrome.webview.postMessage({action:'command',command:key==='s'?(e.shiftKey?'saveAs':'save'):key==='o'?'open':'print'});}
},true);
chrome.webview.postMessage({action:'ready'});
