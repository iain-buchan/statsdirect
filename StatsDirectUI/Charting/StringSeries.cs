namespace StatsDirect.Charting
{
    public class StringSeries : ISeries 
    { 
        public string?[] Data { get; }

        public string? Title { get; }

        public StringSeries(int dataLength, string? title = null)
        { 
            Data = new string?[dataLength];
            Title = title;
        }

        public StringSeries(string?[] data, string? title = null)
        {
            Data = data;
            Title = title;
        }

        public int Length => Data.Length;
    } 
} 
