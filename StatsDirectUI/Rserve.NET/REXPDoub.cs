#if USE_R

using System;
namespace org.rosuda.REngine
{
	
	/// <summary>REXPDouble represents a vector of double precision floating point values. </summary>
	public class REXPDouble:REXPVector
	{
		/// <summary>return <code>true</code> </summary>
		override public bool Numeric
		{
			get
			{
				return true;
			}
			
		}
		private double[] payload;
		
		/// <summary>NA real value as defined in R. Note: it can NOT be used in comparisons, you must use {@link #isNA(double)} instead. </summary>
		public static readonly double NA = BitConverter.ToDouble(BitConverter.GetBytes(0x7ff00000000007a2L), 0);
		
		/// <summary>checks whether a given double value is a NA representation in R. Note that NA is NaN but not all NaNs are NA. </summary>
		public static bool isNA(double value_Renamed)
		{
			return BitConverter.ToInt64(BitConverter.GetBytes(value_Renamed), 0) == 0x7ff00000000007a2L;
		}
		
		/// <summary>create real vector of the length 1 with the given value as its first (and only) element </summary>
		public REXPDouble(double load):base()
		{
			payload = new double[]{load};
		}
		
		public REXPDouble(double[] load):base()
		{
			payload = (load == null)?new double[0]:load;
		}
		
		public REXPDouble(double[] load, REXPList attr):base(attr)
		{
			payload = (load == null)?new double[0]:load;
		}
		
		public override int length()
		{
			return payload.Length;
		}
		
		/// <summary>returns the values represented by this vector </summary>
		public override double[] asDoubles()
		{
			return payload;
		}
		
		/// <summary>converts the values of this vector into integers by cast </summary>
		public override int[] asIntegers()
		{
			int[] a = new int[payload.Length];
			int i = 0;
			while (i < payload.Length)
			{
				//UPGRADE_WARNING: Data types in Visual C# might be different.  Verify the accuracy of narrowing conversions. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1042'"
				a[i] = (int) payload[i];
				i++;
			}
			return a;
		}
		
		/// <summary>converts the values of this vector into strings </summary>
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
		
		/// <summary>returns a boolean vector of the same length as this vector with <code>true</code> for NA values and <code>false</code> for any other values (including NaNs) </summary>
		public override bool[] isNA()
		{
			bool[] a = new bool[payload.Length];
			int i = 0;
			while (i < a.Length)
			{
				a[i] = isNA(payload[i]); i++;
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