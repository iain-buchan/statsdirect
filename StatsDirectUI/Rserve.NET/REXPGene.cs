#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPGenericVector:REXPVector
	{
		override public bool List
		{
			get
			{
				return true;
			}
			
		}
		override public bool Recursive
		{
			get
			{
				return true;
			}
			
		}
		private RList payload;
		
		public REXPGenericVector(RList list):base()
		{
			payload = (list == null)?new RList():list;
			// automatically generate 'names' attribute
			if (payload.Named)
				attr = new REXPList(new RList(new REXP[]{new REXPString(payload.keys())}, new System.String[]{"names"}));
		}
		
		public REXPGenericVector(RList list, REXPList attr):base(attr)
		{
			payload = (list == null)?new RList():list;
		}
		
		public override int length()
		{
			return payload.Count;
		}
		
		public override RList asList()
		{
			return payload;
		}
		
		public override System.String ToString()
		{
			return base.ToString() + (asList().Named?"named":"");
		}
		
		public override System.String toDebugString()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder(base.toDebugString() + "{");
			int i = 0;
			while (i < payload.Count && i < maxDebugItems)
			{
				if (i > 0)
					sb.Append(",\n");
				sb.Append(payload.at(i).toDebugString());
				i++;
			}
			if (i < payload.Count)
				sb.Append(",..");
			return sb.ToString() + "}";
		}
	}
}
#endif