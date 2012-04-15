#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPRaw:REXPVector
	{
		override public bool Raw
		{
			get
			{
				return true;
			}
			
		}
		private byte /* sbyte */[] payload;
		
		public REXPRaw(byte /* sbyte */[] load):base()
		{
			payload = (load == null)?new byte /* sbyte */[0]:load;
		}
		
		public REXPRaw(byte /* sbyte */[] load, REXPList attr):base(attr)
		{
			payload = (load == null)?new byte /* sbyte */[0]:load;
		}
		
		public override int length()
		{
			return payload.Length;
		}
		
		public override byte /* sbyte */[] asBytes()
		{
			return payload;
		}
	}
}
#endif