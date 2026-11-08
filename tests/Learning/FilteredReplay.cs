using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static async Task TestFilteredReplay(frmMain main)
    {
        var grid=(frmSpreadsheetGear)main.CreateGrid("Filtered replay fixture");
        try
        {
            grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Before","values":[10,20,30,40]},{"title":"After","values":[11,22,32,43]}]""").AsArray());
            var view=(SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid,"workbookView");
            string sheet;
            view.GetLock();
            try { view.ActiveWorksheet.Cells[2,0].EntireRow.Hidden=true; sheet=view.ActiveWorksheet.Name; }
            finally { view.ReleaseLock(); }
            string path=((IGrid)grid).WorkbookPath;
            var origins=Enumerable.Range(0,2).Select(c=>new WorksheetOrigin(path,sheet,c,0,7,DataAcquisitionMode.NumericPreserveRows,true,true,1)).ToList();
            DataFrame frame=((IGrid)grid).ReadColumns(origins);
            Check(frame.VariableCount==2 && ((WorksheetOrigin)frame.Variables[0].Origin).DataRowIndices.SequenceEqual(new[]{1,3,4,5,6}),"native filtered grid maps acquired observations to physical data rows");
            ParameterBag bag=new();bag.AddInput("data",frame);bag.AddInput("gamma",0.95);bag.AddInput("doAgreement",true);
            OperationRun run=new(new Operation{Name="TPaired"});run.Note(new FrameParameter{Name="data"},"Pairs",value:bag["data"]);
            JsonElement record=JsonDocument.Parse(RunRecord.Write(run,bag,threshold:0)).RootElement.Clone();
            FramePointer pointer=FramePointer.From(record.GetProperty("inputs")[0]);
            Check(pointer.Columns.All(c=>c.LastRow==5),"native filtered replay points to the physical last observation after trimming");
            FrameValues reread=FrameReader.Read(pointer);
            Check(reread.Unavailable==null && reread.Columns.Count==2 && reread.Columns.All(c=>!c.Changed) && reread.Columns[0].Values.SequenceEqual(new object[]{10d,30d,40d}) && reread.Columns[1].Values.SequenceEqual(new object[]{11d,32d,43d}),"actual FrameReader pointer-only reread keeps hidden-row alignment and matching hashes");
            RScript script=RScriptWriter.Write(record,null,FrameReader.Read);
            await RunConversationR(script.Text,"filtered-replay","stopifnot(identical(before,c(10,30,40)),identical(after,c(11,32,43)),n==3L,mean(d)==-2)\ncat('FILTERED_REPLAY_OK\\n')","FILTERED_REPLAY_OK");
        }
        finally { CloseTestDocument(grid); }
    }
}
