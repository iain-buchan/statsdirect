/* Windows document commands around the shared R console; no R evaluation here. */
(function () {
  'use strict';
  function install() {
    const script = document.getElementById('script-source');
    const input = document.getElementById('console-input');
    const transcript = document.getElementById('transcript');
    if (!script || !input || !transcript || !window.StatsDirectRConsole) return;
    let target = input, transcriptTarget = false, contextSource = null;
    let lastState = '', scheduled = false, undo = false, redo = false;
    const mode = () => document.getElementById('script-panel').hidden ? 'console' : 'script';
    const post = message => window.chrome?.webview?.postMessage({channel: 'statsdirect-r-host', ...message});
    const field = () => {
      const expected = mode() === 'script' ? script : input;
      return target === expected ? target : expected;
    };
    function selectedText() {
      if (transcriptTarget) return window.getSelection()?.toString() || '';
      const current = field();
      return current.value.slice(current.selectionStart, current.selectionEnd);
    }
    function state() {
      const current = field();
      const editable = !transcriptTarget && !current.disabled && !current.readOnly;
      if (document.activeElement === current && editable) {
        undo = document.queryCommandEnabled('undo');
        redo = document.queryCommandEnabled('redo');
      }
      const value = {
        action: 'editorState', mode: mode(), editable,
        hasSelection: selectedText().length > 0,
        selectedText: selectedText(), canUndo: editable && undo, canRedo: editable && redo
      };
      const key = JSON.stringify(value);
      if (key !== lastState) { lastState = key; post(value); }
    }
    function schedule() {
      if (scheduled) return;
      scheduled = true;
      requestAnimationFrame(() => { scheduled = false; state(); });
    }
    function focusField() {
      const current = field();
      if (current.disabled || current.readOnly) return null;
      transcriptTarget = false; target = current;
      current.focus({preventScroll: true});
      return current;
    }
    function changed(current) {
      // Chromium normally emits input for execCommand; dispatching again is
      // harmless and also keeps programmatic replacements in the shared model.
      current.dispatchEvent(new Event('input', {bubbles: true}));
      schedule();
    }
    function insertText(value) {
      const current = focusField();
      if (!current || typeof value !== 'string') return false;
      const ok = document.execCommand('insertText', false, value);
      if (ok) changed(current);
      return ok;
    }
    function edit(action, value) {
      if (action === 'copy') return selectedText();
      if (transcriptTarget) return false;
      const current = focusField();
      if (!current) return false;
      if (action === 'paste' || action === 'insertText') return insertText(value || '');
      if (!['undo', 'redo', 'delete', 'cut'].includes(action)) return false;
      if (action === 'cut' && current.selectionStart === current.selectionEnd) return false;
      const ok = document.execCommand(action === 'cut' ? 'delete' : action, false);
      if (ok) changed(current);
      return ok;
    }
    function selectAll() {
      if (transcriptTarget) {
        const range = document.createRange(); range.selectNodeContents(transcript);
        const selection = window.getSelection(); selection.removeAllRanges(); selection.addRange(range);
      } else {
        const current = field(); current.focus({preventScroll: true}); current.select();
      }
      schedule(); return true;
    }
    function scriptField() {
      window.StatsDirectRConsole.setMode('script');
      target = script; transcriptTarget = false;
      script.focus({preventScroll: true}); return script;
    }
    const literalPattern = (term, matchCase) => new RegExp(term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), matchCase ? 'g' : 'gi');
    function findNext(term, matchCase) {
      if (!term) return false;
      const current = scriptField();
      const pattern = literalPattern(term, matchCase);
      pattern.lastIndex = current.selectionEnd;
      let match = pattern.exec(current.value);
      if (!match) { pattern.lastIndex = 0; match = pattern.exec(current.value); }
      if (!match) return false;
      const at = match.index;
      current.setSelectionRange(at, at + match[0].length);
      // Scroll only the editor, keeping the main window and transcript still.
      const lines = current.value.slice(0, at).split('\n').length - 1;
      const height = parseFloat(getComputedStyle(current).lineHeight) || 18;
      current.scrollTop = Math.max(0, lines * height - current.clientHeight / 2);
      schedule(); return true;
    }
    function replaceSelection(term, replacement, matchCase) {
      const current = scriptField();
      const selected = current.value.slice(current.selectionStart, current.selectionEnd);
      const match = term ? literalPattern(term, matchCase).exec(selected) : null;
      if (!match || match.index !== 0 || match[0].length !== selected.length) return false;
      return insertText(replacement);
    }
    function replaceAll(term, replacement, matchCase) {
      if (!term) return 0;
      const current = scriptField(), original = current.value;
      let count = 0;
      const result = original.replace(literalPattern(term, matchCase), () => { count++; return replacement; });
      if (!count) return 0;
      current.select();
      return insertText(result) ? count : 0;
    }
    const more = document.createElement('button');
    more.id = 'windows-more'; more.type = 'button'; more.textContent = 'More';
    more.title = 'More R session commands'; more.setAttribute('aria-label', 'More R session commands');
    more.addEventListener('click', () => post({action: 'more'}));
    document.querySelector('.toolbar').append(more);
    document.addEventListener('focusin', event => {
      if (event.target === script || event.target === input) { target = event.target; transcriptTarget = false; }
      else if (event.target === transcript) transcriptTarget = true;
      schedule();
    });
    transcript.addEventListener('pointerdown', event => {
      if (!event.target.closest('button')) { transcriptTarget = true; schedule(); }
    });
    document.addEventListener('contextmenu', event => {
      const image = event.target.closest?.('figure.plot img');
      contextSource = image?.getAttribute('src') || null;
      if (event.target === script || event.target === input) { target = event.target; transcriptTarget = false; }
      else if (transcript.contains(event.target)) transcriptTarget = true;
      schedule();
    }, true);
    for (const name of ['input', 'keyup', 'mouseup', 'selectionchange', 'focusout']) document.addEventListener(name, schedule);
    document.addEventListener('keydown', event => {
      if (event.isComposing || event.altKey) return;
      const key = event.key.toLowerCase();
      const command = event.key === 'F1' ? 'help' : event.ctrlKey && !event.metaKey
        ? ({s: 'save', f: 'find', h: 'replace', p: 'print'})[key] : null;
      if (command) { event.preventDefault(); post({action: command}); }
    });
    for (const tab of document.querySelectorAll('[role="tab"]')) tab.addEventListener('click', () => {
      target = field(); transcriptTarget = false; schedule();
    });
    window.StatsDirectRHost = {
      setScript(value) {
        if (typeof value !== 'string') return false;
        if (script.value !== value) { script.value = value; changed(script); }
        return true;
      },
      edit, selectedText, selectAll, findNext, replaceSelection, replaceAll,
      focus() { if (transcriptTarget) transcript.focus({preventScroll: true}); else field().focus({preventScroll: true}); },
      contextChartSource: () => contextSource,
      refresh: () => { lastState = ''; schedule(); }
    };
    state();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', install, {once: true});
  else install();
})();
