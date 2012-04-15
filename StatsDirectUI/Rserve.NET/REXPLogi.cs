#if USE_R
namespace org.rosuda.REngine
{
	
	public class REXPLogical:REXPVector
	{
		override public bool Logical
		{
			get
			{
				return true;
			}
			
		}
		private byte /* sbyte */[] payload;
		
		//UPGRADE_NOTE: Final was removed from the declaration of 'NA '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
		public byte /* sbyte */ NA = 2;
		
		public REXPLogical(byte /* sbyte */[] load):base()
		{
			payload = (load == null)?new byte /* sbyte */[0]:load;
		}
		
		public REXPLogical(byte /* sbyte */[] load, REXPList attr):base(attr)
		{
			payload = (load == null)?new byte /* sbyte */[0]:load;
		}
		
		public REXPLogical(bool[] load, REXPList attr):base(attr)
		{
			if (load == null)
			{
				payload = new byte /* sbyte */[0];
			}
			else
			{
				payload = new byte /* sbyte */[load.Length];
				int i = 0;
				while (i < load.Length)
				{
					payload[i] = (byte /* sbyte */) (load[i]?1:0);
					i++;
				}
			}
		}
		
		public override int length()
		{
			return payload.Length;
		}
		
		public override int[] asIntegers()
		{
			int[] a = new int[payload.Length];
			int i = 0;
			while (i < payload.Length)
			{
				a[i] = (int) payload[i]; i++;
			}
			return a;
		}
		
		public override byte /* sbyte */[] asBytes()
		{
			return payload;
		}
		
		public override System.String[] asStrings()
		{
			System.String[] s = new System.String[payload.Length];
			int i = 0;
			while (i < payload.Length)
			{
				s[i] = (payload[i] == 0)?"false":((payload[i] == 1)?"true":null); i++;
			}
			return s;
		}
		
		public virtual bool[] isTrue()
		{
			bool[] a = new bool[payload.Length];
			int i = 0;
			while (i < a.Length)
			{
				a[i] = (payload[i] == 1); i++;
			}
			return a;
		}
		
		public virtual bool[] isFalse()
		{
			bool[] a = new bool[payload.Length];
			int i = 0;
			while (i < a.Length)
			{
				a[i] = (payload[i] == 0); i++;
			}
			return a;
		}
		
		public override bool[] isNA()
		{
			bool[] a = new bool[payload.Length];
			int i = 0;
			while (i < a.Length)
			{
				a[i] = (payload[i] == 2); i++;
			}
			return a;
		}
	}
}
#endif