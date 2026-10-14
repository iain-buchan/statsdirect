using SpreadsheetGear;
using StatsDirect.R;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using StatsDirect.UI.WebReports;
using System.Diagnostics;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task TestFileInterop(frmMain main, frmSpreadsheetGear originalGrid)
    {
        string folder = Path.Combine(root, "files"); Directory.CreateDirectory(folder);
        string script = Path.Combine(folder,"Example.R"); File.WriteAllText(script,"before <- c(1, 2, 3)\nprint(before)\n");
        Check(main.OpenFile(script,false) && main.ActiveMdiChild is frmScript openedR && openedR.LearningScript.Contains("before <-"), "File Open dispatches uppercase .R to the script editor without running it");
        CloseTestDocument((StatsDirectForm)main.ActiveMdiChild);
        string csv = Path.Combine(folder,"Example.CSV");
        File.WriteAllText(csv,"ID,Number,Text,Blank\r\n0012,12.25,\"one, two\",\r\n0001,-2,\"line1\r\nline2\",\r\n3,1e3,=1+2,\r\n");
        Check(main.OpenFile(csv,false) && main.ActiveMdiChild is frmSpreadsheetGear, "File Open dispatches uppercase CSV to a worksheet");
        var grid = (frmSpreadsheetGear)main.ActiveMdiChild;
        Check(grid.Path == null && grid.WindowInformation.IsNew, "imported CSV is an unsaved workbook so Save cannot write Excel bytes over the CSV");
        var view = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid,"workbookView"); view.GetLock();
        try
        {
            var s = view.ActiveWorksheet;
            Check((string)s.Cells[1,0].Value=="0012" && (double)s.Cells[1,1].Value==12.25 && (double)s.Cells[3,1].Value==1000,"CSV preserves leading-zero IDs while recognizing ordinary numeric data");
            Check((string)s.Cells[1,2].Value=="one, two" && ((string)s.Cells[2,2].Value).Contains("line1\r\nline2") && s.Cells[1,3].Value==null,"CSV reads quoted delimiters, embedded newlines and missing cells");
            Check((string)s.Cells[3,2].Value=="=1+2" && !s.Cells[3,2].HasFormula,"CSV formula-like text remains literal and unexecuted");
        }
        finally { view.ReleaseLock(); }
        CloseTestDocument(grid);
        var set = Factory.GetWorkbookSet();
        foreach (var (extension,delimiter) in new[]{("tsv","\t"),("tab","\t"),("csv",";")})
        {
            string file=Path.Combine(folder,"delimited."+extension);File.WriteAllText(file,"x"+delimiter+"y\n4"+delimiter+"5\n");
            var book=SpreadsheetFiles.Open(set,file);Check((double)book.Worksheets[0].Cells[1,1].Value==5,"imports "+extension+" delimiter correctly");book.Close();
        }
        string tall=Path.Combine(folder,"excel-limit.csv");
        using(var writer=new StreamWriter(tall)){writer.WriteLine("Value");for(int row=1;row<1048576;row++)writer.WriteLine(row);}
        var tallBook=SpreadsheetFiles.Open(set,tall);
        Check((double)tallBook.Worksheets[0].Cells[1048575,0].Value==1048575,"CSV opens the full Excel limit of 1,048,576 rows without truncation");tallBook.Close();
        File.AppendAllText(tall,"1048576\n");
        await Reject(()=>Task.Run(()=>SpreadsheetFiles.Open(Factory.GetWorkbookSet(),tall)),"CSV beyond Excel's row limit fails explicitly instead of truncating");
        string identifiers=Path.Combine(folder,"identifiers.csv");File.WriteAllText(identifiers,"ID\n1234567890123456789\n");
        var idBook=SpreadsheetFiles.Open(set,identifiers);Check((string)idBook.Worksheets[0].Cells[1,0].Value=="1234567890123456789","CSV preserves long numeric identifiers beyond Excel's numeric precision");idBook.Close();
        string source = Path.Combine(folder,"fixture.xlsx");var sample=set.Workbooks.Add();sample.Worksheets[0].Cells[0,0].Value="Data";sample.Worksheets[0].Cells[1,0].Value=2;
        foreach(var (extension,format) in new[]{("xlsx",FileFormat.OpenXMLWorkbook),("xlsm",FileFormat.OpenXMLWorkbookMacroEnabled),("xls",FileFormat.Excel8)})
        {
            string file=Path.Combine(folder,"workbook."+extension);sample.SaveAs(file,format);var book=SpreadsheetFiles.Open(set,file);
            Check((double)book.Worksheets[0].Cells[1,0].Value==2,"opens "+extension+" workbook");book.Close();
        }
        sample.SaveAs(source,FileFormat.OpenXMLWorkbook);sample.Close();
        string xlsb=Environment.GetEnvironmentVariable("STATSDIRECT_TEST_XLSB");
        if (!string.IsNullOrEmpty(xlsb))
        {
            var binary=SpreadsheetFiles.Open(set,xlsb);
            Check((string)binary.Worksheets[0].Cells[1,0].Value=="0012"&&(double)binary.Worksheets[0].Cells[1,1].Value==12.25,"imports XLSB written by installed Excel while preserving text IDs and numeric values");binary.Close();
        }
        string template=Path.Combine(folder,"template.xltx");File.Copy(source,template);
        Check(main.OpenFile(template,false) && ((frmSpreadsheetGear)main.ActiveMdiChild).Path==null,"Excel template opens as an unsaved working copy");CloseTestDocument((StatsDirectForm)main.ActiveMdiChild);

        var report=(frmReportWebView)main.CreateReport();await report.LearningReport.Ready;
        string fixtures = Path.Combine(AppContext.BaseDirectory,"Fixtures","Reports");
        foreach(string file in new[]{"layout.rtf","legacy-charts.rtf","standard-wmf.rtf","raster-picture.rtf","binary-picture.rtf","damaged-picture.rtf"})
        {
            var legacy=LegacyReport.Read(Path.Combine(fixtures,file));
            await report.LearningReport.OpenLegacyAsync(legacy,file);
            var dom=JsonNode.Parse(await report.LearningReport.Browser.CoreWebView2.ExecuteScriptAsync("({text:document.getElementById('results').innerText,svg:document.querySelectorAll('#results svg').length,img:document.querySelectorAll('#results img').length,tables:document.querySelectorAll('#results table').length,unsafe:document.querySelectorAll('#results script,#results iframe').length})"));
            Check(dom.I("unsafe")==0 && !dom.S("text").Contains("STATSDIRECT_PICTURE_"),"RTF "+file+" imports through sanitizer without unresolved image markers");
            if(file=="layout.rtf")Check(dom.I("tables")>0,"RTF import retains report tables");
            if(file is "legacy-charts.rtf" or "standard-wmf.rtf" or "binary-picture.rtf")Check(dom.I("svg")>0,"RTF "+file+" converts its legacy chart to SVG");
            if(file=="raster-picture.rtf")Check(dom.I("img")>0,"RTF raster picture survives import");
            if(file=="damaged-picture.rtf")Check(dom.S("text").Contains("could not be converted"),"damaged RTF chart retains readable report with explicit placeholder");
        }
        string malformed=Path.Combine(folder,"broken.rtf");File.WriteAllText(malformed,"{\\rtf1 broken");
        await Reject(()=>Task.Run(()=>LegacyReport.Read(malformed)),"unbalanced RTF is rejected before conversion");
        CloseTestDocument(report);

        string r = RDataFiles.Executable();
        RunRFile(r,folder,"create.R","""
            args <- commandArgs(TRUE); setwd(args[1])
            x <- data.frame(number=c(1.25,NA_real_,NaN,Inf,-Inf), integer=c(1L,NA_integer_,3L,4L,5L),
              logical=c(TRUE,FALSE,NA,TRUE,FALSE), text=c('',NA,'0012','a\r\nb%25','\ufeffunicode'),
              factor=ordered(c('a',NA,'b','a','b'),levels=c('b','a','unused')),
              bom=c('\ufefffirst','second','third','fourth','fifth'),
              date=as.Date(c(1,NA,3.5,Inf,NaN),origin='1970-01-01'),
              time=as.POSIXct(c(100.123456789,NA,200,-Inf,NaN),origin='1970-01-01',tz='Europe/London'),check.names=FALSE)
            saveRDS(x,'source.rds'); y <- matrix(1:6,ncol=2); colnames(y)<-c('Left','Right');save(x,y,file='source.RData')
            named<-x;row.names(named)<-letters[1:5];saveRDS(named,'named.rds')
            """);
        var rbook=RDataFiles.Read(set,Path.Combine(folder,"source.rds"),r,out var meta);
        Check(rbook.Worksheets[0].Cells[1,3].Value is string empty && empty=="" && rbook.Worksheets[0].Cells[2,3].Value==null,"R character empty string remains distinct from NA in the grid");
        RDataFiles.Write(rbook,rbook.Worksheets[0],Path.Combine(folder,"roundtrip.rds"),meta,r);
        RunRFile(r,folder,"verify.R","args<-commandArgs(TRUE);setwd(args[1]);stopifnot(identical(readRDS('source.rds'),readRDS('roundtrip.rds')))");
        Check(true,"base R confirms exact RDS roundtrip of numbers, NA/NaN/Inf, integer, logical, strings, ordered factors, dates and timestamp precision");
        rbook.Worksheets[0].Cells[1,0].Value=8.25;rbook.Worksheets[0].Cells[1,4].Value="new level";
        RDataFiles.Write(rbook,rbook.Worksheets[0],Path.Combine(folder,"edited.rds"),meta,r);
        RunRFile(r,folder,"edited.R","args<-commandArgs(TRUE);setwd(args[1]);x<-readRDS('edited.rds');y<-readRDS('source.rds');stopifnot(x$number[1]==8.25,as.character(x$factor[1])=='new level',is.ordered(x$factor),identical(x$time,y$time),identical(x$date,y$date))");
        Check(true,"edited R data preserves column types, factor ordering and unrelated precise timestamps");rbook.Close();
        rbook=RDataFiles.Read(set,Path.Combine(folder,"source.RData"),r,out meta);
        Check(rbook.Worksheets.Count==2,"RData loads multiple named tables as worksheets");
        RDataFiles.Write(rbook,rbook.Worksheets[0],Path.Combine(folder,"roundtrip.RData"),meta,r);
        RunRFile(r,folder,"multi.R","args<-commandArgs(TRUE);setwd(args[1]);a<-new.env();b<-new.env();load('source.RData',a);load('roundtrip.RData',b);stopifnot(identical(a$x,b$x),identical(a$y,b$y))");
        Check(true,"base R confirms named RData data-frame and matrix roundtrip");rbook.Close();
        rbook=RDataFiles.Read(set,Path.Combine(folder,"named.rds"),r,out meta);
        RDataFiles.Write(rbook,rbook.Worksheets[0],Path.Combine(folder,"named-copy.rds"),meta,r);
        RunRFile(r,folder,"names.R","args<-commandArgs(TRUE);setwd(args[1]);stopifnot(identical(readRDS('named.rds'),readRDS('named-copy.rds')))");
        Check(true,"unchanged R data retains explicit row names");
        rbook.Worksheets[0].Cells[1,0].Value=9;
        byte[] before=File.ReadAllBytes(Path.Combine(folder,"named-copy.rds"));
        await Reject(()=>Task.Run(()=>RDataFiles.Write(rbook,rbook.Worksheets[0],Path.Combine(folder,"named-copy.rds"),meta,r)),"ambiguous edited row-name mapping refuses lossy R overwrite");
        Check(File.ReadAllBytes(Path.Combine(folder,"named-copy.rds")).SequenceEqual(before),"failed R export preserves destination bytes");rbook.Close();
        string openRds=Path.Combine(folder,"opened.rds");File.Copy(Path.Combine(folder,"source.rds"),openRds);
        Check(main.OpenFile(openRds,false)&&main.ActiveMdiChild is frmSpreadsheetGear,"File Open dispatches RDS to its typed worksheet importer");
        var openedGrid=(frmSpreadsheetGear)main.ActiveMdiChild;
        var openedView=(SpreadsheetGear.Windows.Forms.WorkbookView)Field(openedGrid,"workbookView");openedView.GetLock();
        try { Check(openedGrid.Path==openRds&&openedView.ActiveWorkbook.FullName==openRds,"R data workbook keeps its source identity for report replay");openedView.ActiveWorksheet.Cells[1,0].Value=25; }
        finally { openedView.ReleaseLock(); }
        Check(openedGrid.SaveContents(),"ordinary Save writes an opened RDS through base R, not the Excel writer");
        RunRFile(r,folder,"saved.R","args<-commandArgs(TRUE);setwd(args[1]);stopifnot(readRDS('opened.rds')$number[1]==25)");
        Check(true,"base R reads the RDS saved by the Windows document workflow");CloseTestDocument(openedGrid);
        originalGrid.Activate();
    }
    static void RunRFile(string executable,string folder,string filename,string code)
    {
        string file=Path.Combine(folder,filename);File.WriteAllText(file,code);
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
        info.ArgumentList.Add("--vanilla");info.ArgumentList.Add(file);info.ArgumentList.Add(folder);
        using var p=Process.Start(info)!;var error=p.StandardError.ReadToEndAsync();var output=p.StandardOutput.ReadToEndAsync();
        if(!p.WaitForExit(60000)){p.Kill(true);throw new Exception("R test timed out");}
        if(p.ExitCode!=0)throw new Exception(error.GetAwaiter().GetResult());output.GetAwaiter().GetResult();
    }
}
