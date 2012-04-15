#if USE_R
// JRclient library - client interface to Rserve, see http://www.rosuda.org/Rserve/
// Copyright (C) 2004 Simon Urbanek
// --- for licensing information see LICENSE file in the original JRclient distribution ---
using System;
using org.rosuda.REngine;
using org.rosuda.REngine.Rserve.protocol;
namespace org.rosuda.REngine.Rserve
{
	
	/// <summary><b>RFileInputStream</b> is an {@link InputStream} to transfer files
	/// from <b>Rserve</b> server to the client. It is used very much like
	/// a {@link FileInputStream}. Currently mark and seek is not supported.
	/// The current implementation is also "one-shot" only, that means the file
	/// can be read only once.
	/// </summary>
	/// <version>  $Id: RFileInputStream.java 2743 2007-05-04 16:42:17Z urbanek $
	/// </version>
	public class RFileInputStream:System.IO.Stream
	{
		/// <summary>RTalk class to use for communication with the Rserve </summary>
		internal RTalk rt;
		/// <summary>set to <code>true</code> when {@link #close} was called.
		/// Any subsequent read requests on closed stream  result in an 
		/// {@link IOException} or error result 
		/// </summary>
		internal bool closed;
		/// <summary>set to <code>true</code> once EOF is reached - or more specifically
		/// the first time remore fread returns OK and 0 bytes 
		/// </summary>
		internal bool eof;
		
		/// <summary>tries to open file on the R server, using specified {@link RTalk} object
		/// and filename. Be aware that the filename has to be specified in host
		/// format (which is usually unix). In general you should not use directories
		/// since Rserve provides an own directory for every connection. Future Rserve
		/// servers may even strip all directory navigation characters for security
		/// purposes. Therefore only filenames without path specification are considered
		/// valid, the behavior in respect to absolute paths in filenames is undefined. 
		/// </summary>
		internal RFileInputStream(RTalk rti, System.String fn)
		{
			rt = rti;
			RPacket rp = rt.request(RTalk.CMD_openFile, fn);
			if (rp == null || !rp.Ok)
				throw new System.IO.IOException((rp == null)?"Connection to Rserve failed":("Request return code: " + rp.Stat));
			closed = false; eof = false;
		}
		
		/// <summary>reads one byte from the file. This function should be avoided, since
		/// {@link RFileInputStream} provides no buffering. This means that each
		/// call to this function leads to a complete packet exchange between
		/// the server and the client. Use {@link #read(byte[],int,int)} instead
		/// whenever possible. In fact this function calls <code>#read(b,0,1)</code>.
		/// </summary>
		/// <returns> -1 on any failure, or the acquired byte (0..255) on success 
		/// </returns>
		public override int ReadByte()
		{
			byte /* sbyte */[] b = new byte /* sbyte */[1];
			if (read(b, 0, 1) < 1)
				return - 1;
			return b[0];
		}
		
		/// <summary>Reads specified number of bytes (or less) from the remote file.</summary>
		/// <param name="b">buffer to store the read bytes
		/// </param>
		/// <param name="off">offset where to strat filling the buffer
		/// </param>
		/// <param name="len">maximal number of bytes to read
		/// </param>
		/// <returns> number of bytes read or -1 if EOF reached
		/// </returns>
		//UPGRADE_NOTE: The equivalent of method 'java.io.InputStream.read' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public int read(byte /* sbyte */[] b, int off, int len)
		{
			if (closed)
				throw new System.IO.IOException("File is not open");
			if (eof)
				return - 1;
			RPacket rp = rt.request(RTalk.CMD_readFile, len);
			if (rp == null || !rp.Ok)
				throw new System.IO.IOException((rp == null)?"Connection to Rserve failed":("Request return code: " + rp.Stat));
			byte /* sbyte */[] rd = rp.Cont;
			if (rd == null)
			{
				eof = true;
				return - 1;
			}
			;
			int i = 0;
			while (i < rd.Length)
			{
				b[off + i] = rd[i]; i++;
			} ;
			return rd.Length;
		}
		
		/// <summary>close stream - is not related to the actual RConnection, calling
		/// close does not close the RConnection
		/// </summary>
		public override void  Close()
		{
			RPacket rp = rt.request(RTalk.CMD_closeFile, (byte /* sbyte */[]) null);
			if (rp == null || !rp.Ok)
				throw new System.IO.IOException((rp == null)?"Connection to Rserve failed":("Request return code: " + rp.Stat));
			closed = true;
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
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
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		public override void  Write(System.Byte[] buffer, System.Int32 offset, System.Int32 count)
		{
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