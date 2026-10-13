// Narrow port of statsdirect-mac 0a112bae's conversation selection handling.
// Keep the recorded lesson/source baseline intact; fail if it drifts.
export function selectionPort(source) {
  source=source.replace(/\r\n?/g,'\n');
  const once = (before, after) => {
    if (source.split(before).length !== 2) throw Error('Review the Learning selection port: ' + before);
    source = source.replace(before, after);
  };
  once("import {richText,bindRichText} from './rich-text.mjs';", "import {richText,bindRichText} from './rich-text.mjs';\nimport {conversationReading,restoreConversationReading} from '../../conversation-view.mjs';");
  once("let streamedText='',streamTimer=null;", "let streamedText='',streamTimer=null,renderedStudy=null;");
  once("function renderStudy(){const l=lesson();if(ensureLessonPrompt(state,allLessons()))save();", "function renderStudy(){const l=lesson();if(ensureLessonPrompt(state,allLessons()))save();\n  const key=state.id+':'+l.id,reading=renderedStudy===key?conversationReading($('messages')):null;renderedStudy=key;");
  once('<details class="earlier-guide">', '<details class="earlier-guide" data-message-id="${esc(c.id)}">');
  once('<article class="message ${c.role', '<article data-message-id="${esc(c.id)}" class="message ${c.role');
  once('if(pending)renderStream();', 'if(pending)renderStream(false);');
  once("renderWorkspace();\n  $('messages').scrollTop=$('messages').scrollHeight;", "renderWorkspace();\n  restoreConversationReading($('messages'),reading);");
  once('function renderStream(){', 'function renderStream(preserve=true){');
  once("const atBottom=$('messages').scrollHeight-$('messages').scrollTop-$('messages').clientHeight<80;", "const reading=preserve?conversationReading($('messages')):null;");
  once("article.id='streamedReply';", "article.id='streamedReply';article.dataset.messageId=pending;");
  once("bindConversation();if(atBottom)$('messages').scrollTop=$('messages').scrollHeight;", "bindConversation();if(preserve)restoreConversationReading($('messages'),reading);");
  once('reply(value){if(value.id!==pending)return;pending=null;', 'reply(value){if(value.id!==pending)return;const messageId=pending;pending=null;');
  once("transcriptEntry(state,'assistant',value.text,'StatsDirect AI tutor',{lessonId:", "transcriptEntry(state,'assistant',value.text,'StatsDirect AI tutor',{id:messageId,lessonId:");
  return source;
}
