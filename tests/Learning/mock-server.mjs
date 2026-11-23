import readline from 'node:readline';
import {appendFileSync,readFileSync} from 'node:fs';
import {join} from 'node:path';
let loggedIn=false,sequence=0;const turns=new Map(),threadTools=new Map(),responses=new Map();
const workspaceProbePath=process.argv[2]==='--workspace-policy-probe'?process.argv[3]:null;
const analysisTracePath=process.argv[2]==='--worksheet-analysis-probe'?process.argv[3]:null;
const probe=record=>{if(workspaceProbePath)appendFileSync(workspaceProbePath,JSON.stringify({pid:process.pid,...record})+'\n');};
const mockArguments=process.argv.slice(4);
const workspacePinArgument=mockArguments.find(value=>value.startsWith('forced_chatgpt_workspace_id='));
const workspacePin=workspacePinArgument?JSON.parse(workspacePinArgument.split('=',2)[1]):null;
const wrongWorkspaceUrl=mockArguments.includes('--wrong-workspace-url');
const wrongCachedWorkspace=mockArguments.includes('--cached-account-wrong-workspace');
const missingCachedWorkspace=mockArguments.includes('--cached-account-missing-workspace');
const otherWorkspace='33333333-3333-4333-8333-333333333333';
if(wrongCachedWorkspace||missingCachedWorkspace)loggedIn=true;
if(workspaceProbePath)probe({event:'launch',arguments:mockArguments,config:readFileSync(join(process.env.CODEX_HOME,'config.toml'),'utf8')});
const out=v=>process.stdout.write(JSON.stringify(v)+'\n');
async function readFullHelpForProbe(call) {
  const topic='references/reference_list.htm';
  const firstReply=await call('statsdirect_read_help_topic',{topic});
  const first=firstReply.success?JSON.parse(firstReply.contentItems[0].text):null;
  const continuationReply=Number.isInteger(first?.nextOffset)
    ? await call('statsdirect_read_help_topic',{topic,offset:first.nextOffset}) : null;
  const continuation=continuationReply?.success?JSON.parse(continuationReply.contentItems[0].text):null;
  const invalid=await call('statsdirect_read_help_topic',{topic:'basics/unknown-help-topic.htm'});
  return {
    topic:first?.topic,
    url:first?.url,
    introductoryText:first?.text?.includes('Nonparametric inference for a family of counting processes')??false,
    continued:first?.nextOffset>2400 && continuation?.offset===first.nextOffset &&
      continuation?.topic===topic && continuation.text.length>2400 && continuation.text!==first.text,
    invalidRejected:!invalid.success
  };
}
readline.createInterface({input:process.stdin}).on('line',line=>{
 if(analysisTracePath)appendFileSync(analysisTracePath,line+'\n');
 const q=JSON.parse(line); if(!q.method){probe({event:'response'});const f=responses.get(q.id);if(f){responses.delete(q.id);f(q.result);}return;}
 probe({event:'request',method:q.method});
 const p=q.params??{};const result=v=>out({id:q.id,result:v});
 switch(q.method){
 case 'initialize':result({});break;
 case 'account/read':result({account:loggedIn?{type:'chatgpt',email:'mock@example.invalid',planType:'test'}:null,
   workspaceRouting:loggedIn&&workspacePin&&!missingCachedWorkspace?{chatgptAccountId:wrongCachedWorkspace?otherWorkspace:workspacePin}:undefined});break;
 case 'account/login/start':{
   const url=new URL('https://auth.openai.com/mock-test');
   if(workspacePin)url.searchParams.set('allowed_workspace_id',wrongWorkspaceUrl?otherWorkspace:workspacePin);
   loggedIn=!wrongWorkspaceUrl;result({loginId:'mock-login',authUrl:url.href});
   setTimeout(()=>out({method:'account/login/completed',params:{loginId:'mock-login',success:loggedIn}}),20);break;}
 case 'account/login/cancel':result({});break;
 case 'account/logout':loggedIn=false;result({});break;
 case 'thread/start':{const id='thread-'+(++sequence);threadTools.set(id,p.dynamicTools??[]);result({thread:{id},model:'mock-no-network'});break;}
 case 'turn/start':{
   const id='turn-'+(++sequence);result({turn:{id}});const text=p.input[0].text;
   const request=JSON.parse(text.slice(text.indexOf('\n')+1));
   const lastQuestion=request.conversation.at(-1).text.replace(/^\[Lesson context: [^\n]*\]\n/,'');
   const timer=setTimeout(async()=>{
     if(text.includes('FORCE_ERROR')){out({method:'turn/completed',params:{threadId:p.threadId,turn:{id,status:'failed',error:{message:'401 SECRET_SHOULD_NOT_BE_SHOWN'}}}});return;}
     let answer='Mock answer: paired observations need paired analysis.\n```r\nx <- c(1,2,3)\nmean(x)\n```';
     const question=lastQuestion;
     if(analysisTracePath && question==='yes please') {
       const reference=JSON.parse(request.referenceContext);
       const asked=reference.openingQuestion==='Do you want me to help you analyse your data?';
       const guided=reference.methodSelection?.text?.includes('goal');
       answer=asked&&guided
         ? 'What would you like to find out from your data? For example, are you comparing groups or looking at a relationship?'
         : 'The opening offer or Method Selection guide was missing.';
     }
     if(question==='WORKSPACE_POLICY_TOOL_PROBE'){
       await new Promise(resolve=>{const rid='tool-'+(++sequence);responses.set(rid,resolve);out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool:'statsdirect_method_help',arguments:{}}});});
     }
     if(question==='PEFR_CONTEXT') {
       const sample=readFileSync(new URL('ai-pefr.R',import.meta.url),'utf8');
       answer='Use the shared paired data.\n```r\n'+sample.split('\n')[0]+'\n```\nThen analyse and plot:\n```r\n'+sample.split('\n').slice(1).join('\n')+'\n```';
     }
     if(question==='PEFR_FOLLOWUP') answer='Continue with the same differences.\n```r\nstopifnot(length(d) == 9)\nprint(mean(d))\n```';
     if(question==='LOCAL_CONTEXT_PRIVACY') answer=text.includes('LOCAL_REPLAY_ONLY')?'PRIVATE_SNAPSHOT_LEAK':'LOCAL_METADATA_EXCLUDED';
     if(question.startsWith('TRE_GUIDANCE_PROBE')) {
       const tools=threadTools.get(p.threadId).map(t=>t.name);
       const call=(tool,args)=>new Promise(resolve=>{const rid='tool-'+(++sequence);responses.set(rid,resolve);out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool,arguments:args}});});
       const workspace=await call('statsdirect_workspace',{});
       const documents=workspace.success?JSON.parse(workspace.contentItems[0].text).documents:[];
       const workbook=documents.find(doc=>doc.title==='TRE_PRIVATE_WORKBOOK');
       const read=await call('statsdirect_read_data',{documentId:workbook?.documentId??'unapproved',firstRow:2,lastRow:2,columns:[0]});
       answer=JSON.stringify({request,tools,canReadData:read.success,value:read.success?JSON.parse(read.contentItems[0].text).columns[0].values[0]:null});
     }
     if(question==='ALL_OPEN_WORKBOOKS') {
       const call=(tool,args)=>new Promise(resolve=>{const rid='tool-'+(++sequence);responses.set(rid,r=>resolve(r.success?JSON.parse(r.contentItems[0].text):{error:r.contentItems[0].text}));out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool,arguments:args}});});
       const scope=await call('statsdirect_workspace',{}),books=scope.documents.filter(d=>d.kind==='grid'),reads=[];
       for(const book of books) reads.push(await call('statsdirect_read_data',{documentId:book.documentId,firstRow:2,lastRow:2,columns:[0]}));
       answer=JSON.stringify({scopeIds:books.map(b=>b.documentId),readIds:reads.map(d=>d.documentId),titles:reads.map(d=>d.workbook),values:reads.map(d=>d.columns?.[0]?.values?.[0]),datasetIds:reads.map(d=>d.datasetId)});
     }
     if(question.startsWith('WORKSHEET_AI_') || question==='What is the mean age?') {
       const call=(tool,args)=>new Promise(resolve=>{
         const rid='tool-'+(++sequence);
         responses.set(rid,resolve);
         out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool,arguments:args}});
       });
       const help=await call('statsdirect_search_help',{query:'worksheet'});
       const fullHelp=question==='WORKSHEET_AI_WITHOUT_SHARING'||question==='WORKSHEET_AI_RESTRICTED'
         ? await readFullHelpForProbe(call) : null;
       const scope=await call('statsdirect_workspace',{});
       const documents=scope.success?JSON.parse(scope.contentItems[0].text).documents:[];
       const read=await call('statsdirect_read_data',{documentId:documents[0]?.documentId??'unapproved',firstRow:2,lastRow:4,columns:[0]});
       if((question==='WORKSHEET_AI_R_ANSWER' || question==='WORKSHEET_AI_R_PRIOR_ANSWER' || question==='What is the mean age?') && read.success) {
         const data=JSON.parse(read.contentItems[0].text);
         const values=data.columns[0].values;
         const mean=values.reduce((sum,value)=>sum+value,0)/values.length;
         const age='age <- .statsdirect_data[['+JSON.stringify(data.datasetId)+']]$column_0\n';
         const followsEarlierAnswer=request.conversation.some(message=>message.role==='assistant' && message.text.startsWith('The median age'));
         answer=question==='WORKSHEET_AI_R_ANSWER' && !followsEarlierAnswer
           ? 'The earlier reply is missing from the conversation.'
           : question==='WORKSHEET_AI_R_PRIOR_ANSWER'
           ? 'The median age is the middle of the sorted values.\n```r\n'+age+'earlier_answer_only <- TRUE\nprint(median(age))\n```'
           : 'The mean age is '+mean+' across '+values.length+' worksheet values.\n```r\n'+age+'cat("Mean age: ", mean(age), "\\n", sep = "")\nplot(age, ylab = "Age")\n```';
       } else {
         const edit=question==='WORKSHEET_AI_DEPRECATED_EDIT'
           ? await call('statsdirect_propose_worksheet_edit',{range:'B1:B3',values:[['Age band'],['=2+2'],[null]],description:'Try the old worksheet tool'})
           : null;
         answer=JSON.stringify({
           help:help.success?help.contentItems[0].text:'',
           fullHelp,
           ids:documents.map(d=>d.documentId),
           read:read.success,
           editAttempted:edit!==null,
           editAccepted:edit?.success??false,
           toolNames:threadTools.get(p.threadId).map(t=>t.name)
         });
       }
     }
     const analysisDemo=question==='Compare my two paired measurements using a paired t test';
     if(question==='TRE_WORKSHEET_ANALYSIS_R_EXAMPLE') {
       answer='This fictional example illustrates the method.\n```r\nt.test(c(6, 9, 12), c(5, 7, 8), paired = TRUE)\n```';
     } else if(question.startsWith('TRE_WORKSHEET_ANALYSIS_') || analysisDemo) {
       const call=(tool,args)=>new Promise(resolve=>{
         const rid='tool-'+(++sequence);
         responses.set(rid,resolve);
         out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool,arguments:args}});
       });
       const tools=threadTools.get(p.threadId).map(t=>t.name);
       const help=await call('statsdirect_search_help',{query:'paired t test'});
       const scope=await call('statsdirect_workspace',{});
       const read=await call('statsdirect_read_data',{documentId:'unapproved',firstRow:2,lastRow:6,columns:[0,1]});
       const calculation=await call('statsdirect_run_analysis',{operation:'TPaired',datasetId:'unapproved'});
       const mannWhitney=question==='TRE_WORKSHEET_ANALYSIS_MANN_WHITNEY';
       const operation=mannWhitney?'MannWhitney':'TPaired';
       const found=await call('statsdirect_find_worksheet_methods',{query:mannWhitney?'Mann–Whitney':'Paired t'});
       const matches=found.success ? JSON.parse(found.contentItems[0].text).methods : [];
       const methodId=matches.find(method=>method.operation===operation)?.id;
       const broad=await call('statsdirect_find_worksheet_methods',{query:'Analysis'});
       const broadMatches=broad.success?JSON.parse(broad.contentItems[0].text).methods.length:0;
       const invalid=await call('statsdirect_propose_worksheet_analysis',{methodId,columns:[0,1]});
       const unknownMethod=await call('statsdirect_propose_worksheet_analysis',{methodId:'not-a-picker-command'});
       const proposal=question==='TRE_WORKSHEET_ANALYSIS_FOLLOWUP' ? null
         : question==='TRE_WORKSHEET_ANALYSIS_DESIGN' ? await call('statsdirect_choose_worksheet_t_test',{})
         : methodId ? await call('statsdirect_propose_worksheet_analysis',{methodId}) : null;
       const pending=proposal?.success?JSON.parse(proposal.contentItems[0].text):null;
       answer=analysisDemo
         ? 'A **paired t test** compares the mean difference between matched observations. Check the method and selection in the conversation, then choose **Continue to analysis**. Your worksheet and result stay on this computer.'
         : JSON.stringify({
           tools,help:help.success?help.contentItems[0].text:'',
           workspace:scope.success,read:read.success,calculation:calculation.success,invalid:invalid.success,
           unknownMethod:unknownMethod.success,matches,broadMatches,pending
         });
     }
     if(question==='LEARN_WORKSHEET') {
       const call=(tool,args)=>new Promise(resolve=>{const rid='tool-'+(++sequence);responses.set(rid,r=>resolve(r.success?JSON.parse(r.contentItems[0].text):{error:r.contentItems[0].text}));out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool,arguments:args}});});
       const scope=await call('statsdirect_workspace',{});
       const data=await call('statsdirect_read_data',{documentId:scope.documents[0].documentId,firstRow:2,lastRow:5,columns:[0,1]});
       answer=data.rCode?'Worksheet literal snapshot.\n```r\nstopifnot(length(.statsdirect_data[['+JSON.stringify(data.datasetId)+']][["column_0"]]) == 4)\nprint(.statsdirect_data[['+JSON.stringify(data.datasetId)+']])\n```':'MISSING_WORKSHEET_R_BINDINGS';
     }
     if(text.includes('TOOL_BOUNDARIES')){
       const call=extra=>new Promise(resolve=>{const rid='tool-'+(++sequence);responses.set(rid,resolve);out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool:'statsdirect_workspace',arguments:{},...extra}});});
       const wrong=await call({threadId:'not-approved'}),namespace=await call({namespace:'unapproved'}),unknown=await call({tool:'shell'}),valid=await call({callId:'once'}),duplicate=await call({callId:'once'});
       answer=JSON.stringify({wrong:wrong.success,namespace:namespace.success,unknown:unknown.success,valid:valid.success,duplicate:duplicate.success});
     }
     out({method:'item/agentMessage/delta',params:{threadId:p.threadId,turnId:id,itemId:'reply',delta:answer}});
     out({method:'item/completed',params:{threadId:p.threadId,turnId:id,item:{type:'agentMessage',id:'reply',text:answer}}});
     out({method:'turn/completed',params:{threadId:p.threadId,turn:{id,status:'completed'}}});
   },lastQuestion.includes('COPY_SELECTION_CHECK')?60000:lastQuestion.includes('SLOW_REPLY')?8000:50);turns.set(id,timer);break;}
 case 'turn/interrupt':clearTimeout(turns.get(p.turnId));result({});break;
 case 'thread/unsubscribe':result({});break;
 default: if(q.id!==undefined)out({id:q.id,error:{code:-32601,message:'Unsupported mock method'}});
 }
});
