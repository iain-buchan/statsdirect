// Windows host presentation only. The pinned editor still owns formatting and history.
// These 24-unit paths match SharpToolbarRenderer and the Mac document toolbar.
window.WindowsReportToolbar = (() => {
  const icons = {
    save: '<path d="M4 3H17L21 7V21H3V3H4M7 3V9H16V3M7 21V14H17V21"/>',
    undo: '<path d="M8 4L3 9L8 14M3 9H14C23 9 23 21 14 21"/>',
    redo: '<path transform="translate(24 0) scale(-1 1)" d="M8 4L3 9L8 14M3 9H14C23 9 23 21 14 21"/>',
    cut: '<circle cx="6" cy="6" r="3"/><circle cx="6" cy="18" r="3"/><path d="M8 8L20 20M8 16L20 4"/>',
    copy: '<path d="M7 16H3V3H15V7"/><rect x="8" y="8" width="13" height="13"/>',
    paste: '<path d="M8 5H4V21H20V5H16M8 12H16M8 16H14"/><rect x="8" y="3" width="8" height="4"/>',
    find: '<circle cx="9" cy="9" r="6"/><path d="M14 14L21 21"/>',
    help: '<circle cx="12" cy="12" r="9.5"/><path d="M8.5 8C8.5 4.5 16 4.5 16 8.5C16 11 12 11 12 14M12 17V17.2"/>',
    r: '<path d="M7 21V3H13C19.5 3 19.5 12 13 12H7M12.5 12L18.5 21"/>',
    more: '<circle cx="4" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="20" cy="12" r="1"/>'
  };
  const svg = name => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[name]}</svg>`;
  let remembered = null, refresh = () => {};
  function remember() {
    const selection = getSelection(), root = document.getElementById('results');
    if (selection?.rangeCount && root.contains(selection.anchorNode) && root.contains(selection.focusNode))
      remembered = selection.getRangeAt(0).cloneRange();
  }
  function restore() {
    if (remembered && remembered.startContainer.isConnected && remembered.endContainer.isConnected) {
      const selection = getSelection();
      selection.removeAllRanges(); selection.addRange(remembered);
    }
  }
  function start() {
    const bar = document.getElementById('report-toolbar'), tools = document.getElementById('report-format-tools');
    const oldRow = tools.querySelector('.report-format-row'), formatting = [...oldRow.children];
    const more = document.getElementById('report-more-tools'), toggle = document.getElementById('report-more-toggle');
    const addText = bar.querySelector('[data-command="addText"]');
    const undo = oldRow.querySelector('[data-command="undo"]'), redo = oldRow.querySelector('[data-command="redo"]');
    undo.innerHTML = svg('undo'); redo.innerHTML = svg('redo');
    redo.title = 'Redo (Ctrl+Y / Ctrl+Shift+Z)'; redo.setAttribute('aria-label', redo.title);
    const row = document.createElement('div'); row.className = 'document-toolbar-row';
    const overflow = document.createElement('div'); overflow.className = 'report-overflow-commands';
    more.prepend(overflow);
    toggle.innerHTML = svg('more'); toggle.classList.add('report-icon-button');
    toggle.title = 'More commands'; toggle.setAttribute('aria-label', 'More commands');
    more.setAttribute('aria-label', 'More report commands');
    toggle.setAttribute('aria-haspopup', 'dialog'); more.setAttribute('role', 'dialog');
    function showMore(open, focus = false) {
      more.hidden = !open; toggle.setAttribute('aria-expanded', String(open));
      if (open && focus) more.querySelector('button:not(:disabled),select:not(:disabled),input:not(:disabled)')?.focus();
    }
    toggle.onclick = () => showMore(more.hidden);
    function button(name, label, shortcut, action, priority) {
      const b = document.createElement('button'); b.type = 'button'; b.className = 'report-icon-button';
      b.dataset.hostCommand = name; b.innerHTML = svg(name);
      b.title = shortcut ? `${label} (${shortcut})` : label; b.setAttribute('aria-label', b.title);
      b.onclick = () => { restore(); action(); }; b.dataset.priority = priority; return b;
    }
    const send = command => chrome.webview.postMessage({action:'command', command});
    const clipboard = command => chrome.webview.postMessage({action:'clipboard', command});
    const save = button('save', 'Save', 'Ctrl+S', () => send('save'), 100);
    const cut = button('cut', 'Cut', 'Ctrl+X', () => clipboard('cut'), 76);
    const copy = button('copy', 'Copy', 'Ctrl+C', () => clipboard('copy'), 85);
    const paste = button('paste', 'Paste', 'Ctrl+V', () => clipboard('paste'), 84);
    const find = button('find', 'Find', 'Ctrl+F', () => send('find'), 75);
    const help = button('help', 'Method help', 'F1', () => chrome.webview.postMessage({action:'help', context:currentHelpContext()}), 60);
    const r = button('r', 'Continue in R: selected result, otherwise latest eligible result', null, () => send('continueInR'), 65);
    function separator() { const s = document.createElement('span'); s.className = 'document-toolbar-separator'; s.setAttribute('role','separator'); return s; }
    undo.dataset.priority = 90; redo.dataset.priority = 89;
    const specific = formatting.filter(n => n !== undo && n !== redo && n !== toggle);
    for (const n of specific) n.dataset.priority = ({bold:74, italic:73, underline:72})[n.dataset.command] ?? 30;
    addText.dataset.priority = 10;
    const ordered = [save, separator(), undo, redo, separator(), cut, copy, paste, find, separator(), ...specific, addText, separator(), help, r];
    tools.replaceChildren(row, more); bar.replaceChildren(tools);
    row.append(...ordered, toggle);
    let pending = false;
    function layout() {
      pending = false;
      const active = document.activeElement, focusInToolbar = bar.contains(active);
      // Reparent the original controls, never clones: handlers, disabled states and formatting values survive.
      for (const n of ordered) row.insertBefore(n, toggle);
      for (const n of ordered) if (n.classList.contains('document-toolbar-separator')) n.hidden = false;
      const candidates = ordered.filter(n => !n.hidden && !n.classList.contains('document-toolbar-separator'));
      const widths = new Map(candidates.map(n => [n, n.getBoundingClientRect().width]));
      const separators = ordered.filter(n => n.classList.contains('document-toolbar-separator'));
      const available = row.clientWidth;
      let needed = candidates.reduce((sum,n) => sum + widths.get(n) + 4, 0) + separators.length * 13 + toggle.getBoundingClientRect().width;
      for (const n of [...candidates].sort((a,b) => Number(a.dataset.priority) - Number(b.dataset.priority))) {
        if (needed <= available) break;
        overflow.append(n); needed -= widths.get(n) + 4;
      }
      for (const n of ordered) if (n.parentElement === overflow) overflow.append(n);
      let previous = null;
      for (const n of [...row.children]) {
        if (n.classList.contains('document-toolbar-separator')) {
          n.hidden = !previous || previous.classList.contains('document-toolbar-separator');
          if (!n.hidden) previous = n;
        } else if (!n.hidden) previous = n;
      }
      if (toggle.previousElementSibling?.classList.contains('document-toolbar-separator')) toggle.previousElementSibling.hidden = true;
      if (focusInToolbar && active !== document.body && active.isConnected) {
        if (more.contains(active)) showMore(true);
        active.focus({preventScroll:true});
      }
    }
    function schedule() { if (!pending) { pending = true; requestAnimationFrame(layout); } }
    let lastWidth = -1;
    new ResizeObserver(() => { if (bar.clientWidth !== lastWidth) { lastWidth = bar.clientWidth; schedule(); } }).observe(bar);
    const picture = tools.querySelector('.report-picture-tools');
    let pictureHidden = picture?.hidden;
    if (picture) new MutationObserver(() => {
      // The editor also assigns hidden when its value is unchanged. Only relayout a real transition,
      // otherwise restoring toolbar focus could schedule another layout on every frame.
      if (picture.hidden !== pictureHidden) { pictureHidden = picture.hidden; schedule(); }
    }).observe(picture, {attributes:true, attributeFilter:['hidden']});
    refresh = () => {
      if (remembered && (!remembered.startContainer.isConnected || !remembered.endContainer.isConnected)) remembered = null;
      remember();
      const range = remembered, selected = range && range.startContainer.isConnected && !range.collapsed;
      const editable = document.getElementById('results').isContentEditable;
      cut.disabled = !selected || !editable; copy.disabled = !selected; paste.disabled = !editable;
      const selection = getSelection(), at = resultAt(selection?.rangeCount ? selection.anchorNode : null) ?? resultAt(range?.startContainer);
      const target = entryOf(at ?? entries.findLast(e => isRecord(e.record))?.id);
      r.disabled = !isRecord(target?.record);
    };
    document.addEventListener('selectionchange', refresh);
    document.addEventListener('focusin', refresh);
    document.addEventListener('pointerdown', event => { if (!bar.contains(event.target)) showMore(false); });
    bar.addEventListener('focusout', () => queueMicrotask(() => { if (!bar.contains(document.activeElement)) showMore(false); }));
    bar.addEventListener('keydown', event => {
      if (event.key === 'Escape' && !more.hidden) {
        event.preventDefault(); event.stopPropagation(); showMore(false); toggle.focus(); return;
      }
      if (event.target === toggle && event.key === 'ArrowDown') { event.preventDefault(); showMore(true, true); return; }
      if (event.target.tagName !== 'BUTTON' || !['ArrowLeft','ArrowRight','Home','End'].includes(event.key)) return;
      const area = more.contains(event.target) ? more : row;
      const buttons = [...area.querySelectorAll('button:not(:disabled),select:not(:disabled),input:not(:disabled)')].filter(n => n.getClientRects().length);
      const index = buttons.indexOf(event.target), step = event.key === 'ArrowLeft' ? -1 : 1;
      const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : (index + step + buttons.length) % buttons.length;
      event.preventDefault(); buttons[next]?.focus();
    }, true);
    refresh(); schedule();
  }
  return {start, refresh:() => refresh()};
})();
