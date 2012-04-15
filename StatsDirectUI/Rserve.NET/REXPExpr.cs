#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPExpressionVector:REXPGenericVector
	{
		override public bool Expression
		{
			get
			{
				return true;
			}
			
		}
		public REXPExpressionVector(RList list):base(list)
		{
		}
		public REXPExpressionVector(RList list, REXPList attr):base(list, attr)
		{
		}
	}
}
#endif