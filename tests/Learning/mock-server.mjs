import readline from 'node:readline';
import {readFileSync} from 'node:fs';
let loggedIn=false,sequence=0;const turns=new Map(),threadTools=new Map(),responses=new Map();
const out=v=>process.stdout.write(JSON.stringify(v)+'\n');
readline.createInterface({input:process.stdin}).on('line',line=>{
 const q=JSON.parse(line); if(!q.method){const f=responses.get(q.id);if(f){responses.delete(q.id);f(q.result);}return;}
 const p=q.params??{};const result=v=>out({id:q.id,result:v});
 switch(q.method){
 case 'initialize':result({});break;
 case 'account/read':result({account:loggedIn?{type:'chatgpt',email:'mock@example.invalid',planType:'test'}:null});break;
 case 'account/login/start':loggedIn=true;result({loginId:'mock-login',authUrl:'https://auth.openai.com/mock-test'});setTimeout(()=>out({method:'account/login/completed',params:{loginId:'mock-login',success:true}}),20);break;
 case 'account/login/cancel':result({});break;
 case 'account/logout':loggedIn=false;result({});break;
 case 'thread/start':{const id='thread-'+(++sequence);threadTools.set(id,p.dynamicTools??[]);result({thread:{id},model:'mock-no-network'});break;}
 case 'turn/start':{
   const id='turn-'+(++sequence);result({turn:{id}});const text=p.input[0].text;
   const lastQuestion=JSON.parse(text.slice(text.indexOf('\n')+1)).conversation.at(-1).text.replace(/^\[Lesson context: [^\n]*\]\n/,'');
   const timer=setTimeout(async()=>{
     if(text.includes('FORCE_ERROR')){out({method:'turn/completed',params:{threadId:p.threadId,turn:{id,status:'failed',error:{message:'401 SECRET_SHOULD_NOT_BE_SHOWN'}}}});return;}
     let answer='Mock answer: paired observations need paired analysis.\n```r\nx <- c(1,2,3)\nmean(x)\n```';
     const question=lastQuestion;
     if(question==='PEFR_CONTEXT') {
       const sample=readFileSync(new URL('ai-pefr.R',import.meta.url),'utf8');
       answer='Use the shared paired data.\n```r\n'+sample.split('\n')[0]+'\n```\nThen analyse and plot:\n```r\n'+sample.split('\n').slice(1).join('\n')+'\n```';
     }
     if(question==='PEFR_FOLLOWUP') answer='Continue with the same differences.\n```r\nstopifnot(length(d) == 9)\nprint(mean(d))\n```';
     if(question==='LOCAL_CONTEXT_PRIVACY') answer=text.includes('LOCAL_REPLAY_ONLY')?'PRIVATE_SNAPSHOT_LEAK':'LOCAL_METADATA_EXCLUDED';
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
