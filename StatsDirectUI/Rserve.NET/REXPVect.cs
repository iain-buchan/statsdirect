#if USE_R
namespace org.rosuda.REngine
{
	
	/// <summary>abstract class representing all vectors in R </summary>
	public abstract class REXPVector:REXP
	{
		override public bool Vector
		{
			get
			{
				return true;
			}
			
		}
		public REXPVector():base()
		{
		}
		
		public REXPVector(REXPList attr):base(attr)
		{
		}
		
		/// <summary>returns the length of the vector (i.e. the number of elements) </summary>
		public abstract override int length();
		
		/// <summary>returns a boolean vector of the same length as this vector with <code>true</code> for NA values and <code>false</code> for any other values </summary>
		public virtual bool[] isNA()
		{
			bool[] a = new bool[length()];
			return a;
		}
		
		public override System.String ToString()
		{
			return base.ToString() + "[" + length() + "]";
		}
		
		public override System.String toDebugString()
		{
			return base.toDebugString() + "[" + length() + "]";
		}
	}
}
#endif