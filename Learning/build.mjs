import {build} from 'esbuild';
import {readFile,access} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {verifyUpstream,verifyBundle} from './verify-upstream.mjs';
import {resolveAuthoredTutor,authoredBuildInputs,authoredBuildIsCurrent,assertAuthoredBuildInputsUnchanged,assertConsumedBuildInputsUnchanged,recordAuthoredBuild,promoteBuildOutputs} from './authored-tutor.mjs';
const local=path=>fileURLToPath(new URL(path,import.meta.url));
const learningRoot=local('./'),repoRoot=local('../');
if(process.argv.slice(2).some(arg=>arg!=='--authored-if-needed'))throw Error('Use node Learning/build.mjs [--authored-if-needed].');
const authored=await resolveAuthoredTutor(repoRoot);
if(process.argv.includes('--authored-if-needed')&&!authored){console.log('No authored built-in tutor is selected; checked-in assets are unchanged.');process.exit(0);}
const inputs=await authoredBuildInputs(repoRoot,authored);
if(process.argv.includes('--authored-if-needed')&&await authoredBuildIsCurrent(repoRoot,authored,inputs)){console.log('Authored built-in tutor assets are current ('+authored.manifest.bankVersion+').');process.exit(0);}
// The shared Learning code is the pinned submodule package; its pin, hashes and host contract are checked before every bundle.
const upstream=await verifyUpstream();
const packageRoot=local('./Shared/Learning/');
const lessonAsset=local('../StatsDirectUI/Assets/Learn/lessons.json');
const lessonBytes=authored?Buffer.from(authored.texts['lessons.json']):await readFile(lessonAsset);
const consumed=new Map();
async function readInput(file){
  const bytes=await readFile(file),relative=path.relative(repoRoot,file).replaceAll(path.sep,'/');
  const record={path:relative,sha256:createHash('sha256').update(bytes).digest('hex'),bytes:bytes.length};
  if(consumed.has(relative)&&consumed.get(relative).sha256!==record.sha256)throw Error('A consumed dependency changed while bundling: '+relative);
  consumed.set(relative,record);return bytes;
}
function captureRemainingInputs(b){
  b.onLoad({filter:/\.(?:mjs|cjs|js|json)$/},async({path:file})=>({contents:await readInput(file),loader:file.endsWith('.json')?'json':'js'}));
}
const learning=await build({
  entryPoints:[local('./Shared/Learning/app.mjs')],bundle:true,format:'iife',target:'safari17',
  outfile:local('../StatsDirectUI/Assets/Learn/app.js'),write:false,metafile:true,absWorkingDir:learningRoot,
  plugins:[{name:'windows-learning-host',setup(b){
    // The two imports every host remaps (Learning/Shared/Learning/README.md): the published lessons and the host's bank shim.
    b.onResolve({filter:/^\.\.\/Content\/Learn\/lessons\.json$/},args=>path.resolve(args.importer).startsWith(packageRoot)?{path:authored?.lessonsPath??lessonAsset}:undefined);
    if(authored){
      b.onResolve({filter:/^\.\/bank\.mjs$/},args=>path.resolve(args.importer).startsWith(packageRoot)?{path:authored.bankPath}:undefined);
      b.onLoad({filter:/[\\/]AuthoredTutor[\\/]bank\.mjs$/},()=>({contents:authored.texts['bank.mjs'],loader:'js'}));
      b.onLoad({filter:/[\\/]AuthoredTutor[\\/]lessons\.json$/},()=>({contents:authored.texts['lessons.json'],loader:'json'}));
    }
    captureRemainingInputs(b);
  }}]
});
const conversation=await build({
  entryPoints:[local('./conversation.mjs')],bundle:true,format:'iife',target:'chrome120',
  outfile:local('../StatsDirectUI/Assets/Tutor/conversation.js'),write:false,metafile:true,absWorkingDir:learningRoot,
  plugins:[{name:'windows-conversation',setup(b){
    // Preserve the Windows transcript's explicit line breaks while sharing the
    // sanitized Markdown, equations and code renderer with Learning.
    b.onLoad({filter:/[\\/]rich-text\.mjs$/},async({path})=>({contents:(await readInput(path)).toString('utf8').replace('mangle:false','breaks:true,mangle:false'),loader:'js'}));
    captureRemainingInputs(b);
  }}]
});
const dependencies=new Set();
for(const result of [learning,conversation])for(const name of Object.keys(result.metafile.inputs)){
  const file=path.resolve(learningRoot,name);dependencies.add(path.relative(repoRoot,file));
  if(file.includes(path.sep+'node_modules'+path.sep)){
    for(let parent=path.dirname(file);parent.startsWith(repoRoot);parent=path.dirname(parent)){
      const manifest=path.join(parent,'package.json');
      try{await access(manifest);dependencies.add(path.relative(repoRoot,manifest));await readInput(manifest);}catch(error){if(error.code!=='ENOENT')throw error;}
      if(parent===path.dirname(parent))break;
    }
  }
}
// Frozen authored JSON is separately guarded by its manifest and input hash.
// All other actual module bytes (including vendor sources) are checked here.
// Use the same ordinal order as the persisted dependency fingerprint.
const consumedRecords=[...consumed.values()].sort((a,b)=>a.path<b.path?-1:a.path>b.path?1:0);
const validate=async()=>{
  await assertAuthoredBuildInputsUnchanged(repoRoot,authored,inputs);
  await assertConsumedBuildInputsUnchanged(repoRoot,consumedRecords);
};
await validate();
const replacements=[];
for(const result of [learning,conversation])for(const output of result.outputFiles)
  replacements.push({path:output.path,bytes:Buffer.from(output.text.replace(/\r\n?/g,'\n').replace(/\n/g,'\r\n'))});
if(authored)replacements.push({path:lessonAsset,bytes:lessonBytes});
for(const relative of [...dependencies])if(!consumed.has(relative.replaceAll(path.sep,'/')))dependencies.delete(relative);
await promoteBuildOutputs(replacements,()=>recordAuthoredBuild(repoRoot,authored,inputs,[...dependencies],consumedRecords),validate);
await verifyBundle(local('../StatsDirectUI/Assets/Learn/app.js'),upstream.hostContract);
console.log(authored?'Built authored StatsDirect tutor: '+authored.manifest.lessonCount+' lessons, '+authored.manifest.questionCount+' questions; bank '+authored.manifest.bankVersion+'.':'Built the checked-in upstream Learning and conversation assets.');
