#if USE_R
using System;
using System.Runtime.InteropServices;
using RPacket = org.rosuda.REngine.Rserve.protocol.RPacket;
using RTalk = org.rosuda.REngine.Rserve.protocol.RTalk;
namespace org.rosuda.REngine.Rserve
{
	
	[Serializable]
	public class RSession
	{
		// serial version UID should only change if method signatures change
		// significantly enough that previous versions cannot be used with
		// current versions
		private const long serialVersionUID = - 7048099825974875604L;
		
		internal System.String host;
		internal int port;
		internal byte /* sbyte */[] key;
		
		[NonSerialized]
		internal RPacket attachPacket = null; // response on session attach
		internal int rsrvVersion;
		
		protected internal RSession()
		{
			// default no-args constructor for serialization
		}
		
		internal RSession(RConnection c, RPacket p)
		{
			this.host = c.host;
			this.rsrvVersion = c.rsrvVersion;
			byte /* sbyte */[] ct = p.Cont;
			if (ct == null || ct.Length != 32 + 3 * 4)
				throw new RserveException(c, "Invalid response to session detach request.");
			this.port = RTalk.getInt(ct, 4);
			this.key = new byte /* sbyte */[32];
			Array.Copy(ct, 12, this.key, 0, 32);
		}
		
		/// <summary>attach/resume this session </summary>
		public virtual RConnection attach()
		{
			RConnection c = new RConnection(this);
			attachPacket = c.rt.request(- 1);
			return c;
		}
	}
}
#endif