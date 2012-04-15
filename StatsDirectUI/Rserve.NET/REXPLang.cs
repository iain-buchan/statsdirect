#if USE_R
namespace org.rosuda.REngine
{
	
	/// <summary>represents a language object in R </summary>
	public class REXPLanguage:REXPList
	{
		override public bool Language
		{
			get
			{
				return true;
			}
			
		}
		public REXPLanguage(RList list):base(list)
		{
		}
		public REXPLanguage(RList list, REXPList attr):base(list, attr)
		{
		}
	}
}
#endif