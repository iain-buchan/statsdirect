using Microsoft.Web.WebView2.Core;
using StatsDirect.R;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Reflection;
using System.Text.Json.Nodes;

internal static partial class Program
{
    const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks;
    static readonly string root = Path.Combine(Path.GetTempPath(), "StatsDirect-learning-checks-" + Guid.NewGuid().ToString("N"));
    static readonly string artifacts = Environment.GetEnvironmentVariable("STATSDIRECT_LEARNING_ARTIFACTS") ?? root;
    static object Field(object o,string n)=>o.GetType().GetField(n,Members)!.GetValue(o)!;
    static void Check(bool yes,string label){if(!yes)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static async Task Wait(Func<bool> yes,string label,int ms=15000){var until=DateTime.UtcNow.AddMilliseconds(ms);while(!yes()&&DateTime.UtcNow<until)await Task.Delay(30);Check(yes(),label);}
    static async Task<JsonNode> JS(LearningView view,string code)=>JsonNode.Parse(await view.Browser.CoreWebView2.ExecuteScriptAsync(code));
    [STAThread] static int Main()
    {
        ApplicationConfiguration.Initialize();Directory.CreateDirectory(root);Directory.CreateDirectory(artifacts);
        var assembly=typeof(frmMain).Assembly;
        var license=assembly.GetCustomAttributes().Single(a=>a.GetType().Name=="SpreadsheetGearLicenseAttribute");
        SpreadsheetGear.Factory.SetSignedLicense((string)license.GetType().GetProperty("LicenseString")!.GetValue(license)!);
        var appType=assembly.GetType("StatsDirect.UI.SdApplication")!;
        var app=appType.GetProperty("SoleInstance",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        using var main=new frmMain();appType.GetProperty("MainWindow",Members)!.SetValue(app,main);
        foreach(var (evt,method) in new[]{("Load","frmMain_Load"),("Shown","frmMain_Shown"),("FormClosing","frmMain_FormClosing"),("FormClosed","frmMain_FormClosed")})
        {var e=typeof(frmMain).GetEvent(evt)!;e.RemoveEventHandler(main,typeof(frmMain).GetMethod(method,Members)!.CreateDelegate(e.EventHandlerType!,main));}
        main.StartPosition=FormStartPosition.Manual;main.Location=new(-16000,-16000);main.Size=new(1500,1000);main.ShowInTaskbar=false;
        int exit=1;
        main.Shown+=async(_,_)=>{try{await Run(main);Console.WriteLine($"PASS {checks} Windows Learning/AI/R setup checks");exit=0;}catch(Exception ex){Console.WriteLine("FAIL "+ex);}finally{foreach(var child in main.MdiChildren)child.Dispose();main.Close();}};
        Application.Run(main);return exit;
    }
    static async Task Run(frmMain main)
    {
        main.EnsureStartupDocument();await Task.Delay(100);
        Check(main.MdiChildren.Length==1&&main.ActiveMdiChild is frmSpreadsheetGear,"ordinary startup opens one blank worksheet without a chooser");
        main.EnsureStartupDocument();Check(main.MdiChildren.Length==1,"startup preserves an already-open document");
        var grid=(frmSpreadsheetGear)main.ActiveMdiChild;
        var node=Environment.GetEnvironmentVariable("STATSDIRECT_TEST_NODE")??throw new Exception("Set STATSDIRECT_TEST_NODE to an absolute Node executable path.");
        using var tutor=new ChatGPTTutor(node,Path.Combine(root,"mock-account"),Path.Combine(AppContext.BaseDirectory,"Tutor"),Path.Combine(AppContext.BaseDirectory,"mock-server.mjs"));
        using var service=new LearningService(main,Path.Combine(root,"Learning"),tutor);
        typeof(frmMain).GetField("learning",Members)!.SetValue(main,service);
        var menu=(MenuStrip)Field(main,"mnuMain");var help=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text.Replace("&","")=="Help");
        Check(menu.Items.OfType<ToolStripMenuItem>().TakeLast(2).Select(i=>i.Text.Replace("&","")).SequenceEqual(new[]{"Window","Help"}),"main menu ends with Window then Help");
        var helpItems=help.DropDownItems;int exampleIndex=helpItems.IndexOfKey("exampleWorkbookMenuItem");
        Check(exampleIndex==helpItems.Count-1&&helpItems[exampleIndex-1] is ToolStripSeparator&&helpItems.IndexOfKey("checkForUpdatesToolStripMenuItem")<exampleIndex&&helpItems.IndexOfKey("aboutsStatsDirectToolStripMenuItem")<exampleIndex,"Help ends with Example workbook in its own separated section");
        Check(!((ToolStripMenuItem)Field(main,"toolsToolStripMenuItem")).DropDownItems.ContainsKey("checkForUpdatesToolStripMenuItem"),"update checking is moved to Help without a duplicate in Tools");
        string examplePath=Path.Combine(AppContext.BaseDirectory,"Data","test.xlsx");
        byte[] exampleBytes=File.ReadAllBytes(examplePath);
        help.DropDownItems["exampleWorkbookMenuItem"]!.PerformClick();
        await Wait(()=>main.ActiveMdiChild is frmSpreadsheetGear example&&example!=grid,"Help > Example workbook opens a separate data document");
        var example=(frmSpreadsheetGear)main.ActiveMdiChild;
        Check(example.WindowInformation.IsNew&&example.Path==null&&example.Text=="Example workbook","example opens as an unsaved working copy so Save uses Save As");
        var exampleView=(SpreadsheetGear.Windows.Forms.WorkbookView)Field(example,"workbookView");
        exampleView.GetLock();
        try
        {
            Check(exampleView.ActiveWorkbook.Worksheets.Count>1,"example workbook retains its supplied worksheets");
            exampleView.ActiveWorksheet.Cells[0,0].Value="Edited in a working copy";
            Check((string)exampleView.ActiveWorksheet.Cells[0,0].Value=="Edited in a working copy","example workbook is editable");
        }
        finally{exampleView.ReleaseLock();}
        Check(File.ReadAllBytes(examplePath).SequenceEqual(exampleBytes),"editing the example leaves the installed workbook unchanged");
        typeof(StatsDirectForm).GetProperty("Dirty",Members)!.SetValue(example,false);example.Close();grid.Activate();
        help.DropDownItems["learningMenuItem"]!.PerformClick();
        await Wait(()=>service.View!=null,"Help > Learning dispatch opens the learning document");var view=service.View;
        await view.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40));await Task.Delay(250);
        Check((await JS(view,"document.querySelectorAll('[data-lesson]').length")).GetValue<int>()==12,"all twelve shared lessons render in Windows WebView2");
        Check(File.Exists(Path.Combine(service.Storage,"portfolio.json")),"initial learning record is saved locally");
        help.DropDownItems["learningOptionsMenuItem"]!.PerformClick();await Task.Delay(150);
        Check((await JS(view,"Boolean(document.getElementById('statisticalSkills'))")).GetValue<bool>(),"Help > Learning Options selects the settings view");
        await JS(view,"document.getElementById('statisticalSkills').value='advanced';document.getElementById('statisticalSkills').dispatchEvent(new Event('input'));document.getElementById('courseTitle').value='Local assessment course';document.getElementById('courseTitle').dispatchEvent(new Event('input'));document.getElementById('assessmentType').value='self';document.getElementById('assessmentType').dispatchEvent(new Event('change'));");
        await Wait(()=>service.State?["learning"].S("statisticalSkills")=="advanced"&&service.State?["training"].S("courseTitle")=="Local assessment course","learner and provider preferences persist as edited");
        Check(service.State["training"].S("assessmentType")=="self","assessment preference is preserved");
        using(var shot=File.Create(Path.Combine(artifacts,"learning-options.png")))await view.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
        await TestCourses(service,view);
        await TestTutor(tutor);
        await TestConsent(main,service,view,grid);
        await TestCalculations(main,grid);
        await TestExports(service,view);
        await TestRSetup(main);
        view.Close();await Task.Delay(80);service.Show(true);await service.View.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40));await Task.Delay(100);
        Check((await JS(service.View,"document.getElementById('statisticalSkills').value")).GetValue<string>()=="advanced","reopening Learning restores learner settings from disk");
        service.View.Close();await Task.Delay(80);
        string portfolio=Path.Combine(service.Storage,"portfolio.json"),valid=File.ReadAllText(portfolio);
        var damaged=JsonNode.Parse(valid);damaged["identity"]=null;string invalid=damaged.ToJsonString();File.WriteAllText(portfolio,invalid);
        try
        {
            service.Show(true);await Reject(()=>service.View.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20)),"malformed nested portfolio is rejected by the shared validator before options open");
            Check(File.ReadAllText(portfolio)==invalid,"failed restore preserves the original portfolio bytes");
        }
        finally{service.View.Close();File.WriteAllText(portfolio,valid);}
    }
}
