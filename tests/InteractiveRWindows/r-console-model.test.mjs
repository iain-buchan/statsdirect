import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const require = createRequire(import.meta.url);
const {ConsoleModel} = require('../../StatsDirectUI/Assets/RConsole/console.js');

const event = (type, seq, fields = {}) => ({protocolVersion: 1, sessionId: 'retained-session', generation: 1, type, seq, ...fields});
const entered = 'x <- 42';
const script = '# authored introduction\nx <- 42\n# user-edited ending\n';
const replay = [
  event('inputEcho', 1, {origin: 'console', text: entered, prompt: '> '}),
  event('output', 2, {text: '[1] 42\n', stream: 'stdout'}),
  event('state', 3, {state: 'ready', prompt: '> '})
];
function restore(model, source = script) {
  assert.equal(model.receive(event('session', 0, {script: source, events: replay, capabilities: {consoleInput: true}})), true);
  return model;
}

test('initial native replay restores the authoritative edited script once', () => {
  const model = restore(new ConsoleModel());
  assert.equal(model.script, script);
  assert.equal(model.entries.filter(entry => entry.kind === 'input').length, 1);
  assert.equal(model.entries.find(entry => entry.kind === 'output').text, '[1] 42\n');
  assert.equal(model.state, 'ready');
});

test('repeated renderer recovery never appends replayed console commands to Script', () => {
  let model = restore(new ConsoleModel());
  for (let reload = 0; reload < 3; reload++) {
    model = restore(new ConsoleModel(), model.script);
    assert.equal(model.script, script);
    assert.deepEqual(model.history, [entered]);
  }
});

test('restored console history recalls commands while new commands append exactly once', () => {
  const model = restore(new ConsoleModel());
  assert.equal(model.navigateHistory(-1, 'unfinished draft'), entered);
  assert.equal(model.navigateHistory(1, ''), 'unfinished draft');
  const next = 'x + 1';
  assert.equal(model.receive(event('inputEcho', 4, {origin: 'console', text: next, prompt: '> '})), true);
  assert.equal(model.script, script + next + '\n');
  assert.deepEqual(model.history, [entered, next]);
  assert.equal(model.receive(event('inputEcho', 4, {origin: 'console', text: next, prompt: '> '})), false);
  assert.equal(model.script, script + next + '\n');
});

test('optional file notice metadata survives live output and renderer recovery', () => {
  const model = restore(new ConsoleModel());
  const filePath = 'C:\\R output\\résumé #1.txt', label = 'résumé #1.txt';
  const file = event('notice', 4, {text: 'File written: ' + label, filePath, label});
  assert.equal(model.receive(file), true);
  const notice = model.entries.at(-1);
  assert.deepEqual(notice, {kind: 'notice', text: 'File written: ' + label, filePath, label});
  assert.equal(model.script, script);
  const recovered = new ConsoleModel();
  assert.equal(recovered.receive(event('session', 0, {script, events: [...replay, file]})), true);
  assert.deepEqual(recovered.entries.at(-1), notice);
  assert.equal(recovered.script, script);
  assert.equal(recovered.receive(event('notice', 5, {text: 'Plain notice remains supported'})), true);
  assert.equal(recovered.entries.at(-1).text, 'Plain notice remains supported');
  assert.equal(recovered.entries.at(-1).filePath, undefined);
});


test('executed startup batches enter recall intact without duplicating Script or executing recalled code', () => {
  const source = '# Startup example\nvalues <- c(\n  1, 2, 3\n)\nmean(values)\n';
  const model = new ConsoleModel();
  assert.equal(model.receive(event('session', 0, {script: source})), true);
  assert.deepEqual(model.history, [], 'opening source without running it does not create executed history');
  const submitted = event('inputEcho', 1, {origin: 'script', text: source, prompt: '> '});
  assert.equal(model.receive(submitted), true);
  assert.equal(model.script, source);
  assert.deepEqual(model.history, [source]);
  assert.equal(model.navigateHistory(-1, 'unfinished <- 7'), source);
  assert.equal(model.navigateHistory(1, ''), 'unfinished <- 7');
  assert.equal(model.script, source, 'recall is an edit, not a new execution or Script append');
  assert.equal(model.receive(submitted), false, 'repeated event is not another history entry');
  const recovered = new ConsoleModel();
  assert.equal(recovered.receive(event('session', 0, {script: source, events: [submitted]})), true);
  assert.deepEqual(recovered.history, [source]);
  assert.equal(recovered.script, source);
});

test('renderer recovery retains unanswered questions after Stop or error without inventing replies or code', () => {
  for (const status of ['interrupted','error']) {
    const events = [
      event('inputEcho',1,{origin:'script',commandId:'cmd-question',text:'answer <- readline(prompt)',prompt:'> '}),
      event('inputRequested',2,{requestId:'question-1',commandId:'cmd-question',prompt:'Which method? '}),
      event('commandFinished',3,{status}),
      event('state',4,{state:'ready',prompt:'> '})
    ];
    const authored = '# unexecuted edits\nanswer <- readline(prompt)\n';
    for (let reload=0;reload<3;reload++) {
      const model = new ConsoleModel();
      assert.equal(model.receive(event('session',0,{script:authored,events})),true);
      const questions=model.entries.filter(entry=>entry.kind==='question');
      assert.equal(questions.length,1); assert.equal(questions[0].text,'Which method? ');
      assert.equal(questions[0].commandId,'cmd-question'); assert.equal(questions[0].answered,false);
      assert.equal(model.entries.some(entry=>entry.origin==='input'),false);
      assert.equal(model.script,authored); assert.deepEqual(model.history,['answer <- readline(prompt)']);
    }
  }
});

test('a pending question recovers its reply target and an answered question replays once', () => {
  const authored = '# source stays exactly edited\n';
  const question = event('inputRequested',1,{requestId:'question-1',commandId:'cmd-question',prompt:'Answer? '});
  const model=new ConsoleModel();
  assert.equal(model.receive(event('session',0,{script:authored,events:[question]})),true);
  assert.deepEqual(model.submission(''),{protocolVersion:1,action:'inputReply',sessionId:'retained-session',generation:1,requestId:'question-1',text:''});
  const reply=event('inputEcho',2,{origin:'input',requestId:'question-1',prompt:'Answer? ',text:'answer data'});
  const events=[question,reply,event('state',3,{state:'ready',prompt:'> '})];
  for(let reload=0;reload<3;reload++) {
    const restored=new ConsoleModel();
    assert.equal(restored.receive(event('session',0,{script:authored,events})),true);
    assert.equal(restored.snapshot().outputText,'Answer? \nanswer data');
    assert.equal(restored.entries.filter(entry=>entry.kind==='question').length,1);
    assert.equal(restored.entries[0].answered,true);
    assert.equal(restored.script,authored);assert.deepEqual(restored.history,[]);
  }
});
