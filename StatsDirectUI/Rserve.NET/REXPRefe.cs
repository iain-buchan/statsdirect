#if USE_R
namespace org.rosuda.REngine
{
	
	/// <summary>this class represents a reference (proxy) to an R object.
	/// <p>
	/// The reference semantics works by calling {@link #resolve()} (which in turn uses {@link REngine#resolveReference(REXP)} on itself) whenever any methods are accessed. The implementation is not finalized yat and may change as we approach the JRI interface which is more ameanable to reference-style access. Subclasses are free to implement more efficient implementations. 
	/// </summary>
	public class REXPReference:REXP
	{
		override public bool String
		{
			// type checks
			
			get
			{
				return resolve().String;
			}
			
		}
		override public bool Numeric
		{
			get
			{
				return resolve().Numeric;
			}
			
		}
		override public bool Integer
		{
			get
			{
				return resolve().Integer;
			}
			
		}
		override public bool Null
		{
			get
			{
				return resolve().Null;
			}
			
		}
		override public bool Factor
		{
			get
			{
				return resolve().Factor;
			}
			
		}
		override public bool List
		{
			get
			{
				return resolve().List;
			}
			
		}
		override public bool Logical
		{
			get
			{
				return resolve().Logical;
			}
			
		}
		override public bool Environment
		{
			get
			{
				return resolve().Environment;
			}
			
		}
		override public bool Language
		{
			get
			{
				return resolve().Language;
			}
			
		}
		override public bool Symbol
		{
			get
			{
				return resolve().Symbol;
			}
			
		}
		override public bool Vector
		{
			get
			{
				return resolve().Vector;
			}
			
		}
		override public bool Raw
		{
			get
			{
				return resolve().Raw;
			}
			
		}
		override public bool Complex
		{
			get
			{
				return resolve().Complex;
			}
			
		}
		override public bool Recursive
		{
			get
			{
				return resolve().Recursive;
			}
			
		}
		virtual public System.Object Handle
		{
			get
			{
				return handle;
			}
			
		}
		/// <summary>engine which will be used to resolve the reference </summary>
		internal REngine eng;
		/// <summary>an opaque (optional) handle </summary>
		internal System.Object handle;
		
		/// <summary>create an external REXP reference using given engine and handle. The handle value is just an (optional) identifier not used by the implementation directly. </summary>
		public REXPReference(REngine eng, System.Object handle):base()
		{
			this.eng = eng;
			this.handle = handle;
		}
		
		/// <summary>resolve the external REXP reference into an actual REXP object. </summary>
		public virtual REXP resolve()
		{
			try
			{
				return eng.resolveReference(this);
			}
			catch (REngineException)
			{
				// FIXME: what to we do?
			}
			return null;
		}
		
		// basic accessor methods
		public override System.String[] asStrings()
		{
			return resolve().asStrings();
		}
		public override int[] asIntegers()
		{
			return resolve().asIntegers();
		}
		public override double[] asDoubles()
		{
			return resolve().asDoubles();
		}
		public override RList asList()
		{
			return resolve().asList();
		}
		public override RFactor asFactor()
		{
			return resolve().asFactor();
		}
		
		public override int length()
		{
			return resolve().length();
		}
		
		public override REXPList _attr()
		{
			return resolve()._attr();
		}
		
		public override System.String ToString()
		{
			return base.ToString() + "{eng=" + eng + ",h=" + handle + "}";
		}
	}
}
#endif