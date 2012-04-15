#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPFactor:REXPInteger
	{
		override public bool Factor
		{
			get
			{
				return true;
			}
			
		}
		private System.String[] levels;
		private RFactor factor;
		
		/// <summary>create a new factor REXP</summary>
		/// <param name="ids">indices (one-based!)
		/// </param>
		/// <param name="levels">levels 
		/// </param>
		public REXPFactor(int[] ids, System.String[] levels):base(ids)
		{
			this.levels = (levels == null)?(new System.String[0]):levels;
			factor = new RFactor(this.payload, this.levels, false, 1);
			attr = new REXPList(new RList(new REXP[]{new REXPString(this.levels), new REXPString("factor")}, new System.String[]{"levels", "class"}));
		}
		
		/// <summary>create a new factor REXP</summary>
		/// <param name="ids">indices (one-based!)
		/// </param>
		/// <param name="levels">levels
		/// </param>
		/// <param name="attr">attributes 
		/// </param>
		public REXPFactor(int[] ids, System.String[] levels, REXPList attr):base(ids, attr)
		{
			this.levels = (levels == null)?(new System.String[0]):levels;
			factor = new RFactor(this.payload, this.levels, false, 1);
		}
		
		/// <summary>create a new factor REXP from an existing RFactor</summary>
		/// <param name="factor">factor object (can be of any index base, the contents will be pulled with base 1) 
		/// </param>
		public REXPFactor(RFactor factor):base(factor.asIntegers(1))
		{
			this.factor = factor;
			this.levels = factor.levels();
			attr = new REXPList(new RList(new REXP[]{new REXPString(this.levels), new REXPString("factor")}, new System.String[]{"levels", "class"}));
		}
		
		public REXPFactor(RFactor factor, REXPList attr):base(factor.asIntegers(1), attr)
		{
			this.factor = factor;
			this.levels = factor.levels();
		}
		
		/// <summary>the factor is guaranteed to have index base 1 </summary>
		public override RFactor asFactor()
		{
			return factor;
		}
		
		public override System.String[] asStrings()
		{
			return factor.asStrings();
		}
		
		public override System.String ToString()
		{
			return base.ToString() + "[" + levels.Length + "]";
		}
	}
}
#endif