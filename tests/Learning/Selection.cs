using StatsDirect.UI.Learning;

internal static partial class Program
{
    static async Task TestLearningSelection(LearningService service, LearningView view)
    {
        var original=service.State.DeepClone();
        try
        {
            view.Activate();
            var state=original.DeepClone();state["view"]="study";state["lesson"]="precision";
            await view.CallAsync("restore",state);view.SourceId="__none__";service.RefreshWorkspace();
            await JS(view,"document.getElementById('question').value='COPY_SELECTION_CHECK';document.getElementById('send').click()");
            await Wait(()=>Field(service,"requestId") is string,"learning selection fixture starts a synthetic streamed reply");
            string id=(string)Field(service,"requestId");
            async Task Stream(string value){await view.CallAsync("partial",new System.Text.Json.Nodes.JsonObject{["id"]=id,["text"]=value});await Task.Delay(150);}
            async Task<string> Selected()=> (await JS(view,"getSelection().toString()")).GetValue<string>();
            string text="Previous paragraph.\n\nSelected paragraph continues";
            await Stream(text);
            await JS(view,"(()=>{const p=document.querySelectorAll('#streamedReply .message-body p')[1];getSelection().setBaseAndExtent(p.firstChild,0,p.firstChild,18)})()");
            Check((await JS(view,"getSelection().toString()")).GetValue<string>()=="Selected paragraph","Learning paragraph-start selection has exact initial text");
            Check(await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2)=="Selected paragraph","Learning copies the initial selection without paragraph breaks");
            text+=" while streaming.";
            await Stream(text);
            string selected=(await JS(view,"getSelection().toString()")).GetValue<string>();
            Check(selected=="Selected paragraph","Learning paragraph-start selection retains exact copy text: "+System.Text.Json.JsonSerializer.Serialize(selected));
            Check(await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2)==selected,"Learning native copy retains exact text after a streamed replacement");
            await JS(view,"(()=>{const s=getSelection(),a=s.anchorNode,ao=s.anchorOffset,f=s.focusNode,fo=s.focusOffset;s.setBaseAndExtent(f,fo,a,ao)})()");
            text+=" More text.";await Stream(text);
            Check(await Selected()=="Selected paragraph"&&(await JS(view,"getSelection().anchorOffset>getSelection().focusOffset")).GetValue<bool>(),"Learning preserves backwards selection at a paragraph start");
            string backwardsCopy=await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2);
            string copyDetail=backwardsCopy.Replace("\n","").Trim()=="Selected paragraph"?System.Text.Json.JsonSerializer.Serialize(backwardsCopy):"unexpected text length "+backwardsCopy.Length;
            Check(backwardsCopy=="Selected paragraph","Learning copies a backwards selection without leading newlines: "+copyDetail);
            // Selecting to the end of a paragraph must retain the other side of the boundary too.
            await JS(view,"(()=>{const n=document.querySelector('#streamedReply p').firstChild;getSelection().setBaseAndExtent(n,9,n,n.length)})()");
            string endText=await Selected();text+=" Extra.";await Stream(text);
            Check(await Selected()==endText&&await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2)==endText,"Learning preserves paragraph-end selection and copying");
            text="Previous paragraph.\n\n**Selected** paragraph continues";await Stream(text);
            await JS(view,"(()=>{const p=document.querySelectorAll('#streamedReply p')[1];getSelection().setBaseAndExtent(p.querySelector('strong').firstChild,0,p.lastChild,10)})()");
            text+=" with mixed formatting.";await Stream(text);
            Check(await Selected()=="Selected paragraph"&&await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2)=="Selected paragraph","Learning preserves selection and native copy across bold and plain text");
            await JS(view,"(()=>{const p=document.querySelectorAll('#streamedReply p');getSelection().setBaseAndExtent(p[0].firstChild,9,p[1].lastChild,10)})()");
            string spanning=await Selected(),copiedSpanning=await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2);text+=" Again.";await Stream(text);
            Check(await Selected()==spanning&&await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2)==copiedSpanning,"Learning retains deliberate paragraph breaks in a selection spanning paragraphs");
            await JS(view,"getSelection().removeAllRanges()");
            text=string.Join("\n\n",Enumerable.Range(1,30).Select(i=>$"Paragraph {i} stable text."));await Stream(text);
            await JS(view,"(()=>{const box=document.getElementById('messages');box.scrollTop=box.scrollHeight;const n=document.querySelector('#streamedReply p:last-child').firstChild;getSelection().setBaseAndExtent(n,0,n,12)})()");
            double top=(await JS(view,"document.getElementById('messages').scrollTop")).GetValue<double>();
            string bottomText=await Selected();text+="\n\nLater paragraph below the selected text.";await Stream(text);
            Check(await Selected()==bottomText&&Math.Abs((await JS(view,"document.getElementById('messages').scrollTop")).GetValue<double>()-top)<1,"Learning selection at the bottom pauses following while rendering continues");
            await view.CallAsync("reply",new System.Text.Json.Nodes.JsonObject{["id"]=id,["text"]=text,["model"]="selection-check"});await Task.Delay(150);
            double maximum=(await JS(view,"document.getElementById('messages').scrollHeight-document.getElementById('messages').clientHeight")).GetValue<double>();
            Check(await Selected()==bottomText&&Math.Abs((await JS(view,"document.getElementById('messages').scrollTop")).GetValue<double>()-Math.Min(top,maximum))<1,"Learning completion preserves selected text and reading position, allowing viewport clamping");
            Check(await BrowserCopyProbe.CopyAsync(view.Browser.CoreWebView2)==bottomText,"Learning native copy remains exact after completion replaces the conversation DOM");
        }
        finally
        {
            service.CancelQuestion();await Task.Delay(150);
            await view.CallAsync("restore",original);
        }
    }
}
