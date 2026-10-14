// Port the Learning bridge/replay behaviour from Mac cbccc10 without changing lessons.
export function recoveryPort(source) {
  const once=(before,after)=>{
    if(source.split(before).length!==2)throw Error('Review the Learning recovery port: '+before);
    source=source.replace(before,after);
  };
  once("let streamedText='',streamTimer=null,renderedStudy=null;", "let streamedText='',streamTimer=null,renderedStudy=null,bridgeGeneration='';");
  once("function bindConversation(){bindRichText($('messages'),{", "function bindConversation(){const generation=bridgeGeneration,lessonID=lesson().id;bindRichText($('messages'),{");
  once("openR:code=>{if(native)post({action:'openRCode',code});", "openR:(code,position)=>{if(native)post({action:'openRCode',generation,lessonID,...position});");
  once("transcriptEntry(state,'user',text,'Learner',", "const userMessage=transcriptEntry(state,'user',text,'Learner',");
  once("post({action:'ask',id:pending,lesson:", "post({action:'ask',id:pending,userMessageID:userMessage.id,lesson:");
  once('window.statsDirectLearn={', "window.statsDirectLearn={\n  bridgeGeneration(value){bridgeGeneration=value;},\n  resumeReply(event){if(!loaded||!event?.value?.id)return;pending=event.value.id;streamedText=event.partial??'';if(event.action==='reply')this.reply(event.value);else if(event.action==='tutorError')this.tutorError(event.value);else render();},");
  once('restore(value){try{loaded=false;', "restore(value){try{loaded=false;pending=null;streamedText='';renderedStudy=null;clearTimeout(streamTimer);streamTimer=null;");
  once('const messageId=pending;pending=null;', 'const messageId=pending;pending=null;state.conversation=state.conversation.filter(c=>c.id!==messageId);');
  once("tutorError(value){if(value.id&&value.id!==pending)return;pending=null;state.diagnostics={...state.diagnostics,lastError:value.message,events:value.diagnostics??[]};save();notice(value.message);render();}", "tutorError(value){if(value.id&&value.id!==pending)return;const id=pending;pending=null;if(streamedText&&id){state.conversation=state.conversation.filter(c=>c.id!==id);transcriptEntry(state,'assistant',streamedText+'\\n\\n[Reply stopped before completion.]','StatsDirect AI tutor',{id,lessonId:lesson().id,lessonTitle:lesson().title});}streamedText='';state.diagnostics={...state.diagnostics,requestID:value.id,lastError:'Tutor reply stopped or failed',events:value.diagnostics??[]};save();notice(value.message);render();}");
  return source;
}

export function replayLinks(source) {
  source=source.replace(/\r\n?/g,'\n');
  const before="if(button.dataset.code==='r')openR(code);";
  if(source.split(before).length!==2)throw Error('Review the Learning R-link port');
  return source.replace(before, "if(button.dataset.code==='r'){const message=button.closest('[data-message-id]');openR(code,{messageID:message?.dataset.messageId??'',blockIndex:Array.from(message?.querySelectorAll('[data-code=r]')??[]).indexOf(button)});}");
}
