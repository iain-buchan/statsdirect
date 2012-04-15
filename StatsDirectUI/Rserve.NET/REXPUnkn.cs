#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPUnknown:REXP
	{
		virtual public int Type
		{
			get
			{
				return type;
			}
			
		}
		internal int type;
		public REXPUnknown(int type):base()
		{
			this.type = type;
		}
		public REXPUnknown(int type, REXPList attr):base(attr)
		{
			this.type = type;
		}
		
		public override System.String ToString()
		{
			return base.ToString() + "[" + type + "]";
		}
	}
}
#endif