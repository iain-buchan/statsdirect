#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPSymbol:REXP
	{
		override public bool Symbol
		{
			get
			{
				return true;
			}
			
		}
		private System.String name;
		
		public REXPSymbol(System.String name):base()
		{
			this.name = (name == null)?"":name;
		}
		
		public override System.String asString()
		{
			return name;
		}
		
		public override System.String[] asStrings()
		{
			return new System.String[]{name};
		}
		
		public override System.String ToString()
		{
			return GetType().FullName + "[" + name + "]";
		}
		
		public override System.String toDebugString()
		{
			return base.toDebugString() + "[" + name + "]";
		}
	}
}
#endif