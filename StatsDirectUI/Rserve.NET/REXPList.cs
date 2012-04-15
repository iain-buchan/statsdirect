#if USE_R
namespace org.rosuda.REngine
{
	
	/// <summary>represents a pairlist in R </summary>
	public class REXPList:REXPVector
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
		
		public REXPList(RList list):base()
		{
			payload = (list == null)?new RList():list;
		}
		
		public REXPList(RList list, REXPList attr):base(attr)
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
				System.String name = payload.keyAt(i);
				if (name != null)
					sb.Append(name + "=");
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