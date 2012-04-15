#if USE_R
namespace org.rosuda.REngine
{
	// environments are like REXPReferences except that they cannot be resolved
	
	public class REXPEnvironment:REXP
	{
		override public bool Environment
		{
			get
			{
				return true;
			}
			
		}
		virtual internal System.Object Handle
		{
			get
			{
				return handle;
			}
			
		}
		internal REngine eng;
		internal System.Object handle;
		public REXPEnvironment(REngine eng, System.Object handle):base()
		{
			this.eng = eng;
			this.handle = handle;
		}
		
		public virtual REXP get_Renamed(System.String name, bool resolve)
		{
			return eng.get_Renamed(name, this, resolve);
		}
		
		public virtual REXP get_Renamed(System.String name)
		{
			return get_Renamed(name, true);
		}
		
		public virtual void  assign(System.String name, REXP value_Renamed)
		{
			eng.assign(name, value_Renamed, this);
		}
		
		public virtual REXP parent(bool resolve)
		{
			return eng.getParentEnvironment(this, resolve);
		}
		
		public virtual REXPEnvironment parent()
		{
			return (REXPEnvironment) eng.getParentEnvironment(this, true);
		}
	}
}
#endif