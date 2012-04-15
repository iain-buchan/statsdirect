#if USE_R
// JRclient library - client interface to Rserve, see http://www.rosuda.org/Rserve/
// Copyright (C) 2003 Simon Urbanek
// --- for licensing information see LICENSE file in the original JRclient distribution ---
//
//  RFileOutputStream.java
//
//  Created by Simon Urbanek on Wed Oct 22 2003.
//
using System;
using org.rosuda.REngine;
using org.rosuda.REngine.Rserve.protocol;
namespace org.rosuda.REngine.Rserve
{
	
	/// <summary><b>RFileOutputStream</b> is an {@link OutputStream} to transfer files
	/// from the client to <b>Rserve</b> server. It is used very much like
	/// a {@link FileOutputStream}. Currently mark and seek is not supported.
	/// The current implementation is also "one-shot" only, that means the file
	/// can be written only once.
	/// </summary>
	/// <version>  $Id: RFileOutputStream.java 2743 2007-05-04 16:42:17Z urbanek $
	/// </version>
	
	public class RFileOutputStream:System.IO.Stream
	{
		/// <summary>RTalk class to use for communication with the Rserve </summary>
		internal RTalk rt;
		/// <summary>set to <code>true</code> when {@link #close} was called.
		/// Any subsequent read requests on closed stream  result in an
		/// {@link IOException} or error result 
		/// </summary>
		internal bool closed;
		
		/// <summary>tries to create a file on the R server, using specified {@link RTalk} object
		/// and filename. Be aware that the filename has to be specified in host
		/// format (which is usually unix). In general you should not use directories
		/// since Rserve provides an own directory for every connection. Future Rserve
		/// servers may even strip all directory navigation characters for security
		/// purposes. Therefore only filenames without path specification are considered
		/// valid, the behavior in respect to absolute paths in filenames is undefined.
		/// </summary>
		/// <param name="rti">RTalk object for communication with Rserve
		/// </param>
		/// <param name="fb">filename of the file to create (existing file will be overwritten)
		/// </param>
		internal RFileOutputStream(RTalk rti, System.String fn)
		{
			rt = rti;
			RPacket rp = rt.request(RTalk.CMD_createFile, fn);
			if (rp == null || !rp.Ok)
				throw new System.IO.IOException((rp == null)?"Connection to Rserve failed":("Request return code: " + rp.Stat));
			closed = false;
		}
		/**
		/// <summary>writes one byte to the file. This function should be avoided, since
		/// {@link RFileOutputStream} provides no buffering. This means that each
		/// call to this function leads to a complete packet exchange between
		/// the server and the client. Use {@link #write(byte[])} instead
		/// whenever possible. In fact this function calls <code>write(b,0,1)</code>.
		/// </summary>
		/// <param name="b">byte to write
		/// </param>
		public  void  WriteByte(int b)
		{
			byte[] ba = new byte[1];
			Write(SupportClass.ToByteArray(ba), 0, 1);
		}
         * **/
		public override  void  WriteByte(byte b)
		{
            byte[] ba = new byte[1];
            ba[0] = b;
            Write(ba, 0, 1);
        }
		
		/// <summary>writes the content of b into the file. This methods is equivalent to calling <code>write(b,0,b.length)</code>.</summary>
		/// <param name="b">content to write
		/// </param>
		public void  write(byte /* sbyte */[] b)
		{
			Write(SupportClass.ToByteArray(b), 0, b.Length);
		}
		
		/// <summary>Writes specified number of bytes to the remote file.</summary>
		/// <param name="b">buffer containing the bytes to write
		/// </param>
		/// <param name="off">offset where to start
		/// </param>
		/// <param name="len">number of bytes to write
		/// </param>
		public override void  Write(byte[] b, int off, int len)
		{
			if (closed)
				throw new System.IO.IOException("File is not open");
			if (len < 0)
				len = 0;
			bool isLarge = (len > 0xfffff0);
			byte /* sbyte */[] hdr = RTalk.newHdr(RTalk.DT_BYTESTREAM, len);
			RPacket rp = rt.request(RTalk.CMD_writeFile, hdr, b, off, len);
			if (rp == null || !rp.Ok)
				throw new System.IO.IOException((rp == null)?"Connection to Rserve failed":("Request return code: " + rp.Stat));
		}
		
		/// <summary>close stream - is not related to the actual RConnection, calling
		/// close does not close the RConnection.
		/// </summary>
		public override void  Close()
		{
			RPacket rp = rt.request(RTalk.CMD_closeFile, (byte /* sbyte */[]) null);
			if (rp == null || !rp.Ok)
				throw new System.IO.IOException((rp == null)?"Connection to Rserve failed":("Request return code: " + rp.Stat));
			closed = true;
		}
		
		/// <summary>currently (Rserve 0.3) there is no way to force flush on the remote side, hence this function is noop. Future versions of Rserve may support this feature though. At any rate, it is safe to call it. </summary>
		public override void  Flush()
		{
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override System.Int64 Seek(System.Int64 offset, System.IO.SeekOrigin origin)
		{
			return 0;
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override void  SetLength(System.Int64 value)
		{
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override System.Int32 Read(System.Byte[] buffer, System.Int32 offset, System.Int32 count)
		{
			return 0;
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override System.Boolean CanRead
		{
			get
			{
				return false;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override System.Boolean CanSeek
		{
			get
			{
				return false;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override System.Boolean CanWrite
		{
			get
			{
				return false;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override System.Int64 Length
		{
			get
			{
				return 0;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override System.Int64 Position
		{
			get
			{
				return 0;
			}
			
			set
			{
			}
			
		}
	}
}
#endif