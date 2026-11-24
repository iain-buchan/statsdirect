import {richText} from './Shared/Learning/rich-text.mjs';
// The Script AI conversation page.  The program posts the whole conversation after every change: the messages so far and the reply
// being streamed.  Each message is rendered from its Markdown once; only the streamed reply is rendered again as it grows, and the
// view follows it while the reader is at the end, staying put when they have scrolled up or are selecting text.  Everything in a
// message is text to be escaped: Markdown is read into a small set of elements, and no markup in a reply reaches the page as markup.
// Worksheet controls come separately from the native host and never from the reply's Markdown.
(() => {
  const container = document.getElementById('conversation');
  const post = message => { try { window.chrome.webview.postMessage(message); } catch (error) { } };
  // Same offline, sanitized Markdown/TeX renderer as Learning and the Mac AI pane.
  function render(text, foldRCode) {
    const body = document.createElement('div');
    body.className = 'body';
    body.innerHTML = richText(text).replaceAll('data-code="r"', 'data-code="open"');
    if (foldRCode) {
      for (const block of body.querySelectorAll('.code-block')) {
        if (!block.querySelector('[data-code="open"]')) continue;
        const details = document.createElement('details');
        details.className = 'r-code-disclosure';
        const summary = document.createElement('summary');
        summary.innerHTML = '<span class="show-r-code">Show R code</span><span class="hide-r-code">Hide R code</span>';
        block.replaceWith(details);
        details.append(summary, block);
      }
    }
    return body;
  }

  // The page's state: the messages rendered, in order, and the streamed reply.
  const rendered = [];
  let welcomeElement = null;
  let worksheetActionElement = null, worksheetActionKey = '';
  let partialElement = null, partialText = '', renders = 0, applied = 0, context = 0, requests = 0, copying = null, collapseRCode = false;
  function bind(element) {
    element.querySelectorAll('a').forEach(a=>{if(!/^https?:\/\//i.test(a.getAttribute('href')??''))a.removeAttribute('href');else {a.target='_blank';a.rel='noopener noreferrer';}});
    element.querySelectorAll('button[data-code]').forEach(button => button.onclick = () => {
      const code = button.closest('.code-block').querySelector('code').textContent;
      // Copy is confirmed by the program: the button says "Copied" only when the program answers that it has copied the block.
      const request = ++requests;
      if (button.dataset.code === 'copy') copying = { request, button };
      post({ action: 'code', what: button.dataset.code, code, context, request,
        messageIndex: [...container.children].indexOf(element),
        blockIndex: [...element.querySelectorAll('button[data-code=open]')].indexOf(button) });
    });
    element.querySelectorAll('a').forEach(a => { if (!/^https?:\/\//i.test(a.getAttribute('href') || '')) a.removeAttribute('href'); });
  }
  // The streamed reply's block, rendered again as it grows: only the children that changed are replaced, so that the paragraphs
  // before the one being written, and any selection in them, stay as they are.
  function renderInto(element, text) {
    const body = element.querySelector('.body'), fresh = render(text, collapseRCode);
    // Keep the reader's disclosure state when a reply grows or finishes.
    if (collapseRCode) {
      fresh.querySelectorAll('[data-code]').forEach(button => button.disabled = true);
      const previous = body.querySelectorAll('.r-code-disclosure');
      fresh.querySelectorAll('.r-code-disclosure').forEach((details, index) => {
        details.open = previous[index]?.open ?? false;
      });
    }
    const old = [...body.childNodes], now = [...fresh.childNodes];
    let same = 0;
    while (same < old.length && same < now.length && old[same].isEqualNode(now[same])) same++;
    for (let k = old.length - 1; k >= same; k--) old[k].remove();
    for (let k = same; k < now.length; k++) body.appendChild(now[k]);
    bind(element); renders++;
  }
  function block(role, text) {
    const element = document.createElement('section');
    element.className = 'message ' + (role === 'user' ? 'user' : 'assistant');
    const who = document.createElement('div');
    who.className = 'who';
    who.textContent = role === 'user' ? 'You' : 'StatsDirect AI';
    element.append(who, render(text, collapseRCode && role === 'assistant'));
    bind(element); renders++;
    return element;
  }
  const scroller = () => document.scrollingElement || document.documentElement;
  const atEnd = () => { const d = scroller(); return d.scrollTop + d.clientHeight >= d.scrollHeight - 4; };

  function updateWorksheetAction(action) {
    const available = typeof action?.token === 'string' && ['compare', 'choose', 'confirm'].includes(action.kind);
    const key = available ? JSON.stringify(action) : '';
    if (key === worksheetActionKey) return;
    const transferFocus = worksheetActionElement?.contains(document.activeElement);
    worksheetActionElement?.remove();
    worksheetActionElement = null;
    worksheetActionKey = key;
    if (!available) return;

    const section = document.createElement('section');
    section.className = 'message assistant worksheet-analysis';
    section.dataset.worksheetAction = action.kind;
    section.dataset.token = action.token;
    section.setAttribute('aria-label', action.kind === 'confirm' ? 'Proposed worksheet method'
      : action.kind === 'choose' ? 'Compare two means' : 'Choose a worksheet method');
    const actions = document.createElement('div');
    actions.className = 'worksheet-analysis-actions';
    const requestContext = context;
    function button(label, choice) {
      const option = document.createElement('button');
      option.type = 'button';
      option.textContent = label;
      option.dataset.action = 'worksheet-analysis';
      option.dataset.choice = choice;
      option.addEventListener('click', () => post({ action: 'worksheetAnalysis', token: action.token, choice, context: requestContext }));
      actions.append(option);
    }
    if (action.kind === 'compare') {
      if (action.showCompare !== false) button('Compare two means', 'compare');
      button('Choose a method', 'browse');
    } else if (action.kind === 'choose') {
      const question = document.createElement('p');
      question.textContent = 'Were these measurements taken from the same people or units?';
      section.append(question);
      button('Same people or units', 'paired');
      button('Separate groups', 'independent');
    } else {
      const heading = document.createElement('h2');
      heading.textContent = String(action.method ?? '');
      section.append(heading);
      const details = document.createElement('dl');
      const sheet = [action.workbook, action.sheet].filter(Boolean).join(' · ');
      for (const [label, value] of [
        ['Menu', action.menuPath],
        ['Worksheet', sheet],
        ['Selection', action.selection],
        ['Expected inputs', action.inputs],
        ['Results', action.destination]
      ]) {
        if (!value) continue;
        const term = document.createElement('dt'), description = document.createElement('dd');
        term.textContent = label;
        description.textContent = String(value);
        details.append(term, description);
      }
      section.append(details);
      if (action.effect) {
        const effect = document.createElement('p');
        effect.className = 'worksheet-analysis-effect';
        effect.textContent = String(action.effect);
        section.append(effect);
      }
      button('Continue to analysis', 'continue');
      if (action.choice === 'paired' || action.choice === 'independent') button('Change design', 'change');
    }
    section.append(actions);
    container.append(section);
    worksheetActionElement = section;
    if (transferFocus) actions.querySelector('button')?.focus({ preventScroll: true });
  }

  // A streamed paragraph can be replaced when its Markdown changes. Bookmark the
  // selection within each message body so removing those nodes does not remove
  // the reader's selection. Keep anchor/focus separately for backwards selections.
  function saveSelection() {
    const selection = getSelection();
    if (!selection || selection.isCollapsed || !container.contains(selection.anchorNode) || !container.contains(selection.focusNode)) return null;
    const point = (node, offset) => {
      const element = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
      const body = element.closest('.body') || container;
      const range = document.createRange(); range.selectNodeContents(body); range.setEnd(node, offset);
      return { node, offset, body, index: [...container.children].indexOf(body.closest('.message')), textOffset: range.toString().length };
    };
    return { anchor: point(selection.anchorNode, selection.anchorOffset), focus: point(selection.focusNode, selection.focusOffset) };
  }
  function restoreSelection(bookmark) {
    if (!bookmark) return;
    const point = saved => {
      if (container.contains(saved.node)) return [saved.node, Math.min(saved.offset, saved.node.nodeType === Node.TEXT_NODE ? saved.node.length : saved.node.childNodes.length)];
      const body = saved.body.isConnected ? saved.body : container.children[saved.index]?.querySelector('.body');
      if (!body) return null;
      const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
      let node, previous, remaining = saved.textOffset;
      while ((node = walker.nextNode())) {
        if (remaining <= node.length) return [node, remaining];
        remaining -= node.length; previous = node;
      }
      return previous ? [previous, previous.length] : [body, 0];
    };
    const anchor = point(bookmark.anchor), focus = point(bookmark.focus);
    if (anchor && focus) getSelection().setBaseAndExtent(anchor[0], anchor[1], focus[0], focus[1]);
  }

  function update(m) {
    context = m.context;
    const reset = m.reset || collapseRCode !== Boolean(m.collapseRCode);
    collapseRCode = Boolean(m.collapseRCode);
    const selection = reset ? null : saveSelection();
    const top = scroller().scrollTop;
    const follow = reset || (!selection && atEnd());
    if (reset) {
      container.replaceChildren(); rendered.length = 0; partialElement = null; partialText = ''; welcomeElement = null;
      worksheetActionElement = null; worksheetActionKey = '';
    }
    const messages = Array.isArray(m.messages) ? m.messages : [];
    const partial = String(m.partial ?? '');
    if (!messages.length && !partial && m.welcome) {
      if (!welcomeElement) {
        welcomeElement = block('assistant', m.welcome);
        welcomeElement.classList.add('welcome');
        container.appendChild(welcomeElement);
      }
    } else if (welcomeElement) {
      welcomeElement.remove();
      welcomeElement = null;
    }
    let same = 0;
    while (same < rendered.length && same < messages.length && rendered[same].role === messages[same].role && rendered[same].text === messages[same].text) same++;
    for (let j = rendered.length - 1; j >= same; j--) { rendered[j].element.remove(); rendered.pop(); }
    for (let j = same; j < messages.length; j++) {
      const role = messages[j].role === 'user' ? 'user' : 'assistant', text = String(messages[j].text ?? '');
      let element;
      if (j === messages.length - 1 && partialElement && role === 'assistant' && (text === partialText || collapseRCode) && !m.partial) {
        // Keep the streamed block on completion, including any open R disclosures.
        element = partialElement;
        if (text !== partialText) renderInto(element, text);
        element.classList.remove('partial'); partialElement = null; partialText = '';
      }
      else { element = block(role, text); container.insertBefore(element, partialElement ?? worksheetActionElement); }
      rendered.push({ role, text, element });
    }
    if (partial !== partialText) {
      if (!partial) { if (partialElement) partialElement.remove(); partialElement = null; }
      else if (partialElement) renderInto(partialElement, partial);
      else { partialElement = block('assistant', partial); partialElement.classList.add('partial'); container.insertBefore(partialElement, worksheetActionElement); }
      partialText = partial;
    }
    if (partialElement) partialElement.querySelectorAll('[data-code]').forEach(button=>button.disabled=true);
    for (const row of rendered) row.element.querySelectorAll('[data-code]').forEach(button=>button.disabled=false);
    updateWorksheetAction(m.worksheetAction);
    restoreSelection(selection);
    scroller().scrollTop = follow ? scroller().scrollHeight : top;
    if (typeof m.sequence === 'number') applied = m.sequence;
  }

  // For the program and its checks.
  const textNodes = () => { const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT); const nodes = []; let n; while ((n = walker.nextNode())) nodes.push(n); return nodes; };
  window.conversation = {
    state: () => { const d = scroller(); return { count: rendered.length, partial: partialText.length, renders, applied, atEnd: atEnd(), scrollTop: d.scrollTop, scrollHeight: d.scrollHeight, clientHeight: d.clientHeight, text: container.innerText }; },
    select: text => { for (const node of textNodes()) { const at = node.data.indexOf(text); if (at >= 0) { const range = document.createRange(); range.setStart(node, at); range.setEnd(node, at + text.length); const s = getSelection(); s.removeAllRanges(); s.addRange(range); return true; } } return false; },
    selection: () => getSelection().toString(),
    scrollToText: text => { for (const node of textNodes()) if (node.data.includes(text)) { node.parentElement.scrollIntoView({ block: 'start' }); return true; } return false; },
    scrollToEnd: () => { scroller().scrollTop = scroller().scrollHeight; return atEnd(); },
    scrollTo: top => { scroller().scrollTop = top; return scroller().scrollTop; },
    update
  };
  function copied(m) {
    if (!copying || copying.request !== m.request) return;
    const button = copying.button; copying = null;
    button.textContent = 'Copied'; setTimeout(() => { button.textContent = 'Copy'; }, 1500);
  }
  window.chrome?.webview?.addEventListener('message', e => { if (e.data && e.data.action === 'update') update(e.data); else if (e.data && e.data.action === 'copied') copied(e.data); });
  post({ action: 'ready' });
})();
