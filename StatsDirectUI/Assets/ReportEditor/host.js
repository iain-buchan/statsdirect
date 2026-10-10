/* Windows host for the pinned Mac editor. Document state and multi-section undo
 * stay together; C# owns files, clipboard, printing, window lifetime and help.
 * The report is one editable region (#results) and each result is a block in it,
 * identified by its result id; the editor replaces, extends and reorders the
 * blocks for this host (StatsDirectReportEditor.replace/replaceEntries/extend). */
const entries=[],undo=[],redo=[];
let revision=0,editing=true;
const encode=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const post=m=>chrome.webview.postMessage(m);
const changed=()=>{revision++;post({action:'changed',revision});};
// Help is a numeric context on Windows, kept with the entry: the block carries no data-help, so a saved report gets no help link (the
// shared exporter writes one from a help path, as the Mac has). The entry's name is the block's accessible label.
const entryHTML=e=>`<article class="report-entry" id="result-${e.id}" data-result-id="${e.id}" aria-label="${encode(e.title)}">${e.html}</article>`;
const entryOf=id=>entries.find(e=>e.id===id);
// The record of a result's run (its operation, data file, inputs as pointers, settings and results, as RunRecord writes it) travels with
// the entry: appended with it, kept through edits, undo and the saved report, and given back for the R script of the result
const isRecord=r=>!!r&&typeof r==='object'&&!Array.isArray(r);
const order=()=>entries.map(e=>e.id);
function history(){StatsDirectReportEditor.history(undo.length>0,redo.length>0);document.getElementById('empty').hidden=entries.length>0;}
function reveal(id){StatsDirectReportEditor.reveal(id);}
function replace(e){StatsDirectReportEditor.replace(e.id,entryHTML(e));}
function append(e){entries.push(e);StatsDirectReportEditor.replaceEntries([{id:e.id,html:entryHTML(e)}],order());history();reveal(e.id);}
// More of a result into its block (a chart after its analysis): the block's current markup, then the more; the window stays at the result's start
function extend(id,html,record){const e=entryOf(id);if(!e)throw Error('No such result.');const block=document.getElementById('result-'+id);if(block)e.html=StatsDirectReportEditor.serialize(block);e.html+=html;if(isRecord(record))e.record=record;if(!StatsDirectReportEditor.extend(id,html))replace(e);history();reveal(id);}
function discardEntry(id){const i=entries.findIndex(e=>e.id===id);if(i>=0)entries.splice(i,1);StatsDirectReportEditor.replaceEntries([],order());}
function edits(values,typing=false,removeIDs=[]){
  if(!Array.isArray(values)||values.length>entries.length||new Set(values.map(v=>v.id)).size!==values.length)return;
  if(!Array.isArray(removeIDs)||new Set(removeIDs).size!==removeIDs.length||removeIDs.some(id=>!entryOf(id)||values.some(v=>v.id===id)))return;
  const changes=[];
  for(const v of values){const e=entryOf(v.id);if(!e||typeof v.html!=='string'||v.html.length>30_000_000)return;if(e.html!==v.html)changes.push({id:e.id,before:e.html,after:v.html});}
  const deletions=entries.flatMap((entry,index)=>removeIDs.includes(entry.id)?[{entry:structuredClone(entry),index}]:[]);
  if(!changes.length&&!deletions.length)return;
  const last=undo.at(-1),now=Date.now();
  if(typing&&!deletions.length&&last?.typing&&!last.deletions.length&&changes.length===1&&last.changes.length===1&&last.changes[0].id===changes[0].id&&now-last.time<1000&&!redo.length){last.changes[0].after=changes[0].after;last.time=now;}
  else undo.push({changes,deletions,typing,time:now});
  while(undo.length>50||(undo.length>1&&undo.reduce((n,t)=>n+t.changes.reduce((m,c)=>m+c.before.length+c.after.length,0)+t.deletions.reduce((m,d)=>m+d.entry.html.length,0),0)>30_000_000))undo.shift();
  redo.length=0;for(const c of changes)entryOf(c.id).html=c.after;
  for(const d of deletions)discardEntry(d.entry.id);
  changed();history();
}
// The target of a right-click, for the context menu that C# shows: the result under the pointer and its help topic, whether text is
// selected, whether the place is editable, and whether a chart is there (the editor records it as the menu opens)
let contextTarget={};
window.statsDirectReportHost={post(m){
  switch(m.action){
    case 'editing':editing=!!m.value;break;
    case 'editBody':edits([{id:m.resultID,html:m.html}],m.typing);break;
    case 'editBodies':edits(m.edits,false,m.remove??[]);break;
    case 'context':{const result=entryOf(m.resultId);contextTarget={resultId:m.resultId??null,helpContextId:result?.helpContextId??0,operation:result?.operation??'',hasRecord:isRecord(result?.record),hasSelection:!!m.hasSelection,editable:!!m.editable,picture:!!m.picture,pictureWidth:m.pictureWidth??null};break;}
    case 'addText':append({id:crypto.randomUUID(),title:'Text',operation:'',helpContextId:0,html:'<p>Enter your text here.</p>'});StatsDirectReportEditor.setEditing(true);changed();break;
    case 'undoText':case 'redoText':{
      const back=m.action==='undoText',t=(back?undo:redo).pop();if(!t)break;
      (back?redo:undo).push(t);
      const restored=[];
      if(back)for(const d of t.deletions){const at=Math.min(d.index,entries.length),e=structuredClone(d.entry);entries.splice(at,0,e);restored.push({id:e.id,html:entryHTML(e)});}
      else for(const d of t.deletions)discardEntry(d.entry.id);
      for(const c of t.changes){const e=entryOf(c.id);if(e){e.html=back?c.before:c.after;restored.push({id:e.id,html:entryHTML(e)});}}
      StatsDirectReportEditor.replaceEntries(restored,order());
      changed();history();break;
    }
    case 'clipboard':case 'transferNotice':post(m);break;
  }
}};
function render(){document.getElementById('results').replaceChildren();StatsDirectReportEditor.replaceEntries(entries.map(e=>({id:e.id,html:entryHTML(e)})),order());history();}
function snapshot(){for(const e of entries){const block=document.getElementById('result-'+e.id);if(block)e.html=StatsDirectReportEditor.serialize(block);}return {version:1,entries:structuredClone(entries),revision};}
// The result the selection or focus is in, for F1 and the ribbon's Help: told to C# as it changes
const resultAt=node=>{const el=node?.nodeType===1?node:node?.parentElement;return el?.closest?.('.report-entry')?.dataset.resultId??null;};
function currentHelpContext(){const sel=getSelection();const id=resultAt(sel?.rangeCount?sel.anchorNode:null)??resultAt(document.activeElement);return entryOf(id)?.helpContextId??0;}
let toldContext=null;
function tellContext(){const context=currentHelpContext();if(context===toldContext)return;toldContext=context;post({action:'context',context});}
document.addEventListener('selectionchange',tellContext);
document.addEventListener('focusin',tellContext);
window.WindowsReport={async run(request){
  try {let value;
    switch(request.method){
      case 'append':{if(!isRecord(request.args.record))delete request.args.record;append(request.args);changed();value=true;break;}
      case 'extend':extend(request.args.id,request.args.html,request.args.record);changed();value=true;break;
      case 'record':{const e=entryOf(request.args.id);if(!e)throw Error('No such result.');value={title:e.title,operation:e.operation,record:isRecord(e.record)?e.record:null};break;}
      case 'snapshot':value=snapshot();break;
      case 'contextTarget':value=contextTarget;break;
      case 'selectResult':StatsDirectReportEditor.selectResult(request.args.id);value=true;break;
      case 'selectAll':StatsDirectReportEditor.selectAll();value=true;break;
      case 'preview':StatsDirectReportEditor.setEditing(false);document.body.classList.add('chart-preview');value=true;break;
      case 'command':StatsDirectReportEditor.command(request.args.name,request.args.value);value=true;break;
      case 'load':{
        const incoming=request.args.entries??[{id:crypto.randomUUID(),title:request.args.title,operation:'',helpContextId:0,html:request.args.html}];
        if(!Array.isArray(incoming)||incoming.length>1000)throw Error('Invalid report entries.');
        const clean=[];
        // No imported markup enters the editor until EVERY section is sanitized.
        for(const e of incoming){const converted=JSON.parse(await StatsDirectReportImport.convert({html:e.html}));const entry={id:crypto.randomUUID(),title:String(e.title??'Report'),operation:String(e.operation??''),helpContextId:Number.isSafeInteger(e.helpContextId)?e.helpContextId:0,html:converted.html};if(isRecord(e.record))entry.record=e.record;clean.push(entry);}
        entries.splice(0,entries.length,...clean);undo.length=redo.length=0;render();StatsDirectReportEditor.setEditing(true);revision=0;value=snapshot();break;
      }
      case 'export':{
        const state=snapshot();value=await StatsDirectReportExport.capture({format:request.args.format,title:request.args.title});
        if(request.args.format==='html'){
          // The editable entry metadata must contain the same resolved styles
          // as the visible export. Raw editor classes may depend on stylesheets
          // or ancestor selectors that are unavailable when loading each entry.
          const exported=new DOMParser().parseFromString(value,'text/html');
          const blocks=[...exported.querySelectorAll('.report-entry')];
          if(blocks.length!==state.entries.length)throw Error('The report sections could not be saved.');
          state.entries.forEach((entry,index)=>entry.html=blocks[index].innerHTML);
          const metadata=JSON.stringify(state).replaceAll('<','\\u003c');
          value=value.replace('<head>','<head><meta name="statsdirect-report-format" content="1">').replace('</body>',`<script type="application/json" id="statsdirect-report-data">${metadata}</script></body>`);
        }value={content:value,revision:state.revision};break;
      }
      case 'prepareClipboard':value=StatsDirectReportEditor.prepareClipboard(!!request.args.cut);break;
      case 'officeClipboard':value=await StatsDirectReportExport.clipboard({fragment:request.args.html});break;
      case 'cut':value=StatsDirectReportEditor.cutPrepared(request.args.token);break;
      case 'preparePaste':value=StatsDirectReportEditor.preparePaste();break;
      case 'paste':value=StatsDirectReportEditor.pastePrepared(request.args.token,request.args.payload);break;
      default:throw Error('Unknown report command.');
    }
    post({action:'reply',id:request.id,value:value??null});
  }catch(error){post({action:'reply',id:request.id,error:String(error.message??error)});}
}};
