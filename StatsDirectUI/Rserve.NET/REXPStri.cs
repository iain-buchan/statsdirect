#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPString:REXPVector
	{
		override public bool String
		{
			get
			{
				return true;
			}
			
		}
		private System.String[] payload;
		
		public REXPString(System.String load):base()
		{
			payload = new System.String[]{load};
		}
		
		public REXPString(System.String[] load):base()
		{
			payload = (load == null)?new System.String[0]:load;
		}
		
		public REXPString(System.String[] load, REXPList attr):base(attr)
		{
			payload = (load == null)?new System.String[0]:load;
		}
		
		public override int length()
		{
			return payload.Length;
		}
		
		public override System.String[] asStrings()
		{
			return payload;
		}
		
		public override bool[] isNA()
		{
			bool[] a = new bool[payload.Length];
			int i = 0;
			while (i < a.Length)
			{
				a[i] = (payload[i] == null); i++;
			}
			return a;
		}
		
		public override System.String toDebugString()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder(base.toDebugString() + "{");
			int i = 0;
			while (i < payload.Length && i < maxDebugItems)
			{
				if (i > 0)
					sb.Append(",");
				sb.Append("\"" + payload[i] + "\"");
				i++;
			}
			if (i < payload.Length)
				sb.Append(",..");
			return sb.ToString() + "}";
		}
	}
}
#endif