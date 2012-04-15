#if USE_R
// JRclient library - client interface to Rserve, see http://www.rosuda.org/Rserve/
// Copyright (C) 2004 Simon Urbanek
// --- for licensing information see LICENSE file in the original JRclient distribution ---
using System;
using RConnection = org.rosuda.REngine.Rserve.RConnection;
namespace org.rosuda.REngine.Rserve.protocol
{
	
	/// <summary>This class encapsulates the QAP1 protocol used by Rserv.
	/// it is independent of the underying protocol(s), therefore RTalk
	/// can be used over any transport layer
	/// <p>
	/// The current implementation supports long (0.3+/0102) data format only
	/// up to 32-bit and only for incoming packets.
	/// <p>
	/// </summary>
	/// <version>  $Id: RTalk.java 2743 2007-05-04 16:42:17Z urbanek $
	/// </version>
	public class RTalk
	{
		public const int DT_INT = 1;
		public const int DT_CHAR = 2;
		public const int DT_DOUBLE = 3;
		public const int DT_STRING = 4;
		public const int DT_BYTESTREAM = 5;
		public const int DT_SEXP = 10;
		public const int DT_ARRAY = 11;
		
		/// <summary>this is a flag saying that the contents is large (>0xfffff0) and hence uses 56-bit length field </summary>
		public const int DT_LARGE = 64;
		
		public const int CMD_login = 0x001;
		public const int CMD_voidEval = 0x002;
		public const int CMD_eval = 0x003;
		public const int CMD_shutdown = 0x004;
		public const int CMD_openFile = 0x010;
		public const int CMD_createFile = 0x011;
		public const int CMD_closeFile = 0x012;
		public const int CMD_readFile = 0x013;
		public const int CMD_writeFile = 0x014;
		public const int CMD_removeFile = 0x015;
		public const int CMD_setSEXP = 0x020;
		public const int CMD_assignSEXP = 0x021;
		
		public const int CMD_setBufferSize = 0x081;
		
		public const int CMD_detachSession = 0x030;
		public const int CMD_detachedVoidEval = 0x031;
		public const int CMD_attachSession = 0x032;
		
		// errors as returned by Rserve
		public const int ERR_auth_failed = 0x41;
		public const int ERR_conn_broken = 0x42;
		public const int ERR_inv_cmd = 0x43;
		public const int ERR_inv_par = 0x44;
		public const int ERR_Rerror = 0x45;
		public const int ERR_IOerror = 0x46;
		public const int ERR_not_open = 0x47;
		public const int ERR_access_denied = 0x48;
		public const int ERR_unsupported_cmd = 0x49;
		public const int ERR_unknown_cmd = 0x4a;
		public const int ERR_data_overflow = 0x4b;
		public const int ERR_object_too_big = 0x4c;
		public const int ERR_out_of_mem = 0x4d;
		public const int ERR_session_busy = 0x50;
		public const int ERR_detach_failed = 0x51;
		
		internal System.IO.Stream is_Renamed;
		internal System.IO.Stream os;
		
		/// <summary>constructor; parameters specify the streams</summary>
		/// <param name="sis">socket input stream
		/// </param>
		/// <param name="sos">socket output stream 
		/// </param>
		
		public RTalk(System.IO.Stream sis, System.IO.Stream sos)
		{
			is_Renamed = sis; os = sos;
		}
		
		/// <summary>writes bit-wise int to a byte buffer at specified position in Intel-endian form</summary>
		/// <param name="v">value to be written
		/// </param>
		/// <param name="buf">buffer
		/// </param>
		/// <param name="o">offset in the buffer to start at. An int takes always 4 bytes 
		/// </param>
		public static void  setInt(int v, byte /* sbyte */[] buf, int o)
		{
			buf[o] = (byte /* sbyte */) (v & 255); o++;
			buf[o] = (byte /* sbyte */) ((v & 0xff00) >> 8); o++;
			buf[o] = (byte /* sbyte */) ((v & 0xff0000) >> 16); o++;
			buf[o] = (byte /* sbyte */) ((v & unchecked((int) 0xff000000)) >> 24);
		}
		
		/// <summary>writes cmd/resp/type byte + 3/7 bytes len into a byte buffer at specified offset.</summary>
		/// <param name="ty">type/cmd/resp byte
		/// </param>
		/// <param name="len">length
		/// </param>
		/// <param name="buf">buffer
		/// </param>
		/// <param name="o">offset
		/// </param>
		/// <returns> offset in buf just after the header. Please note that since Rserve 0.3 the header can be either 4 or 8 bytes long, depending on the len parameter.
		/// </returns>
		public static int setHdr(int ty, int len, byte /* sbyte */[] buf, int o)
		{
			buf[o] = (byte /* sbyte */) ((ty & 255) | ((len > 0xfffff0)?DT_LARGE:0)); o++;
			buf[o] = (byte /* sbyte */) (len & 255); o++;
			buf[o] = (byte /* sbyte */) ((len & 0xff00) >> 8); o++;
			buf[o] = (byte /* sbyte */) ((len & 0xff0000) >> 16); o++;
			if (len > 0xfffff0)
			{
				// for large data we need to set the next 4 bytes as well
				buf[o] = (byte /* sbyte */) ((len & unchecked((int) 0xff000000)) >> 24); o++;
				buf[o] = 0; o++; // since len is int, we get 32-bits only
				buf[o] = 0; o++;
				buf[o] = 0; o++;
			}
			return o;
		}
		
		/// <summary>creates a new header according to the type and length of the parameter</summary>
		/// <param name="ty">type/cmd/resp byte
		/// </param>
		/// <param name="len">length 
		/// </param>
		public static byte /* sbyte */[] newHdr(int ty, int len)
		{
			byte /* sbyte */[] hdr = new byte /* sbyte */[(len > 0xfffff0)?8:4];
			setHdr(ty, len, hdr, 0);
			return hdr;
		}
		
		/// <summary>converts bit-wise stored int in Intel-endian form into Java int</summary>
		/// <param name="buf">buffer containg the representation
		/// </param>
		/// <param name="o">offset where to start (4 bytes will be used)
		/// </param>
		/// <returns> the int value. no bounds checking is done so you need to
		/// make sure that the buffer is big enough 
		/// </returns>
		public static int getInt(byte /* sbyte */[] buf, int o)
		{
			return ((buf[o] & 255) | ((buf[o + 1] & 255) << 8) | ((buf[o + 2] & 255) << 16) | ((buf[o + 3] & 255) << 24));
		}
		
		/// <summary>converts bit-wise stored length from a header. "long" format is supported up to 32-bit</summary>
		/// <param name="buf">buffer
		/// </param>
		/// <param name="o">offset of the header (length is at o+1)
		/// </param>
		/// <returns> length 
		/// </returns>
		public static int getLen(byte /* sbyte */[] buf, int o)
		{
			
			return ((buf[o] & 64) > 0)?((buf[o + 1] & 255) | ((buf[o + 2] & 255) << 8) | ((buf[o + 3] & 255) << 16) | ((buf[o + 4] & 255) << 24)):((buf[o + 1] & 255) | ((buf[o + 2] & 255) << 8) | ((buf[o + 3] & 255) << 16));
		}
		
		/// <summary>converts bit-wise Intel-endian format into long</summary>
		/// <param name="buf">buffer
		/// </param>
		/// <param name="o">offset (8 bytes will be used)
		/// </param>
		/// <returns> long value 
		/// </returns>
		public static long getLong(byte /* sbyte */[] buf, int o)
		{
			long low = ((long) getInt(buf, o)) & 0xffffffffL;
			long hi = ((long) getInt(buf, o + 4)) & 0xffffffffL;
			hi <<= 32; hi |= low;
			return hi;
		}
		
		public static void  setLong(long l, byte /* sbyte */[] buf, int o)
		{
			setInt((int) (l & unchecked((int) 0xffffffffL)), buf, o);
			setInt((int) (l >> 32), buf, o + 4);
		}
		
		/// <summary>sends a request with no attached parameters</summary>
		/// <param name="cmd">command
		/// </param>
		/// <returns> returned packet or <code>null</code> if something went wrong 
		/// </returns>
		public virtual RPacket request(int cmd)
		{
			byte /* sbyte */[] d = new byte /* sbyte */[0];
			return request(cmd, d);
		}
		
		/// <summary>sends a request with attached parameters</summary>
		/// <param name="cmd">command
		/// </param>
		/// <param name="cont">contents - parameters
		/// </param>
		/// <returns> returned packet or <code>null</code> if something went wrong 
		/// </returns>
		public virtual RPacket request(int cmd, byte[] cont)
		{
			return request(cmd, null, cont, 0, (cont == null)?0:cont.Length);
		}
		
		/// <summary>sends a request with attached prefix and  parameters. Both prefix and cont can be <code>null</code>. Effectively <code>request(a,b,null)</code> and <code>request(a,null,b)</code> are equivalent.</summary>
		/// <param name="cmd">command - a special command of -1 prevents request from sending anything
		/// </param>
		/// <param name="prefix">- this content is sent *before* cont. It is provided to save memory copy operations where a small header precedes a large data chunk (usually prefix conatins the parameter header and cont contains the actual data).
		/// </param>
		/// <param name="cont">contents
		/// </param>
		/// <param name="offset">offset in cont where to start sending (if <0 then 0 is assumed, if >cont.length then no cont is sent)
		/// </param>
		/// <param name="len">number of bytes in cont to send (it is clipped to the length of cont if necessary)
		/// </param>
		/// <returns> returned packet or <code>null</code> if something went wrong 
		/// </returns>
		public virtual RPacket request(int cmd, byte[] prefix, byte[] cont, int offset, int len)
		{
			if (cont != null)
			{
				if (offset >= cont.Length)
				{
					cont = null; len = 0;
				}
				else if (len > cont.Length - offset)
					len = cont.Length - offset;
			}
			if (offset < 0)
				offset = 0;
			if (len < 0)
				len = 0;
			int contlen = (cont == null)?0:len;
			if (prefix != null && prefix.Length > 0)
				contlen += prefix.Length;
			byte /* sbyte */[] hdr = new byte /* sbyte */[16];
			setInt(cmd, hdr, 0);
			setInt(contlen, hdr, 4);
			for (int i = 8; i < 16; i++)
				hdr[i] = 0;
			try
			{
				if (cmd != - 1)
				{
					if (os is org.rosuda.REngine.Rserve.RFileOutputStream)
						((org.rosuda.REngine.Rserve.RFileOutputStream) os).write(hdr);
					else
					{
						byte /* sbyte */[] temp_sbyteArray;
						temp_sbyteArray = hdr;
						os.Write(SupportClass.ToByteArray(temp_sbyteArray), 0, temp_sbyteArray.Length);
					}
					if (prefix != null && prefix.Length > 0)
						if (os is org.rosuda.REngine.Rserve.RFileOutputStream)
							((org.rosuda.REngine.Rserve.RFileOutputStream) os).write(prefix);
						else
						{
							byte /* sbyte */[] temp_sbyteArray2;
							temp_sbyteArray2 = prefix;
							os.Write(SupportClass.ToByteArray(temp_sbyteArray2), 0, temp_sbyteArray2.Length);
						}
					if (cont != null && cont.Length > 0)
						os.Write(SupportClass.ToByteArray(cont), offset, len);
				}
				
				byte /* sbyte */[] ih = new byte /* sbyte */[16];
				if (SupportClass.ReadInput(is_Renamed, ih, 0, ih.Length) != 16)
					return null;
				int rep = getInt(ih, 0);
				int rl = getInt(ih, 4);
				if (rl > 0)
				{
					byte /* sbyte */[] ct = new byte /* sbyte */[rl];
					int n = 0;
					while (n < rl)
					{
						int rd = is_Renamed is org.rosuda.REngine.Rserve.RFileInputStream?((org.rosuda.REngine.Rserve.RFileInputStream) is_Renamed).read(ct, n, rl - n):SupportClass.ReadInput(is_Renamed, ct, n, rl - n);
						n += rd;
					}
					return new RPacket(rep, ct);
				}
				return new RPacket(rep, null);
			}
			catch (System.Exception)
			{
				return null;
			}
		}
		
		/// <summary>sends a request with one string parameter attached</summary>
		/// <param name="cmd">command
		/// </param>
		/// <param name="par">parameter - length and DT_STRING will be prepended
		/// </param>
		/// <returns> returned packet or <code>null</code> if something went wrong 
		/// </returns>
		public virtual RPacket request(int cmd, System.String par)
		{
			try
			{
				byte /* sbyte */[] b = SupportClass.ToSByteArray(System.Text.Encoding.GetEncoding(RConnection.transferCharset).GetBytes(par));
				int sl = par.Length + 1;
				if ((sl & 3) > 0)
					sl = (sl & 0xfffffc) + 4; // make sure the length is divisible by 4
				byte /* sbyte */[] rq = new byte /* sbyte */[sl + 5];
				int i;
				for (i = 0; i < b.Length; i++)
					rq[i + 4] = b[i];
				while (i < sl)
				{
					// pad with 0
					rq[i + 4] = 0; i++;
				} ;
				setHdr(DT_STRING, sl, rq, 0);
				return request(cmd, rq);
			}
			catch (System.Exception)
			{
			}
			return null;
		}
		
		/// <summary>sends a request with one string parameter attached</summary>
		/// <param name="cmd">command
		/// </param>
		/// <param name="par">parameter of the type DT_INT
		/// </param>
		/// <returns> returned packet or <code>null</code> if something went wrong 
		/// </returns>
		public virtual RPacket request(int cmd, int par)
		{
			try
			{
				byte /* sbyte */[] rq = new byte /* sbyte */[8];
				setInt(par, rq, 4);
				setHdr(DT_INT, 4, rq, 0);
				return request(cmd, rq);
			}
			catch (System.Exception)
			{
			}
			return null;
		}
	}
}
#endif