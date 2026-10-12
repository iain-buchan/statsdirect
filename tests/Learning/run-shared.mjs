// Run the unchanged Mac learning tests against Windows' shipped lessons, help and
// operation definitions. Only the hard-coded R executable is adapted in temporary copies.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath, pathToFileURL} from 'node:url';
import {spawnSync} from 'node:child_process';
const repo=fileURLToPath(new URL('../../',import.meta.url));
const r=process.env.STATSDIRECT_RSCRIPT;
if(!r||!fs.existsSync(r))throw Error('Set STATSDIRECT_RSCRIPT to the installed Windows Rscript.exe.');
const scratch=fs.mkdtempSync(path.join(os.tmpdir(),'StatsDirect-shared-learning-'));
try{
  fs.cpSync(path.join(repo,'Learning/upstream/Learn'),path.join(scratch,'Learn'),{recursive:true});
  fs.cpSync(path.join(repo,'Learning/upstream/Docs'),path.join(scratch,'Docs'),{recursive:true});
  fs.mkdirSync(path.join(scratch,'Content/Learn'),{recursive:true});
  for(const f of ['lessons.json','provider-course-example.json'])fs.copyFileSync(path.join(repo,'StatsDirectUI/Assets/Learn',f),path.join(scratch,'Content/Learn',f));
  const {questions}=await import(pathToFileURL(path.join(scratch,'Learn/bank.mjs')));
  const lessons=JSON.parse(fs.readFileSync(path.join(scratch,'Content/Learn/lessons.json'),'utf8'));
  for(const topic of new Set([...questions,...lessons].map(x=>x.help))){
    const source=path.resolve(repo,'StatsDirectUI/Assets/Help',topic);
    if(!source.startsWith(path.join(repo,'StatsDirectUI/Assets/Help')+path.sep))throw Error('Unexpected help path.');
    const target=path.join(scratch,'Content/Help',topic);fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(source,target);
  }
  const operations={};
  for(const f of fs.readdirSync(path.join(repo,'StatsDirectUI/Assets/Operations')).filter(f=>f.endsWith('.xml'))){
    const text=fs.readFileSync(path.join(repo,'StatsDirectUI/Assets/Operations',f),'utf8');const name=text.match(/<name>([^<]+)<\/name>/)?.[1];if(name)operations[name]=true;
  }
  fs.writeFileSync(path.join(scratch,'Content/analysis-menu.json'),JSON.stringify({operations}));
  const tests=fs.readdirSync(path.join(scratch,'Learn')).filter(f=>f.endsWith('.test.mjs'));
  for(const f of tests){
    const file=path.join(scratch,'Learn',f);
    let source=fs.readFileSync(file,'utf8').replaceAll("'/Library/Frameworks/R.framework/Resources/bin/Rscript'",JSON.stringify(r));
    // Rscript.exe's command-line parser does not preserve multiline -e arguments
    // the way Unix does. Write the identical expression as a temporary .R script.
    if(source.includes("import {execFileSync} from 'node:child_process';"))source=source.replace("import {execFileSync} from 'node:child_process';",`
      import {execFileSync as nativeExecFileSync} from 'node:child_process';
      import {writeFileSync as writeExpression} from 'node:fs';
      const execFileSync=(program,args,options)=>{
        if(args.includes('-e')){const i=args.indexOf('-e'),file=${JSON.stringify(scratch.replaceAll('\\','/'))}+'/'+crypto.randomUUID()+'.R';writeExpression(file,args[i+1]);args=[...args.slice(0,i),file];}
        return nativeExecFileSync(program,args,{...options,env:{...process.env,LC_ALL:'C',LANG:'C'}});
      };`);
    fs.writeFileSync(file,source);
  }
  const result=spawnSync(process.execPath,['--test',...tests.map(f=>path.join(scratch,'Learn',f))],{stdio:'inherit',cwd:scratch});
  if(result.error)throw result.error;process.exitCode=result.status??1;
}finally{
  const resolved=path.resolve(scratch),parent=path.resolve(os.tmpdir());
  if(path.dirname(resolved)!==parent||!path.basename(resolved).startsWith('StatsDirect-shared-learning-'))throw Error('Refusing to remove an unexpected test directory.');
  fs.rmSync(resolved,{recursive:true,force:true});
}
