using StatsDirect.UI;
using System.Diagnostics;

internal static partial class Program
{
    static async Task CheckScrollFollowing(frmMain main)
    {
        // Use a separate, never-started R window: controlled browser events must
        // not alter the sequence numbers of an actual native R session.
        var host = await NewConsole(main);
        await host.JS("""
            (()=>{
              window.StatsDirectRConsole.setMode('console');
              const descriptor=Object.getOwnPropertyDescriptor(HTMLImageElement.prototype,'src');
              const fixture=window.__scrollFollowing={descriptor,pending:[],generation:window.StatsDirectRConsole.snapshot().generation,seq:0,source:'preserved <- 42\n# Scroll fixture source\n'};
              Object.defineProperty(HTMLImageElement.prototype,'src',{...descriptor,set(value){
                if(typeof value==='string'&&value.startsWith('data:image/'))fixture.pending.push({image:this,value});
                else descriptor.set.call(this,value);
              }});
              fixture.send=event=>window.StatsDirectRConsole.receive({protocolVersion:1,sessionId:'scroll-fixture',generation:fixture.generation,seq:++fixture.seq,...event});
              fixture.output=Array.from({length:150},(_,i)=>'Scroll fixture line '+i).join('\n');
              fixture.addPlot=()=>fixture.send({type:'plot',plotId:'delayed-'+fixture.generation,revision:3,mime:'image/svg+xml',label:'Delayed SVG',preview:'data:image/svg+xml;base64,'+btoa('<svg xmlns="http://www.w3.org/2000/svg" width="800" height="1800"><rect width="800" height="1800" fill="lightblue"/></svg>')});
              fixture.begin=(plot=true,clear=true)=>{
                window.getSelection().removeAllRanges();document.dispatchEvent(new Event('selectionchange'));
                const input=document.getElementById('console-input'),pane=document.getElementById('transcript');
                input.value='';input.dispatchEvent(new Event('input',{bubbles:true}));
                if(clear){window.StatsDirectRConsole.clearOutput();pane.scrollTop=pane.scrollHeight;pane.dispatchEvent(new Event('scroll'))}
                fixture.generation++;fixture.seq=0;
                fixture.send({type:'session',script:fixture.source,capabilities:{openPlot:true,copyPlot:true}});
                fixture.send({type:'state',state:'ready'});
                fixture.send({type:'output',stream:'stdout',text:fixture.output});
                if(plot)fixture.addPlot();
              };
              fixture.release=index=>{const pending=fixture.pending[index];fixture.descriptor.set.call(pending.image,pending.value)};
              fixture.position=()=>document.getElementById('transcript').scrollTop;
              fixture.gap=()=>{const pane=document.getElementById('transcript');return pane.scrollHeight-pane.scrollTop-pane.clientHeight};
              fixture.begin();
            })()
            """);
        try
        {
            await host.TwoFrames();
            await FollowScrollEnd(host);
            int first = await PendingScrollImage(host);
            await host.JS("""
                (()=>{const input=document.getElementById('console-input');input.value=Array.from({length:30},(_,i)=>'Unsent draft '+i).join('\n');input.dispatchEvent(new Event('input',{bubbles:true}))})()
                """);
            await ReleaseScrollImage(host, first);
            Console.WriteLine("Delayed-image layout: " + await host.JS("(()=>{const p=document.getElementById('transcript'),i=document.getElementById('console-input'),s=window.StatsDirectRConsole.snapshot();return {mode:s.mode,hidden:document.getElementById('console-panel').hidden,gap:p.scrollHeight-p.scrollTop-p.clientHeight,top:p.scrollTop,height:p.scrollHeight,clientHeight:p.clientHeight,inputHeight:i.clientHeight,imageLoaded:document.images[0].complete&&document.images[0].naturalWidth>0}})()"));
            Check(await host.JS("window.__scrollFollowing.gap()<=2&&document.getElementById('console-input').clientHeight>=140") == "true",
                "a delayed chart and a growing multiline input keep a following reader at the final bottom");
            Check(await host.JS("""
                (()=>{const f=window.__scrollFollowing,s=window.StatsDirectRConsole.snapshot(),card=document.querySelector('figure.plot');return s.script===f.source&&s.history.length===0&&s.transcript.filter(e=>e.kind==='plot').length===1&&card.dataset.plotId==='delayed-'+f.generation&&card.dataset.revision==='3'&&[...card.querySelectorAll('button')].map(b=>b.textContent).join('|')==='Open chart|Copy chart'})()
                """) == "true" && Get(host.View, "Session") == null && host.Events.Length == 0,
                "scroll following preserves source, chart revision and native chart actions without starting or submitting R");

            await BeginScrollImage(host);
            int upward = await PendingScrollImage(host);
            await host.JS("(()=>{const f=window.__scrollFollowing,p=document.getElementById('transcript');p.scrollTop-=200;p.dispatchEvent(new Event('scroll'));f.before=f.position()})()");
            await ReleaseScrollImage(host, upward);
            Check(await host.JS("Math.abs(window.__scrollFollowing.position()-window.__scrollFollowing.before)<=2&&window.__scrollFollowing.gap()>200") == "true",
                "scrolling up before a delayed chart loads preserves the reader's chosen position");

            await BeginScrollImage(host);
            int selected = await PendingScrollImage(host);
            await host.JS("""
                (()=>{const f=window.__scrollFollowing,range=document.createRange(),node=document.querySelector('.output-entry').firstChild;range.setStart(node,0);range.setEnd(node,19);window.getSelection().addRange(range);document.dispatchEvent(new Event('selectionchange'));f.selected=window.getSelection().toString();f.before=f.position()})()
                """);
            await ReleaseScrollImage(host, selected);
            Check(await host.JS("window.getSelection().toString()===window.__scrollFollowing.selected&&Math.abs(window.__scrollFollowing.position()-window.__scrollFollowing.before)<=2") == "true",
                "a delayed chart neither replaces selected console text nor scrolls it away");

            await BeginScrollImage(host);
            int pressed = await PendingScrollImage(host);
            await host.JS("(()=>{const f=window.__scrollFollowing;document.querySelector('figure.plot').dispatchEvent(new PointerEvent('pointerdown',{bubbles:true,pointerId:71,pointerType:'mouse',button:0,buttons:1}));f.before=f.position()})()");
            await ReleaseScrollImage(host, pressed);
            Check(await host.JS("Math.abs(window.__scrollFollowing.position()-window.__scrollFollowing.before)<=2") == "true",
                "a held pointer prevents a delayed chart from moving the console underneath the user's press");
            await host.JS("document.dispatchEvent(new PointerEvent('pointerup',{bubbles:true,pointerId:71,pointerType:'mouse',button:0,buttons:0}))");

            await BeginScrollImage(host);
            int detached = await PendingScrollImage(host);
            await host.JS("window.__scrollFollowing.begin(false)");
            await host.TwoFrames();
            await host.JS("(()=>{const f=window.__scrollFollowing,p=document.getElementById('transcript');p.scrollTop=100;p.dispatchEvent(new Event('scroll'));f.before=f.position()})()");
            await ReleaseScrollImage(host, detached);
            Check(await host.JS($"!window.__scrollFollowing.pending[{detached}].image.isConnected&&Math.abs(window.__scrollFollowing.position()-window.__scrollFollowing.before)<=2&&document.querySelectorAll('figure.plot').length===0") == "true",
                "a detached earlier chart cannot scroll or restore a chart in a newer console generation");

            await host.JS("(()=>{const f=window.__scrollFollowing;window.StatsDirectRConsole.clearOutput();f.send({type:'output',stream:'stdout',text:f.output});f.addPlot()})()");
            await host.TwoFrames();
            await ReleaseScrollImage(host, await PendingScrollImage(host));
            Check(await host.JS("window.__scrollFollowing.gap()<=2") == "true",
                "clearing output while reading earlier lines restores following for the next decoded chart");

            await host.JS("(()=>{const p=document.getElementById('transcript');p.scrollTop=100;p.dispatchEvent(new Event('scroll'));window.__scrollFollowing.begin(true,false)})()");
            await host.TwoFrames();
            await ReleaseScrollImage(host, await PendingScrollImage(host));
            Check(await host.JS("window.__scrollFollowing.gap()<=2") == "true",
                "a new console generation starts following even when the previous generation was scrolled up");

            // Exercise the real Script Run -> Console transition, intercepting
            // only this unused renderer's host bridge so no fake source runs in R.
            await host.JS("""
                (()=>{
                  const f=window.__scrollFollowing,bridge=window.chrome.webview;
                  f.bridge=bridge;f.postMessage=bridge.postMessage;f.messages=[];bridge.postMessage=message=>f.messages.push(message);
                  window.StatsDirectRConsole.setMode('script');document.getElementById('run').click();
                  f.send({type:'state',state:'ready'});
                })()
                """);
            await host.TwoFrames();
            Check(await host.JS("(()=>{const f=window.__scrollFollowing;return window.StatsDirectRConsole.snapshot().mode==='console'&&f.messages.filter(e=>e.action==='submit').length===1&&f.messages.find(e=>e.action==='submit').source===f.source&&f.gap()<=2})()") == "true",
                "running Script returns to Console at the bottom without hidden-view layout cancelling following");

            await host.JS("""
                (()=>{const f=window.__scrollFollowing,p=document.getElementById('transcript');p.scrollTop=100;p.dispatchEvent(new Event('scroll'));f.before=f.position();window.StatsDirectRConsole.setMode('script');document.getElementById('run').click();f.send({type:'state',state:'ready'})})()
                """);
            await host.TwoFrames();
            Check(await host.JS("window.StatsDirectRConsole.snapshot().mode==='console'&&Math.abs(window.__scrollFollowing.position()-window.__scrollFollowing.before)<=2") == "true",
                "running Script preserves an earlier reading position when the console was deliberately scrolled up");
        }
        finally
        {
            await host.JS("(()=>{const f=window.__scrollFollowing;if(f){Object.defineProperty(HTMLImageElement.prototype,'src',f.descriptor);if(f.postMessage)f.bridge.postMessage=f.postMessage;document.dispatchEvent(new PointerEvent('pointercancel',{bubbles:true,pointerId:71}));window.getSelection().removeAllRanges();delete window.__scrollFollowing}})()");
        }
    }

    static async Task BeginScrollImage(ConsoleHost host)
    {
        await host.JS("window.__scrollFollowing.begin()");
        await host.TwoFrames();
        await FollowScrollEnd(host);
    }

    static async Task FollowScrollEnd(ConsoleHost host)
    {
        // Deliberately return to the end between independent reader scenarios.
        await host.JS("(()=>{const p=document.getElementById('transcript');p.scrollTop=0;p.dispatchEvent(new Event('scroll'));p.scrollTop=p.scrollHeight;p.dispatchEvent(new Event('scroll'))})()");
        await host.TwoFrames();
        if (await host.JS("window.__scrollFollowing.gap()<=2&&document.getElementById('transcript').clientHeight>0&&window.StatsDirectRConsole.snapshot().mode==='console'") != "true")
            throw new InvalidOperationException("The scroll fixture did not start at the bottom.");
    }

    static async Task<int> PendingScrollImage(ConsoleHost host)
    {
        int index = System.Text.Json.JsonSerializer.Deserialize<int>(await host.JS("window.__scrollFollowing.pending.length-1"));
        if (index < 0 || await host.JS($"window.__scrollFollowing.pending[{index}].image.isConnected&&!window.__scrollFollowing.pending[{index}].image.hasAttribute('src')") != "true")
            throw new InvalidOperationException("The scroll fixture did not hold a current, genuinely undecoded chart.");
        return index;
    }

    static async Task ReleaseScrollImage(ConsoleHost host, int index)
    {
        await host.JS($"window.__scrollFollowing.release({index})");
        var watch = Stopwatch.StartNew();
        while (await host.JS($"window.__scrollFollowing.pending[{index}].image.complete&&window.__scrollFollowing.pending[{index}].image.naturalWidth>0") != "true")
        {
            if (watch.ElapsedMilliseconds > 15000) throw new TimeoutException("The delayed scroll fixture's actual SVG did not decode.");
            await Task.Delay(30);
        }
        await host.TwoFrames();
    }
}
