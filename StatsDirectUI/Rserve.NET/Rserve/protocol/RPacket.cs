#if USE_R
namespace org.rosuda.REngine.Rserve.protocol
{
	
	// JRclient library - client interface to Rserve, see http://www.rosuda.org/Rserve/
	// Copyright (C) 2004 Simon Urbanek
	// --- for licensing information see LICENSE file in the original JRclient distribution ---
	
	/// <summary>small class encapsulating packets from/to Rserv</summary>
	/// <version>  $Id: RPacket.java 2743 2007-05-04 16:42:17Z urbanek $
	/// </version>
	public class RPacket
	{
		/// <summary>get command</summary>
		/// <returns> command 
		/// </returns>
		virtual public int Cmd
		{
			get
			{
				return cmd;
			}
			
		}
		/// <summary>check last response for RESP_OK</summary>
		/// <returns> <code>true</code> if last response was OK 
		/// </returns>
		virtual public bool Ok
		{
			get
			{
				return ((cmd & 15) == 1);
			}
			
		}
		/// <summary>check last response for RESP_ERR</summary>
		/// <returns> <code>true</code> if last response was ERROR 
		/// </returns>
		virtual public bool Error
		{
			get
			{
				return ((cmd & 15) == 2);
			}
			
		}
		/// <summary>get status code of last response</summary>
		/// <returns> status code returned on last response 
		/// </returns>
		virtual public int Stat
		{
			get
			{
				return ((cmd >> 24) & 127);
			}
			
		}
		/// <summary>get content</summary>
		/// <returns> inner package content 
		/// </returns>
		virtual public byte /* sbyte */[] Cont
		{
			get
			{
				return cont;
			}
			
		}
		internal int cmd;
		internal byte /* sbyte */[] cont;
		
		/// <summary>construct new packet</summary>
		/// <param name="Rcmd">command
		/// </param>
		/// <param name="Rcont">content 
		/// </param>
		public RPacket(int Rcmd, byte /* sbyte */[] Rcont)
		{
			cmd = Rcmd; cont = Rcont;
		}
		
		public override System.String ToString()
		{
			return "RPacket[cmd=" + cmd + ",len=" + ((cont == null)?"<null>":("" + cont.Length)) + "]";
		}
	}
}
#endif