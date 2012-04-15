#if USE_R
namespace org.rosuda.REngine
{
	
	public abstract class REngine
	{
		/// <summary>retrieve the last created engine</summary>
		/// <returns> last created engine or <code>null</code> if no engine was created yet 
		/// </returns>
		public static REngine LastEngine
		{
			get
			{
				return lastEngine;
			}
			
		}
		/// <summary>last created engine or <code>null</code> if there is none </summary>
		protected internal static REngine lastEngine = null;
		
		/// <summary>this is the designated constructor for REngine classes. It uses reflection to call createEngine method on the given REngine class.</summary>
		/// <param name="klass">fully qualified class-name of a REngine implementation
		/// </param>
		/// <returns> REngine implementation or <code>null</code> if <code>createEngine</code> invokation failed 
		/// </returns>
		public static REngine engineForClass(System.String klass)
		{
			//UPGRADE_TODO: The differences in the format  of parameters for method 'java.lang.Class.forName'  may cause compilation errors.  "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1092'"
			System.Type cl = System.Type.GetType(klass);
			if (cl == null)
			{
				throw (new System.Exception("can't find engine class " + klass));
			}
			System.Reflection.MethodInfo m = cl.GetMethod("createEngine", ((System.Type[]) null == null)?new System.Type[0]:(System.Type[]) null);
			System.Object o = m.Invoke(null, (System.Object[]) null);
			return lastEngine = (REngine) o;
		}
		
		/// <summary>parse a string into an expression vector</summary>
		/// <param name="text">string to parse
		/// </param>
		/// <param name="resolve">resolve the resulting REXP or just return a reference 
		/// </param>
		public abstract REXP parse(System.String text, bool resolve);
		/// <summary>evaluate an expression vector</summary>
		/// <param name="what">an expression (or vector of such) to evaluate
		/// </param>
		/// <param name="where">environment to evaluate in (or <code>null</code> for global env)
		/// </param>
		/// <param name="resolve">resolve the resulting REXP or just return a reference
		/// </param>
		/// <returns> the result of the evaluation of the last expression 
		/// </returns>
		public abstract REXP eval(REXP what, REXP where, bool resolve);
		/// <summary>assign into an environment</summary>
		/// <param name="symbol">symbol name
		/// </param>
		/// <param name="value">value to assign
		/// </param>
		/// <param name="env">environment to assign to 
		/// </param>
		public abstract void  assign(System.String symbol, REXP value_Renamed, REXP env);
		/// <summary>get a value from an environment</summary>
		/// <param name="symbol">symbol name
		/// </param>
		/// <param name="env">environment
		/// </param>
		/// <param name="resolve">resolve the resulting REXP or just return a reference		
		/// </param>
		/// <returns> value 
		/// </returns>
		public abstract REXP get_Renamed(System.String symbol, REXP env, bool resolve);
		
		/// <summary>fetch the contents of the given reference. The resulting REXP may never be REXPReference.</summary>
		/// <param name="ref">reference to resolve
		/// </param>
		/// <returns> resolved reference 
		/// </returns>
		public abstract REXP resolveReference(REXP ref_Renamed);
		
		public abstract REXP getParentEnvironment(REXP env, bool resolve);
		
		public abstract REXP newEnvironment(REXP parent, bool resolve);
		
		/* derived methods */
		public virtual REXP parseAndEval(System.String text, REXP where, bool resolve)
		{
			REXP p = parse(text, false);
			return eval(p, where, resolve);
		}
		public virtual REXP parseAndEval(System.String cmd)
		{
			return parseAndEval(cmd, null, true);
		}
		
		
		//--- capabilities ---
		public virtual bool supportsReferences()
		{
			return false;
		}
		public virtual bool supportsEnvironemnts()
		{
			return false;
		}
		public virtual bool supportsREPL()
		{
			return false;
		}
		
		//--- convenience methods ---
		public virtual void  assign(System.String symbol, double[] d)
		{
			assign(symbol, new REXPDouble(d), null);
		}
		public virtual void  assign(System.String symbol, int[] d)
		{
			assign(symbol, new REXPInteger(d), null);
		}
		public virtual void  assign(System.String symbol, System.String[] d)
		{
			assign(symbol, new REXPString(d), null);
		}
		public virtual void  assign(System.String symbol, byte[] d)
		{
			assign(symbol, new REXPRaw(d), null);
		}
		
		public override System.String ToString()
		{
			return base.ToString() + ((lastEngine == this)?"{last}":"");
		}
	}
}
#endif