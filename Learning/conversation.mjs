import {richText} from './upstream/Learn/rich-text.mjs';
// The Script AI conversation page.  The program posts the whole conversation after every change: the messages so far and the reply
// being streamed.  Each message is rendered from its Markdown once; only the streamed reply is rendered again as it grows, and the
// view follows it while the reader is at the end, staying put when they have scrolled up or are selecting text.  Everything in a
// message is text to be escaped: Markdown is read into a small set of elements, and no markup in a reply reaches the page as markup.
(() => {
  const container = document.getElementById('conversation');
  const post = message => { try { window.chrome.webview.postMessage(message); } catch (error) { } };
  // Same offline, sanitized Markdown/TeX renderer as Learning and the Mac AI pane.
  const render = text => richText(text).replaceAll('data-code="r"', 'data-code="open"');

  // The page's state: the messages rendered, in order, and the streamed reply.
  const rendered = [];
  let partialElement = null, partialText = '', renders = 0, applied = 0, context = 0;
  function bind(element) {
    element.querySelectorAll('a').forEach(a=>{if(!/^https?:\/\//i.test(a.getAttribute('href')??''))a.removeAttribute('href');else {a.target='_blank';a.rel='noopener noreferrer';}});
    element.querySelectorAll('button[data-code]').forEach(button => button.onclick = () => {
      const code = button.closest('.code-block').querySelector('code').textContent;
      post({ action: 'code', what: button.dataset.code, code, context,
        messageIndex: [...container.children].indexOf(element),
        blockIndex: [...element.querySelectorAll('button[data-code=open]')].indexOf(button) });
      if (button.dataset.code === 'copy') { button.textContent = 'Copied'; setTimeout(() => { button.textContent = 'Copy'; }, 1500); }
    });
    element.querySelectorAll('a').forEach(a => { if (!/^https?:\/\//i.test(a.getAttribute('href') || '')) a.removeAttribute('href'); });
  }
  // The streamed reply's block, rendered again as it grows: only the children that changed are replaced, so that the paragraphs
  // before the one being written, and any selection in them, stay as they are.
  function renderInto(element, text) {
    const fresh = document.createElement('div'); fresh.innerHTML = render(text);
    const body = element.querySelector('.body'), old = [...body.childNodes], now = [...fresh.childNodes];
    let same = 0;
    while (same < old.length && same < now.length && old[same].isEqualNode(now[same])) same++;
    for (let k = old.length - 1; k >= same; k--) old[k].remove();
    for (let k = same; k < now.length; k++) body.appendChild(now[k]);
    bind(element); renders++;
  }
  function block(role, text) {
    const element = document.createElement('section');
    element.className = 'message ' + (role === 'user' ? 'user' : 'assistant');
    element.innerHTML = '<div class="who">' + (role === 'user' ? 'You' : 'StatsDirect AI') + '</div><div class="body">' + render(text) + '</div>';
    bind(element); renders++;
    return element;
  }
  const scroller = () => document.scrollingElement || document.documentElement;
  const atEnd = () => { const d = scroller(); return d.scrollTop + d.clientHeight >= d.scrollHeight - 4; };

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
    const selection = m.reset ? null : saveSelection();
    const top = scroller().scrollTop;
    const follow = !!m.reset || (!selection && atEnd());
    if (m.reset) { container.replaceChildren(); rendered.length = 0; partialElement = null; partialText = ''; }
    const messages = Array.isArray(m.messages) ? m.messages : [];
    let same = 0;
    while (same < rendered.length && same < messages.length && rendered[same].role === messages[same].role && rendered[same].text === messages[same].text) same++;
    for (let j = rendered.length - 1; j >= same; j--) { rendered[j].element.remove(); rendered.pop(); }
    for (let j = same; j < messages.length; j++) {
      const role = messages[j].role === 'user' ? 'user' : 'assistant', text = String(messages[j].text ?? '');
      let element;
      if (j === messages.length - 1 && partialElement && role === 'assistant' && text === partialText && !(m.partial)) {
        // the streamed reply has become the last message, as it is: keep its block rather than render it again
        element = partialElement; element.classList.remove('partial'); partialElement = null; partialText = '';
      }
      else { element = block(role, text); container.insertBefore(element, partialElement); }
      rendered.push({ role, text, element });
    }
    const partial = String(m.partial ?? '');
    if (partial !== partialText) {
      if (!partial) { if (partialElement) partialElement.remove(); partialElement = null; }
      else if (partialElement) renderInto(partialElement, partial);
      else { partialElement = block('assistant', partial); partialElement.classList.add('partial'); container.appendChild(partialElement); }
      partialText = partial;
    }
    if (partialElement) partialElement.querySelectorAll('[data-code]').forEach(button=>button.disabled=true);
    for (const row of rendered) row.element.querySelectorAll('[data-code]').forEach(button=>button.disabled=false);
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
  window.chrome?.webview?.addEventListener('message', e => { if (e.data && e.data.action === 'update') update(e.data); });
  post({ action: 'ready' });
})();
