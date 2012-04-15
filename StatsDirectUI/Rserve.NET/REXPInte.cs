#if USE_R
namespace org.rosuda.REngine
{
	
	/// <summary>REXPDouble represents a vector of integer values. </summary>
	public class REXPInteger:REXPVector
	{
		override public bool Integer
		{
			get
			{
				return true;
			}
			
		}
		override public bool Numeric
		{
			get
			{
				return true;
			}
			
		}
		protected internal int[] payload;
		
		/// <summary>NA integer value as defined in R. Unlike its real equivalent this one can be used in comparisons, although {@link #isNA(int) } is provided for consistency. </summary>
		public const int NA = - 2147483648;
		
		public static bool isNA(int value_Renamed)
		{
			return (value_Renamed == NA);
		}
		
		/// <summary>create integer vector of the length 1 with the given value as its first (and only) element </summary>
		public REXPInteger(int load):base()
		{
			payload = new int[]{load};
		}
		
		/// <summary>create integer vector with the payload specified by <code>load</code> </summary>
		public REXPInteger(int[] load):base()
		{
			payload = (load == null)?new int[0]:load;
		}
		
		/// <summary>create integer vector with the payload specified by <code>load</code> and attributes <code>attr</code> </summary>
		public REXPInteger(int[] load, REXPList attr):base(attr)
		{
			payload = (load == null)?new int[0]:load;
		}
		
		public override int length()
		{
			return payload.Length;
		}
		
		public override int[] asIntegers()
		{
			return payload;
		}
		
		/// <summary>returns the contents of this vector as doubles </summary>
		public override double[] asDoubles()
		{
			double[] d = new double[payload.Length];
			int i = 0;
			while (i < payload.Length)
			{
				d[i] = (double) payload[i]; i++;
			}
			return d;
		}
		
		/// <summary>returns the contents of this vector as strings </summary>
		public override System.String[] asStrings()
		{
			System.String[] s = new System.String[payload.Length];
			int i = 0;
			while (i < payload.Length)
			{
				s[i] = "" + payload[i]; i++;
			}
			return s;
		}
		
		public override bool[] isNA()
		{
			bool[] a = new bool[payload.Length];
			int i = 0;
			while (i < a.Length)
			{
				a[i] = (payload[i] == NA); i++;
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
				sb.Append(payload[i]);
				i++;
			}
			if (i < payload.Length)
				sb.Append(",..");
			return sb.ToString() + "}";
		}
	}
}
#endif