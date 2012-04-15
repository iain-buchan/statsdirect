#if USE_R
// JRclient library - client interface to Rserve, see http://www.rosuda.org/Rserve/
// Copyright (C) 2004-08 Simon Urbanek
// --- for licensing information see LICENSE file in the original JRclient distribution ---
using System;
using org.rosuda.REngine;
using org.rosuda.REngine.Rserve.protocol;
namespace org.rosuda.REngine.Rserve
{
	
	/// <summary>class providing TCP/IP connection to an Rserve</summary>
	/// <version>  $Id: RConnection.java 2864 2008-03-19 15:46:43Z urbanek $
	/// </version>
	public class RConnection:org.rosuda.REngine.REngine
	{
		/// <summary>get server version as reported during the handshake.</summary>
		/// <returns> server version as integer (Rsrv0100 will return 100) 
		/// </returns>
		virtual public int ServerVersion
		{
			get
			{
				return rsrvVersion;
			}
			
		}
		/// <summary>Sets send buffer size of the Rserve (in bytes) for the current connection. All responses send by Rserve are stored in the send buffer before transmitting. This means that any objects you want to get from the Rserve need to fit into that buffer. By default the size of the send buffer is 2MB. If you need to receive larger objects from Rserve, you will need to use this function to enlarge the buffer. In order to save memory, you can also reduce the buffer size once it's not used anymore. Currently the buffer size is only limited by the memory available and/or 1GB (whichever is smaller). Current Rserve implementations won't go below buffer sizes of 32kb though. If the specified buffer size results in 'out of memory' on the server, the corresponding error is sent and the connection is terminated.<br>
		/// <i>Note:</i> This command may go away in future versions of Rserve which will use dynamic send buffer allocation.
		/// </summary>
		/// <param name="sbs">send buffer size (in bytes) min=32k, max=1GB
		/// </param>
		virtual public long SendBufferSize
		{
			set
			{
				if (!connected || rt == null)
					throw new RserveException(this, "Not connected");
				
				RPacket rp = rt.request(RTalk.CMD_setBufferSize, (int) value);
				if (rp != null && rp.Ok)
					return ;
				throw new RserveException(this, "setSendBufferSize failed", rp);
			}
			
		}
		/// <summary>check connection state. Note that currently this state is not checked on-the-spot,
		/// that is if connection went down by an outside event this is not reflected by
		/// the flag
		/// </summary>
		/// <returns> <code>true</code> if this connection is alive 
		/// </returns>
		virtual public bool Connected
		{
			get
			{
				return connected;
			}
			
		}
		/// <summary>get last error string</summary>
		/// <returns> last error string 
		/// </returns>
		virtual public System.String LastError
		{
			get
			{
				return lastError;
			}
			
		}
		/// <summary>last error string </summary>
		internal System.String lastError = null;
		internal System.Net.Sockets.TcpClient s;
		internal bool connected = false;
		internal System.IO.Stream is_Renamed;
		internal System.IO.Stream os;
		internal bool authReq = false;
		internal int authType = AT_plain;
		internal System.String Key = null;
		internal RTalk rt = null;
		
		internal System.String host;
		internal int port;
		
		/// <summary>This static variable specifies the character set used to encode string for transfer. Under normal circumstances there should be no reason for changing this variable. The default is UTF-8, which makes sure that 7-bit ASCII characters are sent in a backward-compatible fashion. Currently (Rserve 0.1-7) there is no further conversion on Rserve's side, i.e. the strings are passed to R without re-coding. If necessary the setting should be changed <u>before</u> connecting to the Rserve in case later Rserves will provide a possibility of setting the encoding during the handshake. </summary>
		public static System.String transferCharset = "UTF-8";
		
		/// <summary>authorization type: plain text </summary>
		public const int AT_plain = 0;
		/// <summary>authorization type: unix crypt </summary>
		public const int AT_crypt = 1;
		
		/// <summary>version of the server (as reported in IDstring just after Rsrv) </summary>
		protected internal int rsrvVersion;
		
		/// <summary>make a new local connection on default port (6311) </summary>
		public RConnection():this("127.0.0.1", 6311)
		{
		}
		
		/// <summary>make a new connection to specified host on default port (6311)</summary>
		/// <param name="host">host name/IP
		/// </param>
		public RConnection(System.String host):this(host, 6311)
		{
		}
		
		/// <summary>make a new connection to specified host and given port.
		/// Make sure you check {@link #isConnected} to ensure the connection was successfully created.
		/// </summary>
		/// <param name="host">host name/IP
		/// </param>
		/// <param name="port">TCP port
		/// </param>
		public RConnection(System.String host, int port):this(host, port, null)
		{
		}
		
		/// <summary>restore a connection based on a previously detached session</summary>
		/// <param name="session">detached session object 
		/// </param>
		internal RConnection(RSession session):this(null, 0, session)
		{
		}
		
		internal RConnection(System.String host, int port, RSession session)
		{
			try
			{
				if (connected)
					s.Close();
				s = null;
			}
			catch (System.Exception e)
			{
				throw new RserveException(this, "Cannot connect: " + e.Message);
			}
			if (session != null)
			{
				host = session.host;
				port = session.port;
			}
			connected = false;
			this.host = host;
			this.port = port;
			try
			{
				s = new System.Net.Sockets.TcpClient(host, port);
				// disable Nagle's algorithm since we really want immediate replies
				s.NoDelay = true;
			}
			catch (System.Exception sce)
			{
				throw new RserveException(this, "Cannot connect: " + sce.Message);
			}
			try
			{
				is_Renamed = s.GetStream();
				os = s.GetStream();
			}
			catch (System.Exception gse)
			{
				throw new RserveException(this, "Cannot get io stream: " + gse.Message);
			}
			rt = new RTalk(is_Renamed, os);
			if (session == null)
			{
				byte /* sbyte */[] IDs = new byte /* sbyte */[32];
				int n = - 1;
				try
				{
					n = SupportClass.ReadInput(is_Renamed, IDs, 0, IDs.Length);
				}
				catch (System.Exception sre)
				{
					throw new RserveException(this, "Error while receiving data: " + sre.Message);
				}
				try
				{
					if (n != 32)
					{
						throw new RserveException(this, "Handshake failed: expected 32 bytes header, got " + n);
					}
					System.String ids = new System.String(SupportClass.ToCharArray(SupportClass.ToByteArray(IDs)));
					if (String.CompareOrdinal(ids.Substring(0, (4) - (0)), "Rsrv") != 0)
						throw new RserveException(this, "Handshake failed: Rsrv signature expected, but received \"" + ids + "\" instead.");
					try
					{
						rsrvVersion = System.Int32.Parse(ids.Substring(4, (8) - (4)));
					}
					catch (System.Exception)
					{
					}
					// we support (knowingly) up to 103
					if (rsrvVersion > 103)
						throw new RserveException(this, "Handshake failed: The server uses more recent protocol than this client.");
					if (String.CompareOrdinal(ids.Substring(8, (12) - (8)), "QAP1") != 0)
						throw new RserveException(this, "Handshake failed: unupported transfer protocol (" + ids.Substring(8, (12) - (8)) + "), I talk only QAP1.");
					for (int i = 12; i < 32; i += 4)
					{
						System.String attr = ids.Substring(i, (i + 4) - (i));
						if (String.CompareOrdinal(attr, "ARpt") == 0)
						{
							if (!authReq)
							{
								// this method is only fallback when no other was specified
								authReq = true;
								authType = AT_plain;
							}
						}
						if (String.CompareOrdinal(attr, "ARuc") == 0)
						{
							authReq = true;
							authType = AT_crypt;
						}
						if (attr[0] == 'K')
						{
							Key = attr.Substring(1, (3) - (1));
						}
					}
				}
				catch (RserveException innerX)
				{
					try
					{
						s.Close();
					}
					catch (System.Exception)
					{
					} is_Renamed = null; os = null; s = null;
					throw innerX;
				}
			}
			else
			{
				// we have a session to take care of
				try
				{
					os.Write(SupportClass.ToByteArray(session.key), 0, 32);
				}
				catch (System.Exception sre)
				{
					throw new RserveException(this, "Error while sending session key: " + sre.Message);
				}
				rsrvVersion = session.rsrvVersion;
			}
			connected = true;
			lastError = "OK";
		}
		
		~RConnection()
		{
			close();
			is_Renamed = null; is_Renamed = null;
		}
		
		/// <summary>closes current connection </summary>
		public virtual void  close()
		{
			try
			{
				if (s != null)
					s.Close();
				connected = false;
			}
			catch (System.Exception)
			{
			}
		}
		
		/// <summary>evaluates the given command, but does not fetch the result (useful for assignment
		/// operations)
		/// </summary>
		/// <param name="cmd">command/expression string 
		/// </param>
		public virtual void  voidEval(System.String cmd)
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			RPacket rp = rt.request(RTalk.CMD_voidEval, cmd + "\n");
			if (rp != null && rp.Ok)
				return ;
			throw new RserveException(this, "voidEval failed", rp);
		}
		
		/// <summary>evaluates the given command, detaches the session (see @link{detach()}) and closes connection while the command is being evaluted (requires Rserve 0.4+).
		/// Note that a session cannot be attached again until the commad was successfully processed. Techincally the session is put into listening mode while the command is being evaluated but accept is called only after the command was evaluated. One commonly used techique to monitor detached working sessions is to use second connection to poll the status (e.g. create a temporary file and return the full path before detaching thus allowing new connections to read it).
		/// </summary>
		/// <param name="cmd">command/expression string
		/// </param>
		/// <returns> session object that can be use to attach back to the session once the command completed 
		/// </returns>
		public virtual RSession voidEvalDetach(System.String cmd)
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			RPacket rp = rt.request(RTalk.CMD_detachedVoidEval, cmd + "\n");
			if (rp == null || !rp.Ok)
				throw new RserveException(this, "detached void eval failed", rp);
			RSession s = new RSession(this, rp);
			close();
			return s;
		}
		
		internal virtual REXP parseEvalResponse(RPacket rp)
		{
			int rxo = 0;
			byte /* sbyte */[] pc = rp.Cont;
			if (rsrvVersion > 100)
			{
				/* since 0101 eval responds correctly by using DT_SEXP type/len header which is 4 bytes long */
				rxo = 4;
				/* we should check parameter type (should be DT_SEXP) and fail if it's not */
				if (pc[0] != RTalk.DT_SEXP && pc[0] != (RTalk.DT_SEXP | RTalk.DT_LARGE))
					throw new RserveException(this, "Error while processing eval output: SEXP (type " + RTalk.DT_SEXP + ") expected but found result type " + pc[0] + ".");
				if (pc[0] == (RTalk.DT_SEXP | RTalk.DT_LARGE))
					rxo = 8; // large data need skip of 8 bytes
				/* warning: we are not checking or using the length - we assume that only the one SEXP is returned. This is true for the current CMD_eval implementation, but may not be in the future. */
			}
			if (pc.Length > rxo)
			{
				try
				{
					REXPFactory rx = new REXPFactory();
					rx.parseREXP(pc, rxo);
					return rx.REXP;
				}
				catch (REXPMismatchException me)
				{
					SupportClass.WriteStackTrace(me, Console.Error);
					throw new RserveException(this, "Error when parsing response: " + me.Message);
				}
			}
			return null;
		}
		
		/// <summary>evaluates the given command and retrieves the result</summary>
		/// <param name="cmd">command/expression string
		/// </param>
		/// <returns> R-xpression or <code>null</code> if an error occured 
		/// </returns>
		public virtual REXP eval(System.String cmd)
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			RPacket rp = rt.request(RTalk.CMD_eval, cmd + "\n");
			if (rp != null && rp.Ok)
				return parseEvalResponse(rp);
			throw new RserveException(this, "eval failed", rp);
		}
		
		/// <summary>assign a string value to a symbol in R. The symbol is created if it doesn't exist already.</summary>
		/// <param name="sym">symbol name. Currently assign uses CMD_setSEXP command of Rserve, i.e. the symbol value is NOT parsed. It is the responsibility of the user to make sure that the symbol name is valid in R (recall the difference between a symbol and an expression!). In fact R will always create the symbol, but it may not be accessible (examples: "bar\nfoo" or "bar$foo").
		/// </param>
		/// <param name="ct">contents
		/// </param>
		public virtual void  assign(System.String sym, System.String ct)
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			byte /* sbyte */[] symn = SupportClass.ToSByteArray(SupportClass.ToByteArray(sym));
			byte /* sbyte */[] ctn = SupportClass.ToSByteArray(SupportClass.ToByteArray(ct));
			int sl = symn.Length + 1;
			int cl = ctn.Length + 1;
			if ((sl & 3) > 0)
				sl = (sl & 0xfffffc) + 4; // make sure the symbol length is divisible by 4
			if ((cl & 3) > 0)
				cl = (cl & 0xfffffc) + 4; // make sure the content length is divisible by 4
			byte /* sbyte */[] rq = new byte /* sbyte */[sl + 4 + cl + 4];
			int ic;
			for (ic = 0; ic < symn.Length; ic++)
				rq[ic + 4] = symn[ic];
			while (ic < sl)
			{
				rq[ic + 4] = 0; ic++;
			}
			for (ic = 0; ic < ctn.Length; ic++)
				rq[ic + sl + 8] = ctn[ic];
			while (ic < cl)
			{
				rq[ic + sl + 8] = 0; ic++;
			}
			RTalk.setHdr(RTalk.DT_STRING, sl, rq, 0);
			RTalk.setHdr(RTalk.DT_STRING, cl, rq, sl + 4);
			RPacket rp = rt.request(RTalk.CMD_setSEXP, rq);
			if (rp != null && rp.Ok)
				return ;
			throw new RserveException(this, "assign failed", rp);
		}
		
		/// <summary>assign a content of a REXP to a symbol in R. The symbol is created if it doesn't exist already.</summary>
		/// <param name="sym">symbol name. Currently assign uses CMD_setSEXP command of Rserve, i.e. the symbol value is NOT parsed. It is the responsibility of the user to make sure that the symbol name is valid in R (recall the difference between a symbol and an expression!). In fact R will always create the symbol, but it may not be accessible (examples: "bar\nfoo" or "bar$foo").
		/// </param>
		/// <param name="rexp">contents
		/// </param>
		public virtual void  assign(System.String sym, REXP rexp)
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			try
			{
				REXPFactory r = new REXPFactory(rexp);
				int rl = r.BinaryLength;
				byte /* sbyte */[] symn = SupportClass.ToSByteArray(SupportClass.ToByteArray(sym));
				int sl = symn.Length + 1;
				if ((sl & 3) > 0)
					sl = (sl & 0xfffffc) + 4; // make sure the symbol length is divisible by 4
				byte /* sbyte */[] rq = new byte /* sbyte */[sl + rl + ((rl > 0xfffff0)?12:8)];
				int ic;
				for (ic = 0; ic < symn.Length; ic++)
					rq[ic + 4] = symn[ic];
				while (ic < sl)
				{
					rq[ic + 4] = 0; ic++;
				} ; // pad with 0
				RTalk.setHdr(RTalk.DT_STRING, sl, rq, 0);
				RTalk.setHdr(RTalk.DT_SEXP, rl, rq, sl + 4);
				r.getBinaryRepresentation(rq, sl + ((rl > 0xfffff0)?12:8));
				RPacket rp = rt.request(RTalk.CMD_setSEXP, rq);
				if (rp != null && rp.Ok)
					return ;
				throw new RserveException(this, "assign failed", rp);
			}
			catch (REXPMismatchException me)
			{
				throw new RserveException(this, "Error creating binary representation: " + me.Message);
			}
		}
		
		/// <summary>open a file on the Rserve for reading</summary>
		/// <param name="fn">file name. should not contain any path delimiters, since Rserve may restrict the access to local working directory.
		/// </param>
		/// <returns> input stream to be used for reading. Note that the stream is read-once only, there is no support for seek or rewind. 
		/// </returns>
		public virtual RFileInputStream openFile(System.String fn)
		{
			return new RFileInputStream(rt, fn);
		}
		
		/// <summary>create a file on the Rserve for writing</summary>
		/// <param name="fn">file name. should not contain any path delimiters, since Rserve may restrict the access to local working directory.
		/// </param>
		/// <returns> output stream to be used for writinging. Note that the stream is write-once only, there is no support for seek or rewind. 
		/// </returns>
		public virtual RFileOutputStream createFile(System.String fn)
		{
			return new RFileOutputStream(rt, fn);
		}
		
		/// <summary>remove a file on the Rserve</summary>
		/// <param name="fn">file name. should not contain any path delimiters, since Rserve may restrict the access to local working directory. 
		/// </param>
		public virtual void  removeFile(System.String fn)
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			RPacket rp = rt.request(RTalk.CMD_removeFile, fn);
			if (rp != null && rp.Ok)
				return ;
			throw new RserveException(this, "removeFile failed", rp);
		}
		
		/// <summary>shutdown remote Rserve. Note that some Rserves cannot be shut down from the client side. </summary>
		public virtual void  shutdown()
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			
			RPacket rp = rt.request(RTalk.CMD_shutdown);
			if (rp != null && rp.Ok)
				return ;
			throw new RserveException(this, "shutdown failed", rp);
		}
		
		/// <summary>login using supplied user/pwd. Note that login must be the first
		/// command if used
		/// </summary>
		/// <param name="user">username
		/// </param>
		/// <param name="pwd">password 
		/// </param>
		public virtual void  login(System.String user, System.String pwd)
		{
			if (!authReq)
				return ;
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			if (authType == AT_crypt)
			{
				if (Key == null)
					Key = "rs";
				RPacket rp = rt.request(RTalk.CMD_login, user + "\n" + jcrypt.crypt(Key, pwd));
				if (rp != null && rp.Ok)
					return ;
				try
				{
					s.Close();
				}
				catch (System.Exception)
				{
				}
				is_Renamed = null; os = null; s = null; connected = false;
				throw new RserveException(this, "login failed", rp);
			}
			RPacket rp2 = rt.request(RTalk.CMD_login, user + "\n" + pwd);
			if (rp2 != null && rp2.Ok)
				return ;
			try
			{
				s.Close();
			}
			catch (System.Exception)
			{
			}
			is_Renamed = null; os = null; s = null; connected = false;
			throw new RserveException(this, "login failed", rp2);
		}
		
		
		/// <summary>detaches the session and closes the connection (requires Rserve 0.4+). The session can be only resumed by calling @link{RSession.attach} </summary>
		public virtual RSession detach()
		{
			if (!connected || rt == null)
				throw new RserveException(this, "Not connected");
			RPacket rp = rt.request(RTalk.CMD_detachSession);
			if (rp == null || !rp.Ok)
				throw new RserveException(this, "Cannot detach", rp);
			RSession s = new RSession(this, rp);
			close();
			return s;
		}
		
		/// <summary>check authentication requirement sent by server</summary>
		/// <returns> <code>true</code> is server requires authentication. In such case first
		/// command after connecting must be {@link #login}. 
		/// </returns>
		public virtual bool needLogin()
		{
			return authReq;
		}
		
		//========= REngine interface API
		
		public override REXP parse(System.String text, bool resolve)
		{
			throw new REngineException(this, "Rserve doesn't support separate parsing step.");
		}
		public override REXP eval(REXP what, REXP where, bool resolve)
		{
			return new REXPNull();
		}
		public override REXP parseAndEval(System.String text, REXP where, bool resolve)
		{
			if (where != null)
				throw new REngineException(this, "Rserve doesn't support environments other than .GlobalEnv");
			try
			{
				return eval(text);
			}
			catch (RserveException re)
			{
				throw new REngineException(this, re.Message, re);
			}
		}
		
		/// <summary>assign into an environment</summary>
		/// <param name="symbol">symbol name
		/// </param>
		/// <param name="value">value to assign
		/// </param>
		/// <param name="env">environment to assign to 
		/// </param>
		public override void  assign(System.String symbol, REXP value_Renamed, REXP env)
		{
			if (env != null)
				throw new REngineException(this, "Rserve doesn't support environments other than .GlobalEnv");
			try
			{
				assign(symbol, value_Renamed);
			}
			catch (RserveException re)
			{
				throw new REngineException(this, re.Message);
			}
		}
		
		/// <summary>get a value from an environment</summary>
		/// <param name="symbol">symbol name
		/// </param>
		/// <param name="env">environment
		/// </param>
		/// <param name="resolve">resolve the resulting REXP or just return a reference		
		/// </param>
		/// <returns> value 
		/// </returns>
		public override REXP get_Renamed(System.String symbol, REXP env, bool resolve)
		{
			if (!resolve)
				throw new REngineException(this, "Rserve doesn't support references");
			try
			{
				return eval("get(\"" + symbol + "\")");
			}
			catch (RserveException re)
			{
				throw new REngineException(this, re.Message);
			}
		}
		
		/// <summary>fetch the contents of the given reference. The resulting REXP may never be REXPReference.</summary>
		/// <param name="ref">reference to resolve
		/// </param>
		/// <returns> resolved reference 
		/// </returns>
		public override REXP resolveReference(REXP ref_Renamed)
		{
			throw new REngineException(this, "Rserve doesn't support references");
		}
		
		public override REXP getParentEnvironment(REXP env, bool resolve)
		{
			throw new REngineException(this, "Rserve doesn't support environments other than .GlobalEnv");
		}
		
		public override REXP newEnvironment(REXP parent, bool resolve)
		{
			throw new REngineException(this, "Rserve doesn't support environments other than .GlobalEnv");
		}
	}
}
#endif