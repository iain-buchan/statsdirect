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
     if(question.startsWith('WORKSHEET_AI_') || question==='Add an Age band column') {
       const call=(tool,args)=>new Promise(resolve=>{
         const rid='tool-'+(++sequence);
         responses.set(rid,resolve);
         out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool,arguments:args}});
       });
       const help=await call('statsdirect_search_help',{query:'worksheet'});
       const scope=await call('statsdirect_workspace',{});
       const documents=scope.success?JSON.parse(scope.contentItems[0].text).documents:[];
       const read=await call('statsdirect_read_data',{documentId:documents[0]?.documentId??'unapproved',firstRow:2,lastRow:3,columns:[0]});
       const demo=question==='Add an Age band column';
       const proposal=await call('statsdirect_propose_worksheet_edit',demo
         ? {range:'B1:B4',values:[['Age band'],['18–39'],['40–64'],['65+']],description:'Add age bands in column B'}
         : {range:'B1:B3',values:[['Age band'],['=2+2'],[null]],description:'Review the proposed column values'});
       answer=demo?'The proposed Age band column groups the three example ages as 18–39, 40–64 and 65+. Select **Review worksheet edit** to check the target cells and values before applying.':JSON.stringify({
         help:help.success?help.contentItems[0].text:'',
         ids:documents.map(d=>d.documentId),
         read:read.success,
         proposed:proposal.success,
         toolNames:threadTools.get(p.threadId).map(t=>t.name)
       });
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
       const invalid=await call('statsdirect_propose_worksheet_analysis',{operation:'TPaired',columns:[0,1]});
       const proposal=question==='TRE_WORKSHEET_ANALYSIS_FOLLOWUP'
         ? null : await call('statsdirect_propose_worksheet_analysis',{operation:'TPaired'});
       const pending=proposal?.success?JSON.parse(proposal.contentItems[0].text):null;
       answer=analysisDemo
         ? 'A **paired t test** compares the mean difference between matched observations. Go to **Review analysis** to see the proposed method, then choose the variables and run it in StatsDirect. Your worksheet and result stay on this computer.'
         : JSON.stringify({
           tools,help:help.success?help.contentItems[0].text:'',
           workspace:scope.success,read:read.success,calculation:calculation.success,invalid:invalid.success,
           pending
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
