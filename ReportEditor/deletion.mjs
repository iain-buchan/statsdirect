// Windows selection/deletion adapter. Reuse the shared transfer implementation
// so Delete and Cut preserve partially selected tables and treat SVG as a unit.
import {segments,deletePiece,currentRange,selectRange} from './upstream/Report/transfer.mjs';

const element=node=>node?.nodeType===1?node:node?.parentElement;
const bodies=()=>[...document.querySelectorAll('.report-body[data-result-id]')];
const field=target=>element(target)?.closest('input,textarea,select');
function covers(range,body) {
  const all=document.createRange();all.selectNodeContents(body);
  return range.compareBoundaryPoints(Range.START_TO_START,all)<=0&&range.compareBoundaryPoints(Range.END_TO_END,all)>=0;
}
function adjacentMedia(range,backward,body) {
  let node=range.startContainer,offset=range.startOffset;
  if(node.nodeType===Node.TEXT_NODE) {
    if(offset!==(backward?0:node.length))return null;
  } else {
    const child=node.childNodes[backward?offset-1:offset];
    if(child)return element(child)?.matches('.report-media')?child:null;
  }
  while(node!==body) {
    let next=backward?node.previousSibling:node.nextSibling;
    while(next?.nodeType===Node.TEXT_NODE&&!next.textContent.trim())next=backward?next.previousSibling:next.nextSibling;
    if(next)return next.nodeType===1&&next.matches('.report-media')?next:null;
    node=node.parentNode;
  }
  return null;
}

export function installDeletion({isEditing,serialize,post,remember}) {
  let anchor=null;
  function selectResults(first,last=first) {
    const list=bodies(),b=list.findIndex(b=>b.dataset.resultId===last);
    if(b<0)return;
    const found=list.findIndex(b=>b.dataset.resultId===first),a=found<0?b:found;
    const start=list[Math.min(a,b)],end=list[Math.max(a,b)],range=document.createRange();
    range.setStart(start,0);range.setEnd(end,end.childNodes.length);
    selectRange(start,range);highlight();
  }
  function highlight() {
    const range=currentRange();
    for(const body of bodies())body.closest('.report-entry').classList.toggle('report-result-selected',isEditing()&&!!range&&!range.collapsed&&covers(range,body));
  }
  document.addEventListener('selectionchange',highlight);
  document.addEventListener('click',event=>{
    const header=element(event.target)?.closest('[data-select-result]');
    if(!isEditing()||!header||element(event.target)?.closest('button,a,input'))return;
    event.preventDefault();const id=header.dataset.selectResult;
    if(!event.shiftKey||!anchor)anchor=id;
    selectResults(anchor,id);
  });

  function remove(event,backward) {
    const target=element(event.target),header=target?.closest('[data-select-result]');
    let range=currentRange();
    const focusedMedia=target?.matches('.report-media')?target:null;
    if(header&&!target.closest('button')) {
      const body=header.parentElement.querySelector('.report-body');
      // Empty results still have a selectable header and can be deleted.
      range=document.createRange();range.selectNodeContents(body);
      return commit([{body,range}],new Set([body.dataset.resultId]));
    }
    if(focusedMedia) {range=document.createRange();range.selectNode(focusedMedia);}
    if(!range)return false;
    if(range.collapsed) {
      const body=element(range.startContainer)?.closest('.report-body');if(!body)return false;
      const media=adjacentMedia(range,backward,body);if(!media)return false;
      range=document.createRange();range.selectNode(media);
    }
    const pieces=segments(range);if(!pieces.length)return false;
    return commit(pieces,new Set(pieces.filter(p=>covers(range,p.body)).map(p=>p.body.dataset.resultId)));
  }
  function commit(pieces,removeIDs) {
    const first=pieces[0],all=bodies(),index=all.indexOf(first.body);
    const caret=first.range.cloneRange();caret.collapse(true);
    const edits=[];
    // Work on partial sections only. The host removes whole sections and
    // records their metadata/order in the SAME transaction as these edits.
    for(const piece of [...pieces].reverse())if(!removeIDs.has(piece.body.dataset.resultId)) {
      deletePiece(piece);
      if(!piece.body.childNodes.length)piece.body.innerHTML='<p><br></p>';
      edits.push({id:piece.body.dataset.resultId,html:serialize(piece.body)});
    }
    post({action:'deleteSelection',edits,removeIDs:[...removeIDs]});
    if(first.body.isConnected)selectRange(first.body,caret);
    else {
      getSelection().removeAllRanges();
      const next=bodies()[Math.min(index,bodies().length-1)];
      (next?.closest('.report-entry').querySelector('header')??document.getElementById('results')).focus({preventScroll:true});
    }
    remember();highlight();return true;
  }
  function inReport(target) {return !!element(target)?.closest('#results,#report-toolbar');}
  const cancel=event=>{event.preventDefault();event.stopImmediatePropagation();};
  document.addEventListener('keydown',event=>{
    if(!isEditing()||event.isComposing||field(event.target)||!inReport(event.target))return;
    const key=event.key.toLowerCase(),modifier=event.ctrlKey||event.metaKey;
    if(modifier&&(key==='z'||key==='y')) {
      cancel(event);post({action:key==='y'||event.shiftKey?'redoText':'undoText'});return;
    }
    if(modifier&&key==='a'&&element(event.target)?.closest('#results')) {
      cancel(event);const list=bodies();if(list.length)selectResults(list[0].dataset.resultId,list.at(-1).dataset.resultId);return;
    }
    if(key==='escape') {
      const range=currentRange();if(range&&!range.collapsed){cancel(event);range.collapse(true);const body=element(range.startContainer)?.closest('.report-body');if(body)selectRange(body,range);else getSelection().removeAllRanges();highlight();}return;
    }
    if((key==='delete'||key==='backspace')&&!element(event.target)?.closest('button,a')&&remove(event,key==='backspace'))cancel(event);
  },true);
  // Also support browser editing commands/accessibility input that arrive
  // without a key event. Ordinary collapsed text deletion remains native.
  document.addEventListener('beforeinput',event=>{
    if(isEditing()&&!event.isComposing&&!field(event.target)&&inReport(event.target)&&['deleteContentBackward','deleteContentForward'].includes(event.inputType)&&remove(event,event.inputType==='deleteContentBackward'))cancel(event);
  },true);
}
