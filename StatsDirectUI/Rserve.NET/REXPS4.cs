#if USE_R
namespace org.rosuda.REngine
{
	
	/// <summary>S4 REXP is a completely vanilla REXP </summary>
	public class REXPS4:REXP
	{
		public REXPS4():base()
		{
		}
		public REXPS4(REXPList attr):base(attr)
		{
		}
	}
}
#endif