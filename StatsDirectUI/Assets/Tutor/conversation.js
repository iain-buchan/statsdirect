// The Script AI conversation page.  The program posts the whole conversation after every change: the messages so far and the reply
// being streamed.  Each message is rendered from its Markdown once; only the streamed reply is rendered again as it grows, and the
// view follows it while the reader is at the end, staying put when they have scrolled up or are selecting text.  Everything in a
// message is text to be escaped: Markdown is read into a small set of elements, and no markup in a reply reaches the page as markup.
(() => {
  const container = document.getElementById('conversation');
  const post = message => { try { window.chrome.webview.postMessage(message); } catch (error) { } };
  const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // Inline Markdown on text that is already escaped: code spans, bold, italics and links to http(s) addresses; a newline is a break.
  function inline(text) {
    const codes = [];
    text = text.replace(/`([^`\n]+)`/g, (_, code) => { codes.push(code); return '\u0000' + (codes.length - 1) + '\u0000'; });
    text = text.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/__(.+?)__/g, '<strong>$1</strong>');
    text = text.replace(/(^|[^*\w])\*([^*\n]+?)\*(?!\w)/g, '$1<em>$2</em>').replace(/(^|[^_\w])_([^_\n]+?)_(?!\w)/g, '$1<em>$2</em>');
    text = text.replace(/\[([^\]\n]+)\]\((https?:\/\/[^\s)]+)\)/g, (_, label, url) => '<a href="' + url + '" target="_blank" rel="noopener noreferrer">' + label + '</a>');
    text = text.replace(/\u0000(\d+)\u0000/g, (_, i) => '<code>' + codes[i] + '</code>');
    return text.replace(/\n/g, '<br>');
  }

  function codeBlock(code, language) {
    const r = /^(r|rscript)$/.test(language);
    return '<div class="code-block"><div class="code-actions"><span>' + (r ? 'R' : (language ? esc(language) : 'Code')) + '</span>' +
      '<button type="button" data-code="copy">Copy</button>' + (r ? '<button type="button" data-code="open">Open in R</button>' : '') +
      '</div><pre><code>' + esc(code) + '</code></pre></div>';
  }

  // A list: items at one indentation, a deeper indentation as a list within the item, an indented plain line as more of the item.
  function list(lines, start) {
    const item = /^(\s*)([-*+]|\d+[.)])\s+(.*)$/;
    let m = item.exec(lines[start]);
    const indent = m[1].length, ordered = /\d/.test(m[2]);
    const items = []; let i = start;
    while (i < lines.length) {
      m = item.exec(lines[i]);
      if (m && m[1].length === indent && /\d/.test(m[2]) === ordered) { items.push([m[3]]); i++; continue; }
      if (m && m[1].length > indent && items.length) { const [html, next] = list(lines, i); items[items.length - 1].push({ sub: html }); i = next; continue; }
      if (!m && items.length && lines[i].trim() !== '' && /^\s+/.test(lines[i])) { items[items.length - 1].push(lines[i].trim()); i++; continue; }
      break;
    }
    const tag = ordered ? 'ol' : 'ul';
    const html = '<' + tag + '>' + items.map(parts => '<li>' + parts.map(p => typeof p === 'string' ? inline(esc(p)) : p.sub).join(' ') + '</li>').join('') + '</' + tag + '>';
    return [html, i];
  }

  function table(lines, start) {
    const cells = line => line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(c => inline(esc(c.trim())));
    const head = cells(lines[start]); const body = []; let i = start + 2;
    while (i < lines.length && lines[i].includes('|') && lines[i].trim() !== '') body.push(cells(lines[i++]));
    const html = '<table><thead><tr>' + head.map(c => '<th>' + c + '</th>').join('') + '</tr></thead><tbody>' +
      body.map(row => '<tr>' + row.map(c => '<td>' + c + '</td>').join('') + '</tr>').join('') + '</tbody></table>';
    return [html, i];
  }

  // Block Markdown: fenced code, headings, rules, quotations, lists, tables and paragraphs.
  function render(markdown) {
    const lines = String(markdown).replace(/\r\n?/g, '\n').split('\n');
    const out = [], para = [];
    const flush = () => { if (para.length) { out.push('<p>' + inline(esc(para.join('\n'))) + '</p>'); para.length = 0; } };
    let i = 0, m;
    while (i < lines.length) {
      const line = lines[i];
      if ((m = /^\s*(`{3,}|~{3,})\s*([\w+#.-]*)\s*$/.exec(line))) {
        flush();
        const fence = m[1][0], language = m[2].toLowerCase(), code = []; i++;
        while (i < lines.length && !new RegExp('^\\s*' + (fence === '`' ? '`' : '~') + '{3,}\\s*$').test(lines[i])) code.push(lines[i++]);
        i++; out.push(codeBlock(code.join('\n'), language)); continue;
      }
      if ((m = /^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$/.exec(line))) { flush(); out.push('<h' + m[1].length + '>' + inline(esc(m[2])) + '</h' + m[1].length + '>'); i++; continue; }
      if (/^\s*([-*_])(\s*\1){2,}\s*$/.test(line)) { flush(); out.push('<hr>'); i++; continue; }
      if (/^\s*>/.test(line)) {
        flush(); const quoted = [];
        while (i < lines.length && /^\s*>/.test(lines[i])) quoted.push(lines[i++].replace(/^\s*>\s?/, ''));
        out.push('<blockquote>' + render(quoted.join('\n')) + '</blockquote>'); continue;
      }
      if (/^\s*([-*+]|\d+[.)])\s+/.test(line)) { flush(); const [html, next] = list(lines, i); out.push(html); i = next; continue; }
      if (/^\s*\|.*\|\s*$/.test(line) && i + 1 < lines.length && /^\s*\|?\s*:?-{3,}/.test(lines[i + 1])) { flush(); const [html, next] = table(lines, i); out.push(html); i = next; continue; }
      if (line.trim() === '') { flush(); i++; continue; }
      para.push(line); i++;
    }
    flush();
    return out.join('\n');
  }

  // The page's state: the messages rendered, in order, and the streamed reply.
  const rendered = [];
  let partialElement = null, partialText = '', renders = 0, applied = 0;
  function bind(element) {
    element.querySelectorAll('button[data-code]').forEach(button => button.onclick = () => {
      const code = button.closest('.code-block').querySelector('code').textContent;
      post({ action: 'code', what: button.dataset.code, code });
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

  function update(m) {
    const follow = !!m.reset || atEnd();
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
    if (follow) scroller().scrollTop = scroller().scrollHeight;
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
