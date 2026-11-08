using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckRecordPadding(string folder)
    {
        double[] before = { 312,242,340,388,296,254,391,402,290 }, after = { 300,201,232,312,220,256,328,330,231 };
        double[] Pad(double[] values, int rows) => values.Concat(Enumerable.Repeat(Constant.MISSING, rows-values.Length)).ToArray();
        DataFrame Frame(double[] x, double[] y, int[] physical = null, bool filtered = false)
        {
            DataFrame frame = new();
            int rows = physical == null ? x.Length+1 : physical[^1]+1;
            frame.Variables.Add(Column("Before",x,new WorksheetOrigin(Workbook,"Padding",0,0,rows,DataAcquisitionMode.NumericPreserveRows,true,filtered,1) { DataRowIndices=physical }));
            frame.Variables.Add(Column("After",y,new WorksheetOrigin(Workbook,"Padding",1,0,rows,DataAcquisitionMode.NumericPreserveRows,true,filtered,1) { DataRowIndices=physical }));
            return frame;
        }
        JsonElement Save(DataFrame frame, bool inherited = false, int threshold = RunRecord.EmbeddingThreshold)
        {
            ParameterBag bag = new(); bag.AddInput("data",frame);bag.AddInput("gamma",0.95);bag.AddInput("doAgreement",true);
            OperationRun run = new(new Operation { Name="TPaired" },inherited?bag:null);
            if(!inherited)run.Note(new FrameParameter { Name="data" },"Pairs",value:bag["data"]);
            return JsonDocument.Parse(RunRecord.Write(run,bag,threshold)).RootElement.Clone();
        }
        string Notebook(JsonElement record, Func<FramePointer,FrameValues> reader)
        {
            var snapshot=new JsonObject {
                ["version"]=1,["engineVersion"]="test",["reportHtml"]="<p>Paired report</p>",
                ["entries"]=new JsonArray(new JsonObject {
                    ["id"]="padding-test",["title"]="Paired comparison",["operation"]="TPaired",["html"]="<p>Paired report</p>",
                    ["cells"]=new JsonArray(new JsonObject { ["source"]="Paired report" }),["record"]=JsonNode.Parse(record.GetRawText())
                })
            };
            var method=typeof(RScriptWriter).Assembly.GetType("StatsDirect.UI.WebReports.ReportNotebook").GetMethod("Write",BindingFlags.NonPublic|BindingFlags.Static);
            string json=(string)method.Invoke(null,new object[]{JsonSerializer.SerializeToElement(snapshot),"Paired report",reader,"test"});
            return string.Join("\n",JsonDocument.Parse(json).RootElement.GetProperty("cells").EnumerateArray().Where(c=>c.GetProperty("cell_type").GetString()=="code").Select(c=>c.GetProperty("source").GetString()));
        }
        foreach(int selected in new[]{2500,2501,10000})
        {
            var x=Pad(before,selected);var y=Pad(after,selected);
            JsonElement record=Save(Frame(x,y));
            JsonElement input=AcquisitionInput(record,"data");
            Check(input.GetProperty("columns").EnumerateArray().All(c=>c.GetProperty("rows")[1].GetInt32()==10),"padding "+selected+": pointer bounds trim independently of embedding");
            bool expectedHeld=selected*2<=RunRecord.EmbeddingThreshold;
            Check(record.TryGetProperty("data",out _)==expectedHeld,"padding "+selected+": storage limit remains bounded");
            int reads=0;
            FrameValues Read(FramePointer pointer)
            {
                reads++;FrameValues values=new();
                foreach(var column in pointer.Columns)
                {
                    double[] original=column.Column==1?x:y;
                    var cells=AcquisitionValues(original.Take(column.LastRow-column.FirstRow).ToArray());
                    Check(RunRecord.Hash(cells)==column.Hash,"padding "+selected+": trimmed pointer/hash roundtrip");
                    values.Columns.Add(new ColumnValues { Title=column.Title,Values=cells });
                }
                return values;
            }
            var script=RScriptWriter.Write(record,null,Read);
            string assertions="\nstopifnot(identical(before,c(312,242,340,388,296,254,391,402,290)),identical(after,c(300,201,232,312,220,256,328,330,231)),n==9L,is.finite(mean(d)))\ncat('PADDING_OK\\n')\n";
            Check(RunR(script.Text+assertions,Path.Combine(folder,"padding-"+selected)).Contains("PADDING_OK"),"padding "+selected+": real R uses nine pairs");
            Check(reads==(expectedHeld?0:1),"padding "+selected+": expected held/reread path exercised");
            string notebookCode=Notebook(record,Read);
            Check(RunR(notebookCode+"\ncat('NOTEBOOK_PADDING_OK\\n')\n",Path.Combine(folder,"notebook-padding-"+selected)).Contains("Mean of differences = 56.111111  (n = 9)"),"padding "+selected+": exported notebook executes correct replay");
        }
        // A genuinely large sample also trims its tail while retaining its last real observation.
        double[] largeX=Enumerable.Range(1,3001).Select(i=>100.0+i*0.1).ToArray();
        double[] largeY=largeX.Select((v,i)=>v-2-Math.Sin(i*.71)).ToArray();
        var large=Save(Frame(Pad(largeX,4100),Pad(largeY,4100)));
        Check(!large.TryGetProperty("data",out _) && AcquisitionInput(large,"data").GetProperty("columns")[1].GetProperty("rows")[1].GetInt32()==3002,"large real sample keeps 3001 pairs without embedding or blank padding");
        Check(AcquisitionInput(large,"data").GetProperty("columns")[0].GetProperty("hash").GetString()==AcquisitionHash(largeX),"large real sample retains its acquisition hash");
        FrameValues ReadLarge(FramePointer pointer)
        {
            FrameValues values=new();foreach(var c in pointer.Columns)
                values.Columns.Add(new ColumnValues{Title=c.Title,Values=AcquisitionValues((c.Column==1?largeX:largeY).Take(c.LastRow-c.FirstRow).ToArray())});
            return values;
        }
        Check(RunR(RScriptWriter.Write(large,null,ReadLarge).Text+"\nstopifnot(length(before)==3001L,n==3001L,tail(before,1)==400.1)\ncat('LARGE_SAMPLE_OK\\n')\n",Path.Combine(folder,"large-real-sample")).Contains("LARGE_SAMPLE_OK"),"large real sample replays every pair and its chart in R");

        // Physical row 3 is hidden. Visible values live at rows 2,4,5,6,7.
        int[] rows={1,3,4,5,6};
        var filtered=Frame(new[]{10.0,30,40,Constant.MISSING,Constant.MISSING},new[]{11.0,32,43,Constant.MISSING,Constant.MISSING},rows,true);
        var hidden=Save(filtered,threshold:0);
        Check(AcquisitionInput(hidden,"data").GetProperty("columns").EnumerateArray().All(c=>c.GetProperty("rows")[1].GetInt32()==5),"hidden rows: pointer ends at physical last observation, not visible count");
        FrameValues ReadHidden(FramePointer pointer)
        {
            FrameValues result=new();
            foreach(var c in pointer.Columns)
            {
                var variable=filtered.Variables[c.Column-1];
                object[] values=RunRecord.Values(variable).Where((_,i)=>rows[i]+1<=c.LastRow).ToArray();
                Check(RunRecord.Hash(values)==c.Hash,"hidden rows: pointer-only reread hash matches acquired data");
                result.Columns.Add(new ColumnValues {Title=c.Title,Values=values});
            }
            return result;
        }
        Check(RunR(RScriptWriter.Write(hidden,null,ReadHidden).Text+"\nstopifnot(identical(before,c(10,30,40)),identical(after,c(11,32,43)))\ncat('HIDDEN_OK\\n')\n",Path.Combine(folder,"hidden-pointer")).Contains("HIDDEN_OK"),"hidden rows: pointer-only replay retains every observation");
        Check(RunR(Notebook(hidden,ReadHidden),Path.Combine(folder,"hidden-notebook")).Contains("Mean of differences = -2  (n = 3)"),"hidden rows: notebook reread uses correct physical range");

        // A legacy filtered origin without a map must not invent physical coordinates.
        var legacy=Save(Frame(new[]{10.0,30,40,Constant.MISSING},new[]{11.0,32,43,Constant.MISSING},filtered:true));
        Check(AcquisitionInput(legacy,"data").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32()==5,"legacy filtered origin without a map retains its original range");
        var mixed=Frame(new[]{10.0,30,40},new[]{11.0,32,Constant.MISSING,Constant.MISSING,Constant.MISSING,Constant.MISSING,Constant.MISSING},filtered:true);
        ((WorksheetOrigin)mixed.Variables[0].Origin).Rows=7;
        ((WorksheetOrigin)mixed.Variables[1].Origin).Rows=8;
        ((WorksheetOrigin)mixed.Variables[1].Origin).WasFiltered=false;
        var mixedColumns=AcquisitionInput(Save(mixed),"data").GetProperty("columns");
        Check(mixedColumns[0].GetProperty("rows")[1].GetInt32()==7 && mixedColumns[1].GetProperty("rows")[1].GetInt32()==4,"trimming another column never rewrites an unmapped filtered column's unchanged range");
        int[] acquiredRows={1,3,4,5,6};
        var mapped=Frame(new[]{10.0,30,40,Constant.MISSING,Constant.MISSING},new[]{11.0,32,43,Constant.MISSING,Constant.MISSING},acquiredRows,true);
        ParameterBag mappedBag=new();mappedBag.AddInput("data",mapped);
        OperationRun mappedRun=new(new Operation{Name="TPaired"});mappedRun.Note(new FrameParameter{Name="data"},"Pairs",value:mappedBag["data"]);
        acquiredRows[2]=999;
        var unchanged=JsonDocument.Parse(RunRecord.Write(mappedRun,mappedBag)).RootElement;
        Check(AcquisitionInput(unchanged,"data").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32()==5,"captured physical row mapping survives later origin-array mutation");

        // Acquisition snapshots stay immutable after algorithms mutate large arrays,
        // including the inherited-frame path and columns with different missing tails.
        foreach(bool inherited in new[]{false,true})
        {
            double[] a=Pad(new[]{1.0,2,3},3000),b=Pad(new[]{4.0,5,6,7,8},3000);
            var frame=Frame(a,b);ParameterBag bag=new();bag.AddInput("data",frame);
            OperationRun run=new(new Operation{Name="TPaired"},inherited?bag:null);
            if(!inherited)run.Note(new FrameParameter{Name="data"},"Pairs",value:bag["data"]);
            Array.Fill(a,999);Array.Fill(b,999);rows[0]=99;
            var saved=JsonDocument.Parse(RunRecord.Write(run,bag)).RootElement;
            var columns=AcquisitionInput(saved,"data").GetProperty("columns");
            Check(columns[0].GetProperty("rows")[1].GetInt32()==6 && columns[0].GetProperty("hash").GetString()==AcquisitionHash(new[]{1.0,2,3,Constant.MISSING,Constant.MISSING}) && columns[1].GetProperty("hash").GetString()==AcquisitionHash(new[]{4.0,5,6,7,8}),"large "+(inherited?"inherited":"acquired")+" snapshot survives array mutation and aligned missing padding");
        }
        var allMissing=Save(Frame(new[]{Constant.MISSING,Constant.MISSING},new[]{Constant.MISSING,Constant.MISSING}));
        Check(AcquisitionInput(allMissing,"data").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32()==3,"all-missing selections retain valid physical ranges");
        var blankColumn=Save(Frame(new[]{Constant.MISSING,Constant.MISSING,Constant.MISSING,Constant.MISSING},new[]{1.0,2,3,Constant.MISSING}));
        Check(AcquisitionInput(blankColumn,"data").GetProperty("columns")[0].GetProperty("hash").GetString()==AcquisitionHash(new[]{Constant.MISSING,Constant.MISSING,Constant.MISSING}),"all-missing column uses the correct prefix hash beside observed data");
    }
}
