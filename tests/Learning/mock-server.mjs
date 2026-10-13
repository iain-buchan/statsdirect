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
   const timer=setTimeout(async()=>{
     if(text.includes('FORCE_ERROR')){out({method:'turn/completed',params:{threadId:p.threadId,turn:{id,status:'failed',error:{message:'401 SECRET_SHOULD_NOT_BE_SHOWN'}}}});return;}
     let answer='Mock answer: paired observations need paired analysis.\n```r\nx <- c(1,2,3)\nmean(x)\n```';
     const question=JSON.parse(text.slice(text.indexOf('\n')+1)).conversation.at(-1).text;
     if(question==='PEFR_CONTEXT') {
       const sample=readFileSync(new URL('ai-pefr.R',import.meta.url),'utf8');
       answer='Use the shared paired data.\n```r\n'+sample.split('\n')[0]+'\n```\nThen analyse and plot:\n```r\n'+sample.split('\n').slice(1).join('\n')+'\n```';
     }
     if(question==='PEFR_FOLLOWUP') answer='Continue with the same differences.\n```r\nstopifnot(length(d) == 9)\nprint(mean(d))\n```';
     if(text.includes('TOOL_BOUNDARIES')){
       const call=extra=>new Promise(resolve=>{const rid='tool-'+(++sequence);responses.set(rid,resolve);out({id:rid,method:'item/tool/call',params:{threadId:p.threadId,turnId:id,callId:'call-'+sequence,tool:'statsdirect_workspace',arguments:{},...extra}});});
       const wrong=await call({threadId:'not-approved'}),namespace=await call({namespace:'unapproved'}),unknown=await call({tool:'shell'}),valid=await call({callId:'once'}),duplicate=await call({callId:'once'});
       answer=JSON.stringify({wrong:wrong.success,namespace:namespace.success,unknown:unknown.success,valid:valid.success,duplicate:duplicate.success});
     }
     out({method:'item/agentMessage/delta',params:{threadId:p.threadId,turnId:id,itemId:'reply',delta:answer}});
     out({method:'item/completed',params:{threadId:p.threadId,turnId:id,item:{type:'agentMessage',id:'reply',text:answer}}});
     out({method:'turn/completed',params:{threadId:p.threadId,turn:{id,status:'completed'}}});
   },text.includes('COPY_SELECTION_CHECK')?60000:text.includes('SLOW_REPLY')?8000:50);turns.set(id,timer);break;}
 case 'turn/interrupt':clearTimeout(turns.get(p.turnId));result({});break;
 case 'thread/unsubscribe':result({});break;
 default: if(q.id!==undefined)out({id:q.id,error:{code:-32601,message:'Unsupported mock method'}});
 }
});
