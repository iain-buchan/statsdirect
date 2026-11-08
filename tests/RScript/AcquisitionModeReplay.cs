using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

internal static partial class Program
{
    // The production acquisition and reread paths decide titles, blank trimming
    // and typed missing markers. Tests must not manufacture those decisions.
    private sealed class RecordCells : IGetCells
    {
        readonly object[][] cells;
        static readonly MethodInfo number=typeof(CellSelection).Assembly.GetType("StatsDirect.UI.frmSpreadsheetGear").GetMethod("ToCellValue",BindingFlags.Static|BindingFlags.Public);
        internal RecordCells(params object[][] cells) { this.cells=cells; }
        object Cell(int row,int col)=>row>=0&&row<cells[col].Length?cells[col][row]:null;
        public string GetColumnTitle(int col)=>"Column "+(col+1);
        public bool IsFormattedLikeATitle(int col,int row)=>false;
        public string GetCellText(int row,int col)=>Convert.ToString(Cell(row,col),CultureInfo.InvariantCulture)??"";
        public double GetCellValue(int row,int col)=>(double)number.Invoke(null,new[]{Cell(row,col)});
        public (string[],int) GetCellTexts(int col,int first,int last)=>(Enumerable.Range(first,Math.Max(0,last-first+1)).Select(r=>GetCellText(r,col)).ToArray(),Math.Max(0,last-first+1));
        public (double[],int) GetCellValues(int col,int first,int last)=>(Enumerable.Range(first,Math.Max(0,last-first+1)).Select(r=>GetCellValue(r,col)).ToArray(),Math.Max(0,last-first+1));
        public (object[,],int) GetCellObjects(int col,int first,int last) {
            int count=Math.Max(0,last-first+1);var result=new object[count,1];
            for(int i=0;i<count;i++)result[i,0]=Cell(first+i,col);return(result,count);
        }
        public (string[],int) GetCellFormulae(int col,int first,int last)=>GetCellTexts(col,first,last);
        public (DateTime[],int) GetCellDateValues(int col,int first,int last)=>(new DateTime[Math.Max(0,last-first+1)],Math.Max(0,last-first+1));
        internal DataFrame Acquire(int column,int first,int count,DataAcquisitionMode mode,bool refill=false,bool title=false) {
            CellSelection selected=new(){LongestRowCount=count};
            selected.ColumnSelections.Add(new CellColumnSelection(this){ColumnIndex=column,RowIndex=first,RowCount=count,WorkbookPath=Workbook,WorksheetName="Modes"});
            return (DataFrame)typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor").GetMethod("ProcessCellArray",BindingFlags.Public|BindingFlags.Static)
                .Invoke(null,new object[]{selected,mode,0,refill,title,1,"mode fixture"});
        }
        internal FrameValues Read(FramePointer pointer) {
            FrameValues result=new();foreach(var col in pointer.Columns) {
                var variable=Acquire(col.Column-1,col.FirstRow-1,col.LastRow-col.FirstRow+1,Enum.Parse<DataAcquisitionMode>(pointer.Mode),true,col.HasTitle).Variables[0];
                result.Columns.Add(new ColumnValues{Title=variable.Title,Values=RunRecord.Values(variable),Changed=RunRecord.Hash(variable)!=col.Hash});
            }return result;
        }
    }

    private static string AcquisitionNotebook(JsonElement record,Func<FramePointer,FrameValues> reader) {
        var snapshot=new JsonObject { ["version"]=1,["engineVersion"]="test",["reportHtml"]="<p>Recorded analysis</p>",
            ["entries"]=new JsonArray(new JsonObject { ["id"]="mode-test",["title"]="Recorded analysis",["operation"]=record.GetProperty("operation").GetString(),
                ["html"]="<p>Recorded analysis</p>",["cells"]=new JsonArray(new JsonObject{["source"]="Recorded analysis"}),["record"]=JsonNode.Parse(record.GetRawText()) }) };
        var method=typeof(RScriptWriter).Assembly.GetType("StatsDirect.UI.WebReports.ReportNotebook").GetMethod("Write",BindingFlags.NonPublic|BindingFlags.Static);
        string json=(string)method.Invoke(null,new object[]{JsonSerializer.SerializeToElement(snapshot),"Recorded analysis",reader,"test"});
        return string.Join("\n",JsonDocument.Parse(json).RootElement.GetProperty("cells").EnumerateArray().Where(c=>c.GetProperty("cell_type").GetString()=="code").Select(c=>c.GetProperty("source").GetString()));
    }

    private static void CheckAcquisitionModeReplay(string folder)
    {
        foreach(int observed in new[]{3,5001})
        foreach(string layout in new[]{"selected-title","title-above","leading-blank"})
        foreach(string marker in new[]{"*",".","MISSING"}) {
            var cells=new List<object>{"Score"};if(layout=="leading-blank")cells.Add(null);
            cells.AddRange(Enumerable.Range(1,observed).Select(i=>(object)(double)i));cells.Add(marker);
            var grid=new RecordCells(cells.ToArray());int first=layout=="title-above"?1:0;
            DataFrame data=grid.Acquire(0,first,cells.Count-first,DataAcquisitionMode.NumericReplaceMissing);
            Check(data.MaxRows==observed+1&&RunRecord.Missing(((DoubleVariable)data.Variables[0]).Data[^1]),"typed "+marker+" "+layout+" "+observed+": actual acquisition retains explicit missing observation");
            string acquiredHash=RunRecord.Hash(data.Variables[0]);
            var (run,_)=Run("TSingle",new(){["data"]=data,["gamma"]=0.95,["population-mean"]=0.0});
            JsonElement record=Record(run);var input=AcquisitionInput(record,"data");
            var origin=(WorksheetOrigin)data.Variables[0].Origin;var column=input.GetProperty("columns")[0];
            Check(column.GetProperty("rows")[0].GetInt32()==origin.TopRow+1&&column.GetProperty("rows")[1].GetInt32()==origin.TopRow+origin.Rows&&column.GetProperty("hash").GetString()==acquiredHash,"typed "+marker+" "+layout+" "+observed+": original physical bounds and full hash stay intact");
            if(record.TryGetProperty("data",out var held))Check(held.GetProperty("data").EnumerateObject().Single().Value.GetArrayLength()==observed+1,"typed marker is preserved in held data");
            var reread=grid.Read(FramePointer.From(input));
            Check(reread.Columns.All(c=>!c.Changed)&&reread.Columns[0].Values.Length==observed+1,"typed "+marker+" "+layout+" "+observed+": production acquisition reread reproduces hash and full length");
            var script=RScriptWriter.Write(record,null,grid.Read);
            Check(!script.DataUnavailable&&!script.Text.Contains("has changed in the workbook"),"unchanged typed-marker workbook is never falsely labelled changed");
            string label=layout+"-"+observed+"-"+(marker=="*"?"star":marker=="."?"dot":"word");
            string assertion=$"\nstopifnot(n=={observed}L,unname(r$parameter)=={observed-1}L,mean(bp)=={(observed+1)/2.0:R})\ncat('MODE_REPLAY_OK\\n')\n";
            Check(RunR(script.Text+assertion,Path.Combine(folder,"mode-"+label)).Contains("MODE_REPLAY_OK"),"typed "+label+": R matches actual native sample size and observations");
            Check(RunR(AcquisitionNotebook(record,grid.Read)+"\ncat('MODE_NOTEBOOK_OK\\n')\n",Path.Combine(folder,"mode-notebook-"+label)).Contains("MODE_NOTEBOOK_OK"),"typed "+label+": notebook export accepts unchanged worksheet and replays");
        }
        // A long independent-sample frame must neither be cut nor constrain an aligned frame's cutoff.
        var legacyGrid=new RecordCells(new object[]{"Independent",1d,2d,"*",null,null},new object[]{"Aligned",10d,20d,null,null,null});
        var independent=legacyGrid.Acquire(0,0,6,DataAcquisitionMode.NumericReplaceMissing);
        var aligned=legacyGrid.Acquire(1,0,6,DataAcquisitionMode.NumericPreserveRows);
        ParameterBag bag=new();bag.AddInput("independent",independent);bag.AddInput("aligned",aligned);
        OperationRun mixed=new(new Operation{Name="ModeRegression"});
        mixed.Note(new FrameParameter{Name="independent"},"Independent",value:bag["independent"]);
        mixed.Note(new FrameParameter{Name="aligned"},"Aligned",value:bag["aligned"]);
        var saved=JsonDocument.Parse(RunRecord.Write(mixed,bag)).RootElement;
        Check(saved.GetProperty("data").GetProperty("independent").EnumerateObject().Single().Value.GetArrayLength()==3&&saved.GetProperty("data").GetProperty("aligned").EnumerateObject().Single().Value.GetArrayLength()==2,"mixed modes retain explicit missing markers without blocking positional blank-tail trimming");
        var before=AcquisitionInput(saved,"independent").GetProperty("columns")[0];
        Check(before.GetProperty("hash").GetString()==RunRecord.Hash(independent.Variables[0]),"mixed-mode non-positional hash remains unchanged");
    }
}
