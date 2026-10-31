using System.Text.Json;

internal static partial class Program
{
    static async Task CheckDomAndWheel(ConsoleHost host)
    {
        try
        {
            await host.JS("""
                (()=>{
                  const buttons=[...document.querySelector('.plot').querySelectorAll('.plot-actions button')];
                  const open=buttons.find(b=>b.textContent.startsWith('Open')),copy=buttons.find(b=>b.textContent==='Copy chart');
                  if(!open||!copy)throw new Error('Original plot actions were not found');
                  const bridge=window.chrome.webview,original=bridge.postMessage;
                  const state=window.__plotInteraction={open,copy,original,actions:[]};
                  bridge.postMessage=message=>{
                    if(['openPlot','copyPlot'].includes(message.action))state.actions.push(message);
                    else original.call(bridge,message);
                  };
                  open.focus();window.getSelection().removeAllRanges();document.dispatchEvent(new Event('selectionchange'));
                })()
                """);
            await host.TwoFrames();
            Check(await host.JS("window.__plotInteraction.open.isConnected&&window.__plotInteraction.copy.isConnected") == "true",
                "focusing a chart action retains the original Open and Copy DOM controls");
            await host.JS("""
                (()=>{
                  const r=window.__plotInteraction;r.open.click();
                  r.copy.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true,pointerId:17,pointerType:'mouse',isPrimary:true,button:0,buttons:1}));
                  r.copy.dispatchEvent(new MouseEvent('mousedown',{bubbles:true,button:0,buttons:1}));
                  r.copy.focus();window.getSelection().removeAllRanges();document.dispatchEvent(new Event('selectionchange'));
                })()
                """);
            Call(host.Session, "PublishState");
            Call(host.Session, "PublishNotice", "A host update arrived during a chart pointer press.");
            await host.TwoFrames();
            Check(await host.JS("window.__plotInteraction.copy.isConnected") == "true", "host output and state updates preserve a chart button while a pointer press is held");
            await host.JS("""
                (()=>{
                  const r=window.__plotInteraction;
                  r.copy.dispatchEvent(new PointerEvent('pointerup',{bubbles:true,pointerId:17,pointerType:'mouse',isPrimary:true,button:0,buttons:0}));
                  r.copy.dispatchEvent(new MouseEvent('mouseup',{bubbles:true,button:0,buttons:0}));r.copy.click();
                })()
                """);
            await host.TwoFrames();
            var actions = JsonSerializer.Deserialize<JsonElement>(await host.JS("window.__plotInteraction.actions"));
            var first = host.Events.First(e => Kind(e) == "plot");
            Check(actions.GetArrayLength() == 2 && Text(actions[0], "action") == "openPlot" && Text(actions[1], "action") == "copyPlot"
                && actions.EnumerateArray().All(e => Text(e, "plotId") == Text(first, "plotId") && Number(e, "revision") == Number(first, "revision")),
                "the retained chart controls dispatch their exact original plot revision after pointer release");
        }
        finally
        {
            await host.JS("(()=>{const r=window.__plotInteraction;if(r?.original)window.chrome.webview.postMessage=r.original;delete window.__plotInteraction})()");
        }

        await host.Run("writeLines(sprintf('wheel line %03d',1:150))", "wheel line 150");
        var wheel = JsonSerializer.Deserialize<JsonElement>(await host.JS("""
            (()=>{
              const input=document.getElementById('console-input'),pane=document.getElementById('transcript'),row=input.parentElement,label=document.getElementById('prompt'),api=window.StatsDirectRConsole;
              api.setMode('console');
              const original={value:input.value,start:input.selectionStart,end:input.selectionEnd,top:input.scrollTop,focus:document.activeElement};
              const script=api.snapshot().script,history=JSON.stringify(api.snapshot().history);
              const close=(a,b)=>Math.abs(a-b)<1;
              const send=(target,fields)=>{pane.scrollTop=200;const before=pane.scrollTop,event=new WheelEvent('wheel',{bubbles:true,cancelable:true,...fields});target.dispatchEvent(event);return {prevented:event.defaultPrevented,delta:pane.scrollTop-before}};
              try{
                input.value='Unsent wheel draft';input.dispatchEvent(new Event('input',{bubbles:true}));input.focus();input.setSelectionRange(2,7);
                const lineHeight=parseFloat(getComputedStyle(input).lineHeight),pageHeight=pane.clientHeight;
                if(pane.scrollHeight-pane.clientHeight<pageHeight+400)throw new Error('Transcript is too short for wheel regression');
                const pixel=send(row,{deltaY:40,deltaMode:0}),line=send(label,{deltaY:3,deltaMode:1}),page=send(row,{deltaY:1,deltaMode:2}),up=send(label,{deltaY:-30});
                const shortInput=send(input,{deltaY:40}),ignored=[send(row,{deltaY:40,ctrlKey:true}),send(label,{deltaY:40,metaKey:true}),send(row,{deltaY:40,shiftKey:true}),send(row,{deltaX:60,deltaY:0}),send(label,{deltaX:80,deltaY:15})];
                const shortPreserved=input.value==='Unsent wheel draft'&&input.selectionStart===2&&input.selectionEnd===7&&document.activeElement===input;
                const long=Array.from({length:60},(_,i)=>'Long line '+i).join('\n');input.value=long;input.dispatchEvent(new Event('input',{bubbles:true}));input.setSelectionRange(3,8);
                const limit=input.scrollHeight-input.clientHeight;if(limit<100)throw new Error('Input did not become scrollable');
                input.scrollTop=limit/2;const nativeDown=send(input,{deltaY:40}),nativeUp=send(input,{deltaY:-40});
                input.scrollTop=0;const top=send(input,{deltaY:-40});input.scrollTop=limit;const bottom=send(input,{deltaY:40});
                return {
                  forwarded:pixel.prevented&&up.prevented&&shortInput.prevented&&close(up.delta,-30),
                  normalized:close(pixel.delta,40)&&line.prevented&&close(line.delta,3*lineHeight)&&page.prevented&&close(page.delta,pageHeight),
                  modifiers:ignored.every(e=>!e.prevented&&e.delta===0),
                  native:!nativeDown.prevented&&nativeDown.delta===0&&!nativeUp.prevented&&nativeUp.delta===0,
                  boundary:top.prevented&&close(top.delta,-40)&&bottom.prevented&&close(bottom.delta,40),
                  preserved:shortPreserved&&input.value===long&&input.selectionStart===3&&input.selectionEnd===8&&document.activeElement===input&&api.snapshot().script===script&&JSON.stringify(api.snapshot().history)===history
                };
              }finally{input.value=original.value;input.dispatchEvent(new Event('input',{bubbles:true}));input.setSelectionRange(original.start,original.end);input.scrollTop=original.top;if(original.focus?.isConnected)original.focus.focus({preventScroll:true})}
            })()
            """));
        Check(wheel.GetProperty("forwarded").GetBoolean(), "wheel over the prompt row, label and short input scrolls console history");
        Check(wheel.GetProperty("normalized").GetBoolean(), "prompt wheel normalizes pixel, line and page scrolling");
        Check(wheel.GetProperty("modifiers").GetBoolean(), "Ctrl, Meta, Shift and horizontal wheel gestures remain unprevented");
        Check(wheel.GetProperty("native").GetBoolean() && wheel.GetProperty("boundary").GetBoolean(), "multiline input keeps native scrolling until its directional boundary, then scrolls history");
        Check(wheel.GetProperty("preserved").GetBoolean(), "prompt scrolling preserves draft text, caret, focus, script and command history");
        await CheckBrowserEditing(host);
    }

    static async Task CheckBrowserEditing(ConsoleHost host)
    {
        await host.JS("""
            (()=>{
              window.StatsDirectRConsole.setMode('script');
              window.StatsDirectRHost.setScript('alpha beta alpha');
              const s=document.getElementById('script-source');s.focus();s.setSelectionRange(6,10);s.dispatchEvent(new Event('select',{bubbles:true}));
              window.StatsDirectRHost.refresh();
            })()
            """);
        // A native menu owns focus while dispatching its edit command. The web
        // editor must still remember the textarea, selection and undo target.
        host.Document.MdiParent!.MainMenuStrip!.Focus();
        Check(await host.JS("window.StatsDirectRHost.edit('paste','replacement')") == "true"
            && await host.JS("document.getElementById('script-source').value==='alpha replacement alpha'") == "true",
            "a native-menu paste targets the retained Script selection after the menu takes focus");
        Check(await host.JS("window.StatsDirectRHost.edit('undo')") == "true"
            && await host.JS("document.getElementById('script-source').value==='alpha beta alpha'") == "true",
            "Script paste remains one browser undo step");
        Check(await host.JS("window.StatsDirectRHost.edit('redo')") == "true"
            && await host.JS("document.getElementById('script-source').value==='alpha replacement alpha'") == "true",
            "Script paste supports redo through the same retained editor target");
        await host.JS("window.StatsDirectRHost.setScript('alpha beta alpha');document.getElementById('script-source').focus()");
        var replaced = JsonSerializer.Deserialize<int>(await host.JS("window.StatsDirectRHost.replaceAll('alpha','gamma',false)"));
        Check(replaced == 2 && await host.JS("document.getElementById('script-source').value==='gamma beta gamma'") == "true", "Replace All changes each matching Script occurrence");
        Check(await host.JS("window.StatsDirectRHost.edit('undo')") == "true"
            && await host.JS("document.getElementById('script-source').value==='alpha beta alpha'") == "true",
            "Replace All is a single undoable browser edit");
        Check(await host.JS("window.StatsDirectRHost.findNext('beta',false)") == "true"
            && await host.JS("window.StatsDirectRHost.selectedText()==='beta'") == "true",
            "Find selects the intended Script text");
        host.Document.MdiParent!.MainMenuStrip!.Focus();
        Check(JsonSerializer.Deserialize<string>(await host.JS("window.StatsDirectRHost.edit('copy')")) == "beta",
            "Copy reads the retained selection after native menu focus without changing the system clipboard");
        Check(await host.JS("window.StatsDirectRHost.edit('cut')") == "true"
            && await host.JS("document.getElementById('script-source').value==='alpha  alpha'") == "true",
            "Cut edits the retained Script selection");
        Check(await host.JS("window.StatsDirectRHost.edit('undo')") == "true"
            && await host.JS("document.getElementById('script-source').value==='alpha beta alpha'") == "true",
            "Script Cut remains undoable after native menu focus");
    }
}
