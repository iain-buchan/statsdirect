/* Windows host for the pinned Mac editor. Document state and multi-section undo
 * stay together; C# owns files, clipboard, printing, window lifetime and help. */
const entries=[],undo=[],redo=[];
let revision=0,editing=true;
const encode=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const post=m=>chrome.webview.postMessage(m);
const changed=()=>{revision++;post({action:'changed',revision});};
const entryHTML=e=>`<article class="report-entry" id="result-${e.id}"><header class="report-controls" data-select-result="${e.id}" tabindex="0" aria-label="Select result: ${encode(e.title)}" title="Select this result; Delete removes it when editing"><h2>${encode(e.title)}</h2><button data-help="${e.helpContextId}">Help</button></header><div class="report-body" data-result-id="${e.id}" role="textbox" aria-label="${encode(e.title)}">${e.html}</div></article>`;
function history(){StatsDirectReportEditor.history(undo.length>0,redo.length>0);document.getElementById('empty').hidden=entries.length>0;}
function append(e){entries.push(e);document.getElementById('results').insertAdjacentHTML('beforeend',entryHTML(e));StatsDirectReportEditor.replace(e.id,entryHTML(e));history();reveal(e.id);}
// The new result is what the user wants to see next: the window scrolls so that its top is just below the sticky toolbar. A result at
// the end of the page can reach the top only with room below it, so the page is given that room (a variable the stylesheet applies
// to the results; the saved report and the printed page do not carry it).
function reveal(id){const node=document.getElementById('result-'+id);if(!node)return;const bar=document.querySelector('.report-toolbar');const gap=(bar?bar.getBoundingClientRect().height:0)+8;const room=Math.max(0,window.innerHeight-gap-node.getBoundingClientRect().height-16);document.documentElement.style.setProperty('--reveal-room',room+'px');window.scrollTo({top:Math.max(0,node.getBoundingClientRect().top+window.scrollY-gap)});}
function replace(e){StatsDirectReportEditor.replace(e.id,entryHTML(e));}
// More of a result into an item (a chart after its analysis): the item's current markup, then the more, and the window stays at the item's start
function extend(id,html,record){const e=entries.find(v=>v.id===id);if(!e)throw Error('No such result.');const body=document.querySelector('#result-'+id+' .report-body');if(body)e.html=StatsDirectReportEditor.serialize(body);e.html+=html;if(isRecord(record))e.record=record;replace(e);history();reveal(id);}
// The record of a result's run (its operation, data file, inputs as pointers, settings and results, as RunRecord writes it) travels with
// the entry: appended with it, kept through edits, undo and the saved report, and given back for the R script of the result
const isRecord=r=>!!r&&typeof r==='object'&&!Array.isArray(r);
function discardEntry(id){const node=document.getElementById('result-'+id);if(node){StatsDirectReportEditor.detach(node);node.remove();}const i=entries.findIndex(e=>e.id===id);if(i>=0)entries.splice(i,1);}
function edits(values,typing=false,removeIDs=[]){
  if(!Array.isArray(values)||values.length>entries.length||new Set(values.map(v=>v.id)).size!==values.length)return;
  if(!Array.isArray(removeIDs)||new Set(removeIDs).size!==removeIDs.length||removeIDs.some(id=>!entries.some(e=>e.id===id)||values.some(v=>v.id===id)))return;
  const changes=[];
  for(const v of values){const e=entries.find(x=>x.id===v.id);if(!e||typeof v.html!=='string'||v.html.length>30_000_000)return;if(e.html!==v.html)changes.push({id:e.id,before:e.html,after:v.html});}
  const deletions=entries.flatMap((entry,index)=>removeIDs.includes(entry.id)?[{entry:structuredClone(entry),index}]:[]);
  if(!changes.length&&!deletions.length)return;
  const last=undo.at(-1),now=Date.now();
  if(typing&&!deletions.length&&last?.typing&&!last.deletions.length&&changes.length===1&&last.changes.length===1&&last.changes[0].id===changes[0].id&&now-last.time<1000&&!redo.length){last.changes[0].after=changes[0].after;last.time=now;}
  else undo.push({changes,deletions,typing,time:now});
  while(undo.length>50||(undo.length>1&&undo.reduce((n,t)=>n+t.changes.reduce((m,c)=>m+c.before.length+c.after.length,0)+t.deletions.reduce((m,d)=>m+d.entry.html.length,0),0)>30_000_000))undo.shift();
  redo.length=0;for(const c of changes)entries.find(e=>e.id===c.id).html=c.after;
  for(const d of deletions)discardEntry(d.entry.id);
  changed();history();
}
window.statsDirectReportHost={post(m){
  switch(m.action){
    case 'editing':editing=!!m.value;break;
    case 'editBody':edits([{id:m.resultID,html:m.html}],m.typing);break;
    case 'editBodies':edits(m.edits);break;
    case 'deleteSelection':edits(m.edits,false,m.removeIDs);break;
    case 'addText':append({id:crypto.randomUUID(),title:'Text',operation:'',helpContextId:0,html:'<p>Enter your text here.</p>'});StatsDirectReportEditor.setEditing(true);changed();break;
    case 'undoText':case 'redoText':{
      const back=m.action==='undoText',t=(back?undo:redo).pop();if(!t)break;
      (back?redo:undo).push(t);
      if(back)for(const d of t.deletions){
        const at=Math.min(d.index,entries.length),e=structuredClone(d.entry),next=entries[at];entries.splice(at,0,e);
        const template=document.createElement('template');template.innerHTML=entryHTML(e);
        document.getElementById('results').insertBefore(template.content, next?document.getElementById('result-'+next.id):null);replace(e);
      }
      else for(const d of t.deletions)discardEntry(d.entry.id);
      for(const c of t.changes){const e=entries.find(e=>e.id===c.id);if(e){e.html=back?c.before:c.after;replace(e);}}
      if(t.deletions.length){const id=back?t.deletions[0].entry.id:entries[Math.min(t.deletions[0].index,entries.length-1)]?.id;(document.getElementById('result-'+id)?.querySelector('header')??document.getElementById('results')).focus({preventScroll:true});}
      changed();history();break;
    }
    case 'clipboard':case 'transferNotice':post(m);break;
  }
}};
function render(){StatsDirectReportEditor.detach(document.getElementById('results'));document.getElementById('results').innerHTML=entries.map(entryHTML).join('');for(const e of entries)replace(e);history();}
function snapshot(){for(const e of entries){const body=document.querySelector(`#result-${e.id} .report-body`);if(body)e.html=StatsDirectReportEditor.serialize(body);}return {version:1,entries:structuredClone(entries),revision};}
document.addEventListener('click',e=>{const h=e.target.closest('[data-help]');if(h)post({action:'help',context:Number(h.dataset.help)});});
// The target of a right-click, for the context menu that C# shows: the result under the pointer and its help topic, whether text is selected, and whether the place is editable
let contextTarget={};
let contextChart=null;   // the chart under the pointer at the last right-click: the outermost svg, or a raster picture
document.addEventListener('contextmenu',e=>{const entry=e.target.closest?.('.report-entry'),id=entry?entry.id.slice(7):null,sel=getSelection(),result=id?entries.find(v=>v.id===id):null;let chart=e.target.closest?.('svg,img')??null;while(chart&&chart.parentElement?.closest('svg'))chart=chart.parentElement.closest('svg');contextChart=chart;contextTarget={resultId:id,helpContextId:result?.helpContextId??0,operation:result?.operation??'',hasRecord:isRecord(result?.record),chart:!!chart,chartKind:chart?(chart.tagName.toLowerCase()==='img'?'png':'svg'):null,hasSelection:!!sel&&sel.rangeCount>0&&!sel.isCollapsed,editable:editing&&!!e.target.closest?.('.report-body')};});
// The chart under the pointer as a file: its own markup for an SVG file (the xmlns ensured), or a PNG drawn from it on a canvas at the
// given scale of its size on the page (a raster picture gives its own data as it is)
async function chartImage(format,scale){
  const el=contextChart;if(!el||!el.isConnected)throw Error('No chart is under the pointer.');
  if(el.tagName.toLowerCase()==='img'){if(format==='svg')throw Error('This picture is not a vector chart.');return el.src;}
  const svg=el.cloneNode(true);if(!svg.getAttribute('xmlns'))svg.setAttribute('xmlns','http://www.w3.org/2000/svg');
  if(format==='svg')return new XMLSerializer().serializeToString(svg);
  const box=el.getBoundingClientRect(),w=Math.max(1,Math.round(box.width)),h=Math.max(1,Math.round(box.height));
  svg.setAttribute('width',w);svg.setAttribute('height',h);
  const img=new Image();
  await new Promise((ok,bad)=>{img.onload=ok;img.onerror=()=>bad(Error('The chart could not be drawn.'));img.src='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(new XMLSerializer().serializeToString(svg));});
  const k=Math.max(1,Number(scale)||1),canvas=document.createElement('canvas');canvas.width=w*k;canvas.height=h*k;
  const ctx=canvas.getContext('2d');ctx.fillStyle='white';ctx.fillRect(0,0,canvas.width,canvas.height);ctx.scale(k,k);ctx.drawImage(img,0,0,w,h);
  return canvas.toDataURL('image/png');
}
// A result selected as a click on its heading selects it; all of them as Ctrl+A does. The result's body takes the focus first, so that
// a key that follows (Delete, Ctrl+C) reaches the editor's handling of the results
function selectResult(id){document.querySelector('#result-'+id+' .report-body')?.focus();document.querySelector('#result-'+id+' [data-select-result]')?.click();}
function selectAll(){document.querySelector('.report-body')?.focus();document.getElementById('results').dispatchEvent(new KeyboardEvent('keydown',{key:'a',ctrlKey:true,bubbles:true,cancelable:true}));}
// A selection that spills outside the results (Ctrl+A on the page takes the toolbar too) is one the editor would leave to the browser,
// whose copy gives Word raw markup; it becomes the selection of all results
function clampSelection(){const sel=getSelection();if(!sel||!sel.rangeCount||sel.isCollapsed)return;const results=document.getElementById('results'),r=sel.getRangeAt(0);if(results.contains(r.startContainer)&&results.contains(r.endContainer))return;if(!r.intersectsNode(results))return;selectAll();}
const field=t=>!!(t?.closest?.('input,textarea,select,[contenteditable="true"]:not(.report-body)'));
document.addEventListener('keydown',e=>{if(!editing||e.isComposing||!(e.ctrlKey||e.metaKey)||e.key.toLowerCase()!=='a')return;const t=e.target instanceof Element?e.target:null;if(field(t)||t?.closest('#results'))return;e.preventDefault();e.stopImmediatePropagation();selectAll();},true);
for(const kind of ['copy','cut'])document.addEventListener(kind,()=>clampSelection(),true);
document.addEventListener('focusin',e=>{const body=e.target.closest('.report-body');if(body){const entry=entries.find(v=>v.id===body.dataset.resultId);post({action:'context',context:entry?.helpContextId??0});}});
window.WindowsReport={async run(request){
  try {let value;
    switch(request.method){
      case 'append':{if(!isRecord(request.args.record))delete request.args.record;append(request.args);changed();value=true;break;}
      case 'extend':extend(request.args.id,request.args.html,request.args.record);changed();value=true;break;
      case 'snapshot':value=snapshot();break;
      case 'contextTarget':value=contextTarget;break;
      case 'record':{const e=entries.find(v=>v.id===request.args.id);if(!e)throw Error('No such result.');value={title:e.title,operation:e.operation,record:isRecord(e.record)?e.record:null};break;}
      case 'chartImage':value=await chartImage(request.args.format,request.args.scale);break;
      case 'selectResult':selectResult(request.args.id);value=true;break;
      case 'selectAll':selectAll();value=true;break;
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
          const bodies=[...exported.querySelectorAll('.report-entry > .report-body')];
          if(bodies.length!==state.entries.length)throw Error('The report sections could not be saved.');
          state.entries.forEach((entry,index)=>entry.html=bodies[index].innerHTML);
          const metadata=JSON.stringify(state).replaceAll('<','\\u003c');
          value=value.replace('<head>','<head><meta name="statsdirect-report-format" content="1">').replace('</body>',`<script type="application/json" id="statsdirect-report-data">${metadata}</script></body>`);
        }value={content:value,revision:state.revision};break;
      }
      case 'prepareClipboard':clampSelection();value=StatsDirectReportEditor.prepareClipboard(!!request.args.cut);break;
      case 'officeClipboard':value=await StatsDirectReportExport.clipboard({fragment:request.args.html});break;
      case 'cut':value=StatsDirectReportEditor.cutPrepared(request.args.token);break;
      case 'preparePaste':value=StatsDirectReportEditor.preparePaste();break;
      case 'paste':value=StatsDirectReportEditor.pastePrepared(request.args.token,request.args.payload);break;
      default:throw Error('Unknown report command.');
    }
    post({action:'reply',id:request.id,value:value??null});
  }catch(error){post({action:'reply',id:request.id,error:String(error.message??error)});}
}};
