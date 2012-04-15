#if USE_R
// JRclient library - client interface to Rserve, see http://www.rosuda.org/Rserve/
// Copyright (C) 2004 Simon Urbanek
// --- for licensing information see LICENSE file in the original JRclient distribution ---
//
//  RserveException.java
//
//  Created by Simon Urbanek on Mon Aug 18 2003.
//
//  $Id: RserveException.java 2743 2007-05-04 16:42:17Z urbanek $
//
using System;
using RPacket = org.rosuda.REngine.Rserve.protocol.RPacket;
using RTalk = org.rosuda.REngine.Rserve.protocol.RTalk;
namespace org.rosuda.REngine.Rserve
{
	
	[Serializable]
	public class RserveException:System.Exception
	{
		public override System.String Message
		{
			get
			{
				return base.Message + ((reqReturnCode != - 1)?", request status: " + getRequestErrorDescription():"");
			}
			
		}
		virtual public int RequestReturnCode
		{
			get
			{
				return reqReturnCode;
			}
			
		}
		protected internal RConnection conn;
		protected internal System.String err;
		protected internal int reqReturnCode;
		
		public virtual System.String getRequestErrorDescription()
		{
			return getRequestErrorDescription(reqReturnCode);
		}
		
		public virtual System.String getRequestErrorDescription(int code)
		{
			switch (code)
			{
				
				case 0:  return "no error";
				
				case 2:  return "R parser: input incomplete";
				
				case 3:  return "R parser: syntax error";
				
				case RTalk.ERR_auth_failed:  return "authorization failed";
				
				case RTalk.ERR_conn_broken:  return "connection broken";
				
				case RTalk.ERR_inv_cmd:  return "invalid command";
				
				case RTalk.ERR_inv_par:  return "invalid parameter";
				
				case RTalk.ERR_IOerror:  return "I/O error on the server";
				
				case RTalk.ERR_not_open:  return "connection is not open";
				
				case RTalk.ERR_access_denied:  return "access denied (local to the server)";
				
				case RTalk.ERR_unsupported_cmd:  return "unsupported command";
				
				case RTalk.ERR_unknown_cmd:  return "unknown command";
				
				case RTalk.ERR_data_overflow:  return "data overflow, incoming data too big";
				
				case RTalk.ERR_object_too_big:  return "evaluation successful, but returned object is too big to transport";
				
				case RTalk.ERR_out_of_mem:  return "FATAL: Rserve ran out of memory, closing connection";
				
				case RTalk.ERR_session_busy:  return "session is busy";
				
				case RTalk.ERR_detach_failed:  return "session detach failed";
				}
			return "error code: " + code;
		}
		
		public RserveException(RConnection c, System.String msg):this(c, msg, - 1)
		{
		}
		
		public RserveException(RConnection c, System.String msg, int requestReturnCode):base(msg)
		{
			conn = c; reqReturnCode = requestReturnCode;
			if (c != null)
			{
				c.lastError = Message;
			}
		}
		
		public RserveException(RConnection c, System.String msg, RPacket p):this(c, msg, (p == null)?- 1:p.Stat)
		{
		}
	}
}
#endif