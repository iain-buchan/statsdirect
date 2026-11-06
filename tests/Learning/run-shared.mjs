// Run the shared Learning tests (Learning/Shared/Learning, the package pinned by the submodule)
// against Windows' shipped lessons, help and operation definitions, the way the package README
// describes a host running them: from a root of the host's own, with the host's bank shim beside
// the package, the host's node_modules above it, and the three variables set. No test file is
// patched; the package's test-config.mjs reads the variables.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath, pathToFileURL} from 'node:url';
import {spawnSync} from 'node:child_process';
const repo=fileURLToPath(new URL('../../',import.meta.url));
const r=process.env.STATSDIRECT_RSCRIPT;
if(!r||!fs.existsSync(r))throw Error('Set STATSDIRECT_RSCRIPT to the installed Windows Rscript.exe.');
// STATSDIRECT_LEARNING_PACKAGE names another copy of the package, to test a candidate before the pin moves.
const packageRoot=process.env.STATSDIRECT_LEARNING_PACKAGE?path.resolve(process.env.STATSDIRECT_LEARNING_PACKAGE):path.join(repo,'Learning/Shared/Learning');
if(!fs.existsSync(path.join(packageRoot,'app.mjs')))throw Error('The Learning package is missing at '+packageRoot+'; run git submodule update --init Learning/Shared.');
const scratch=fs.mkdtempSync(path.join(os.tmpdir(),'StatsDirect-shared-learning-'));
try{
  // The whole package, unchanged, plus the host's bank shim where the package imports './bank.mjs'.
  fs.cpSync(packageRoot,path.join(scratch,'Learn'),{recursive:true});
  fs.copyFileSync(path.join(repo,'Learning/AuthoredTutor/bank.mjs'),path.join(scratch,'Learn/bank.mjs'));
  // The host's dependencies, above the package as in the repository.
  fs.symlinkSync(path.join(repo,'Learning/node_modules'),path.join(scratch,'node_modules'),'junction');
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
  const env={...process.env,STATSDIRECT_RSCRIPT:r,STATSDIRECT_CONTENT_ROOT:path.join(scratch,'Content')};
  // Windows keeps no archived bank, so STATSDIRECT_LEARN_ARCHIVE is normally unset and that comparison is skipped by the package's rule; a named archive passes through.
  const result=spawnSync(process.execPath,['--test',...tests.map(f=>path.join(scratch,'Learn',f))],{stdio:'inherit',cwd:scratch,env});
  if(result.error)throw result.error;process.exitCode=result.status??1;
}finally{
  const resolved=path.resolve(scratch),parent=path.resolve(os.tmpdir());
  if(path.dirname(resolved)!==parent||!path.basename(resolved).startsWith('StatsDirect-shared-learning-'))throw Error('Refusing to remove an unexpected test directory.');
  fs.rmSync(resolved,{recursive:true,force:true});
}
