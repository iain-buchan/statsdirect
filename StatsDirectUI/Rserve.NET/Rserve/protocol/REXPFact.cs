#if USE_R
// JRclient library - client interface to Rserve, see http://www.rosuda.org/Rserve/
// Copyright (C) 2004-8 Simon Urbanek
// --- for licensing information see LICENSE file in the original JRclient distribution ---
using System;
using org.rosuda.REngine;
using org.rosuda.REngine.Rserve;
namespace org.rosuda.REngine.Rserve.protocol
{
	
	/// <summary>representation of R-eXpressions in Java</summary>
	/// <version>  $Id: REXPFactory.java 2908 2008-07-15 14:34:59Z urbanek $
	/// </version>
	public class REXPFactory
	{
		virtual public REXP REXP
		{
			get
			{
				return cont;
			}
			
		}
		virtual public REXPList Attr
		{
			get
			{
				return (attr == null)?null:(REXPList) attr.cont;
			}
			
		}
		/// <summary>Calculates the length of the binary representation of the REXP including all headers. This is the amount of memory necessary to store the REXP via {@link #getBinaryRepresentation}.
		/// <p>Please note that currently only XT_[ARRAY_]INT, XT_[ARRAY_]DOUBLE and XT_[ARRAY_]STR are supported! All other types will return 4 which is the size of the header.
		/// </summary>
		/// <returns> length of the REXP including headers (4 or 8 bytes)
		/// </returns>
		virtual public int BinaryLength
		{
			get
			{
				int l = 0;
				int rxt = type;
				if (type == XT_LIST || type == XT_LIST_TAG || type == XT_LIST_NOTAG)
					rxt = (cont.asList() != null && cont.asList().Named)?XT_LIST_TAG:XT_LIST_NOTAG;
				//System.out.print("len["+xtName(type)+"/"+xtName(rxt)+"] ");
				if (type == XT_VECTOR_STR)
					rxt = XT_ARRAY_STR; // VECTOR_STR is broken right now
				
				/*
				if (type==XT_VECTOR && cont.asList()!=null && cont.asList().isNamed())
				setAttribute("names",new REXPString(cont.asList().keys()));
				*/
				
				bool hasAttr = false;
				REXPList a = Attr;
				RList al = null;
				if (a != null)
					al = a.asList();
				if (al != null && al.Count > 0)
					hasAttr = true;
				if (hasAttr)
					l += attr.BinaryLength;
				switch (rxt)
				{
					
					case XT_NULL: 
					case XT_S4: 
						break;
					
					case XT_INT:  l += 4; break;
					
					case XT_DOUBLE:  l += 8; break;
					
					case XT_RAW:  l += 4 + cont.asBytes().Length; if ((l & 3) > 0)
							l = l - (l & 3) + 4; break;
					
					case XT_STR: 
					case XT_SYMNAME: 
						l += ((cont == null)?1:(cont.asString().Length + 1));
						if ((l & 3) > 0)
							l = l - (l & 3) + 4;
						break;
					
					case XT_ARRAY_INT:  l += cont.asIntegers().Length * 4; break;
					
					case XT_ARRAY_DOUBLE:  l += cont.asDoubles().Length * 8; break;
					
					case XT_ARRAY_CPLX:  l += cont.asDoubles().Length * 8; break;
					
					case XT_LIST_TAG: 
					case XT_LIST_NOTAG: 
					case XT_LANG_TAG: 
					case XT_LANG_NOTAG: 
					case XT_LIST: 
					case XT_VECTOR: 
					{
						//UPGRADE_NOTE: Final was removed from the declaration of 'lst '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
						RList lst = cont.asList();
						int i = 0;
						while (i < lst.Count)
						{
							REXP x = lst.at(i);
							l += ((x == null)?4:(new REXPFactory(x).BinaryLength));
							if (rxt == XT_LIST_TAG)
							{
								int pl = l;
								System.String s = lst.keyAt(i);
								l += 4; // header for a symbol
								l += ((s == null)?1:(s.Length + 1));
								if ((l & 3) > 0)
									l = l - (l & 3) + 4;
								// System.out.println("TAG length: "+(l-pl));
							}
							i++;
						}
						if ((l & 3) > 0)
							l = l - (l & 3) + 4;
						break;
					}
					
					case XT_ARRAY_STR: 
					{
						System.String[] sa = cont.asStrings();
						int i = 0;
						while (i < sa.Length)
						{
							if (sa[i] != null)
							{
								try
								{
									byte /* sbyte */[] b = SupportClass.ToSByteArray(System.Text.Encoding.GetEncoding(RConnection.transferCharset).GetBytes(sa[i]));
									l += b.Length;
									b = null;
								}
								catch (System.IO.IOException)
								{
									// FIXME: we should so something ... so far we hope noone's gonna mess with the encoding
								}
							}
							l++;
							i++;
						}
						if ((l & 3) > 0)
							l = l - (l & 3) + 4;
						break;
					}
					} // switch
				if (l > 0xfffff0)
					l += 4; // large data need 4 more bytes
				// System.out.println("len:"+(l+4)+" "+xtName(rxt)+"/"+xtName(type)+" "+cont);
				return l + 4; // add the header
			}
			
		}
		/// <summary>xpression type: NULL </summary>
		public const int XT_NULL = 0;
		/// <summary>xpression type: integer </summary>
		public const int XT_INT = 1;
		/// <summary>xpression type: double </summary>
		public const int XT_DOUBLE = 2;
		/// <summary>xpression type: String </summary>
		public const int XT_STR = 3;
		/// <summary>xpression type: language construct (currently content is same as list) </summary>
		public const int XT_LANG = 4;
		/// <summary>xpression type: symbol (content is symbol name: String) </summary>
		public const int XT_SYM = 5;
		/// <summary>xpression type: RBool </summary>
		public const int XT_BOOL = 6;
		/// <summary>xpression type: S4 object</summary>
		/// <since> Rserve 0.5 
		/// </since>
		public const int XT_S4 = 7;
		/// <summary>xpression type: generic vector (RList) </summary>
		public const int XT_VECTOR = 16;
		/// <summary>xpression type: dotted-pair list (RList) </summary>
		public const int XT_LIST = 17;
		/// <summary>xpression type: closure (there is no java class for that type (yet?). currently the body of the closure is stored in the content part of the REXP. Please note that this may change in the future!) </summary>
		public const int XT_CLOS = 18;
		/// <summary>xpression type: symbol name</summary>
		/// <since> Rserve 0.5 
		/// </since>
		public const int XT_SYMNAME = 19;
		/// <summary>xpression type: dotted-pair list (w/o tags)</summary>
		/// <since> Rserve 0.5 
		/// </since>
		public const int XT_LIST_NOTAG = 20;
		/// <summary>xpression type: dotted-pair list (w tags)</summary>
		/// <since> Rserve 0.5 
		/// </since>
		public const int XT_LIST_TAG = 21;
		/// <summary>xpression type: language list (w/o tags)</summary>
		/// <since> Rserve 0.5 
		/// </since>
		public const int XT_LANG_NOTAG = 22;
		/// <summary>xpression type: language list (w tags)</summary>
		/// <since> Rserve 0.5 
		/// </since>
		public const int XT_LANG_TAG = 23;
		/// <summary>xpression type: expression vector </summary>
		public const int XT_VECTOR_EXP = 26;
		/// <summary>xpression type: string vector </summary>
		public const int XT_VECTOR_STR = 27;
		/// <summary>xpression type: int[] </summary>
		public const int XT_ARRAY_INT = 32;
		/// <summary>xpression type: double[] </summary>
		public const int XT_ARRAY_DOUBLE = 33;
		/// <summary>xpression type: String[] (currently not used, Vector is used instead) </summary>
		public const int XT_ARRAY_STR = 34;
		/// <summary>internal use only! this constant should never appear in a REXP </summary>
		public const int XT_ARRAY_BOOL_UA = 35;
		/// <summary>xpression type: RBool[] </summary>
		public const int XT_ARRAY_BOOL = 36;
		/// <summary>xpression type: raw (byte[])</summary>
		/// <since> Rserve 0.4-? 
		/// </since>
		public const int XT_RAW = 37;
		/// <summary>xpression type: Complex[]</summary>
		/// <since> Rserve 0.5 
		/// </since>
		public const int XT_ARRAY_CPLX = 38;
		/// <summary>xpression type: unknown; no assumptions can be made about the content </summary>
		public const int XT_UNKNOWN = 48;
		
		/// <summary>xpression type: RFactor; this XT is internally generated (ergo is does not come from Rsrv.h) to support RFactor class which is built from XT_ARRAY_INT </summary>
		public const int XT_FACTOR = 127;
		
		/// <summary>used for transport only - has attribute </summary>
		private const int XT_HAS_ATTR = 128;
		
		internal int type;
		internal REXPFactory attr;
		internal REXP cont;
		internal RList rootList;
		
		public REXPFactory()
		{
		}
		
		public REXPFactory(REXP r)
		{
			if (r == null)
				r = new REXPNull();
			REXPList a = r._attr();
			cont = r;
			if (a != null)
				attr = new REXPFactory(a);
			if (r is REXPNull)
			{
				type = XT_NULL;
			}
			else if (r is REXPList)
			{
				RList l = r.asList();
				type = l.Named?XT_LIST_TAG:XT_LIST_NOTAG;
				if (r is REXPLanguage)
					type = (type == XT_LIST_TAG)?XT_LANG_TAG:XT_LANG_NOTAG;
			}
			else if (r is REXPGenericVector)
			{
				type = XT_VECTOR; // FIXME: may have to adjust names attr
			}
			else if (r is REXPS4)
			{
				type = XT_S4;
			}
			else if (r is REXPInteger)
			{
				// this includes factor - FIXME: do we need speacial handling?
				type = XT_ARRAY_INT;
			}
			else if (r is REXPDouble)
			{
				type = XT_ARRAY_DOUBLE;
			}
			else if (r is REXPString)
			{
				type = XT_ARRAY_STR;
			}
			else if (r is REXPSymbol)
			{
				type = XT_SYMNAME;
			}
			else if (r is REXPRaw)
			{
				type = XT_RAW;
			}
			else if (r is REXPLogical)
			{
				type = XT_ARRAY_BOOL;
			}
			else
			{
				// throw new REXPMismatchException(r, "decode");
				System.Console.Error.WriteLine("*** REXPFactory unable to interpret " + r);
			}
		}
		
		/// <summary>parses byte buffer for binary representation of xpressions - read one xpression slot (descends recursively for aggregated xpressions such as lists, vectors etc.)</summary>
		/// <param name="buf">buffer containing the binary representation
		/// </param>
		/// <param name="o">offset in the buffer to start at
		/// </param>
		/// <returns> position just behind the parsed xpression. Can be use for successive calls to {@link #parseREXP} if more than one expression is stored in the binary array. 
		/// </returns>
		public virtual int parseREXP(byte /* sbyte */[] buf, int o)
		{
			int xl = RTalk.getLen(buf, o);
			bool hasAtt = ((buf[o] & 128) != 0);
			bool isLong = ((buf[o] & 64) != 0);
			int xt = (int) (buf[o] & 63);
			//System.out.println("parseREXP: type="+xt+", len="+xl+", hasAtt="+hasAtt+", isLong="+isLong);
			if (isLong)
				o += 4;
			o += 4;
			int eox = o + xl;
			
			type = xt; attr = new REXPFactory(); cont = null;
			if (hasAtt)
				o = attr.parseREXP(buf, o);
			if (xt == XT_NULL)
			{
				cont = new REXPNull(Attr);
				return o;
			}
			if (xt == XT_DOUBLE)
			{
				long lr = RTalk.getLong(buf, o);
                byte[] intermediate = BitConverter.GetBytes(lr);
                double dr = BitConverter.ToDouble(intermediate, 0);
				double[] d = new double[]{dr};
				o += 8;
				if (o != eox)
				{
					System.Console.Error.WriteLine("Warning: double SEXP size mismatch\n");
					o = eox;
				}
				cont = new REXPDouble(d, Attr);
				return o;
			}
			if (xt == XT_ARRAY_DOUBLE)
			{
				int as_Renamed = (eox - o) / 8, i = 0;
				double[] d = new double[as_Renamed];
				while (o < eox)
				{
                    byte[] intermediate = BitConverter.GetBytes(RTalk.getLong(buf, o));
                    double dr = BitConverter.ToDouble(intermediate, 0);
                    d[i] = dr;
					o += 8;
					i++;
				}
				if (o != eox)
				{
					System.Console.Error.WriteLine("Warning: double array SEXP size mismatch\n");
					o = eox;
				}
				cont = new REXPDouble(d, Attr);
				return o;
			}
			if (xt == XT_BOOL)
			{
				byte /* sbyte */[] b = new byte /* sbyte */[]{buf[o]};
				cont = new REXPLogical(b, Attr);
				o++;
				if (o != eox)
				{
					if (eox != o + 3)
					// o+3 could happen if the result was aligned (1 byte data + 3 bytes padding)
						System.Console.Error.WriteLine("Warning: bool SEXP size mismatch\n");
					o = eox;
				}
				return o;
			}
			if (xt == XT_ARRAY_BOOL_UA)
			{
				int as_Renamed = (eox - o);
				byte /* sbyte */[] d = new byte /* sbyte */[as_Renamed];
				Array.Copy(buf, o, d, 0, eox - o);
				o = eox;
				cont = new REXPLogical(d, Attr);
				return o;
			}
			if (xt == XT_ARRAY_BOOL)
			{
				int as_Renamed = RTalk.getInt(buf, o);
				o += 4;
				byte /* sbyte */[] d = new byte /* sbyte */[as_Renamed];
				Array.Copy(buf, o, d, 0, as_Renamed);
				o = eox;
				cont = new REXPLogical(d, Attr);
				return o;
			}
			if (xt == XT_INT)
			{
				int[] i = new int[]{RTalk.getInt(buf, o)};
				cont = new REXPInteger(i, Attr);
				o += 4;
				if (o != eox)
				{
					System.Console.Error.WriteLine("Warning: int SEXP size mismatch\n");
					o = eox;
				}
				return o;
			}
			if (xt == XT_ARRAY_INT)
			{
				int as_Renamed = (eox - o) / 4, i = 0;
				int[] d = new int[as_Renamed];
				while (o < eox)
				{
					d[i] = RTalk.getInt(buf, o);
					o += 4;
					i++;
				}
				if (o != eox)
				{
					System.Console.Error.WriteLine("Warning: int array SEXP size mismatch\n");
					o = eox;
				}
				cont = null;
				// hack for lists - special lists attached to int are factors
				try
				{
					if (Attr != null)
					{
						REXP ca = Attr.asList().at("class");
						REXP ls = Attr.asList().at("levels");
						if (ca != null && ls != null && ca.asString().Equals("factor"))
						{
							// R uses 1-based index, Java uses 0-based one
							cont = new REXPFactor(d, ls.asStrings(), Attr);
							xt = XT_FACTOR;
						}
					}
				}
				catch (System.Exception)
				{
				}
				if (cont == null)
					cont = new REXPInteger(d, Attr);
				return o;
			}
			if (xt == XT_RAW)
			{
				int as_Renamed = RTalk.getInt(buf, o);
				o += 4;
				byte /* sbyte */[] d = new byte /* sbyte */[as_Renamed];
				Array.Copy(buf, o, d, 0, as_Renamed);
				o = eox;
				cont = new REXPRaw(d, Attr);
				return o;
			}
			if (xt == XT_LIST_NOTAG || xt == XT_LIST_TAG || xt == XT_LANG_NOTAG || xt == XT_LANG_TAG)
			{
				REXPFactory lc = new REXPFactory();
				REXPFactory nf = new REXPFactory();
				RList l = new RList();
				while (o < eox)
				{
					System.String name = null;
					o = lc.parseREXP(buf, o);
					if (xt == XT_LIST_TAG || xt == XT_LANG_TAG)
					{
						o = nf.parseREXP(buf, o);
						if (nf.cont.Symbol || nf.cont.String)
							name = nf.cont.asString();
					}
					if (name == null)
						l.Add(lc.cont);
					else
						l.put(name, lc.cont);
				}
				cont = (xt == XT_LANG_NOTAG || xt == XT_LANG_TAG)?new REXPLanguage(l, Attr):new REXPList(l, Attr);
				if (o != eox)
				{
					System.Console.Error.WriteLine("Warning: int list SEXP size mismatch\n");
					o = eox;
				}
				return o;
			}
			if (xt == XT_LIST || xt == XT_LANG)
			{
				//old-style lists, for comaptibility with older Rserve versions - rather inefficient since we have to convert the recusively stored structures into a flat structure
				bool isRoot = false;
				if (rootList == null)
				{
					rootList = new RList();
					isRoot = true;
				}
				REXPFactory headf = new REXPFactory();
				REXPFactory tagf = new REXPFactory();
				o = headf.parseREXP(buf, o);
				int elIndex = rootList.Count;
				rootList.Add(headf.cont);
				//System.out.println("HEAD="+headf.cont);
				o = parseREXP(buf, o); // we use ourselves recursively for the body
				if (o < eox)
				{
					o = tagf.parseREXP(buf, o);
					//System.out.println("TAG="+tagf.cont);
					if (tagf.cont != null && (tagf.cont.String || tagf.cont.Symbol))
						rootList.setKeyAt(elIndex, tagf.cont.asString());
				}
				if (isRoot)
				{
					cont = (xt == XT_LIST)?new REXPList(rootList, Attr):new REXPLanguage(rootList, Attr);
					rootList = null;
					//System.out.println("result="+cont);
				}
				return o;
			}
			if (xt == XT_VECTOR || xt == XT_VECTOR_EXP)
			{
				System.Collections.ArrayList v = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(10)); //FIXME: could we use RList?
				while (o < eox)
				{
					REXPFactory xx = new REXPFactory();
					o = xx.parseREXP(buf, o);
					v.Add(xx.cont);
				}
				if (o != eox)
				{
					System.Console.Error.WriteLine("Warning: int vector SEXP size mismatch\n");
					o = eox;
				}
				// fixup for lists since they're stored as attributes of vectors
				if (Attr != null && Attr.asList().at("names") != null)
				{
					REXP nam = Attr.asList().at("names");
					System.String[] names = null;
					if (nam.String)
						names = nam.asStrings();
					else if (nam.Vector)
					{
						// names could be a vector if supplied by old Rserve
						RList l = nam.asList();
						System.Object[] oa = l.ToArray();
						names = new System.String[oa.Length];
						for (int i = 0; i < oa.Length; i++)
							names[i] = ((REXP) oa[i]).asString();
					}
					RList l2 = new RList(v, names);
					cont = (xt == XT_VECTOR_EXP)?new REXPExpressionVector(l2, Attr):new REXPGenericVector(l2, Attr);
				}
				else
					cont = (xt == XT_VECTOR_EXP)?new REXPExpressionVector(new RList(v), Attr):new REXPGenericVector(new RList(v), Attr);
				return o;
			}
			if (xt == XT_ARRAY_STR)
			{
				int c = 0, i = o;
				while (i < eox)
					if (buf[i++] == 0)
						c++;
				System.String[] s = new System.String[c];
				if (c > 0)
				{
					c = 0; i = o;
					while (o < eox)
					{
						if (buf[o] == 0)
						{
							try
							{
								System.String tempStr;
								tempStr = System.Text.Encoding.GetEncoding(RConnection.transferCharset).GetString(SupportClass.ToByteArray(buf));
								s[c] = new System.String(tempStr.ToCharArray(), i, o - i);
							}
							catch (System.IO.IOException)
							{
								s[c] = "";
							}
							c++;
							i = o + 1;
						}
						o++;
					}
				}
				cont = new REXPString(s, Attr);
				return o;
			}
			if (xt == XT_VECTOR_STR)
			{
				System.Collections.ArrayList v = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(10));
				while (o < eox)
				{
					REXPFactory xx = new REXPFactory();
					o = xx.parseREXP(buf, o);
					v.Add(xx.cont.asString());
				}
				if (o != eox)
				{
					System.Console.Error.WriteLine("Warning: int vector SEXP size mismatch\n");
					o = eox;
				}
				System.String[] sa = new System.String[v.Count];
				int i = 0; while (i < sa.Length)
				{
					sa[i] = ((System.String) v[i]); i++;
				}
				cont = new REXPString(sa, Attr);
				return o;
			}
			if (xt == XT_STR || xt == XT_SYMNAME)
			{
				int i = o;
				while (buf[i] != 0 && i < eox)
					i++;
				try
				{
					if (xt == XT_STR)
					{
						System.String tempStr2;
						tempStr2 = System.Text.Encoding.GetEncoding(RConnection.transferCharset).GetString(SupportClass.ToByteArray(buf));
						cont = new REXPString(new System.String[]{new System.String(tempStr2.ToCharArray(), o, i - o)}, Attr);
					}
					else
					{
						System.String tempStr3;
						tempStr3 = System.Text.Encoding.GetEncoding(RConnection.transferCharset).GetString(SupportClass.ToByteArray(buf));
						cont = new REXPSymbol(new System.String(tempStr3.ToCharArray(), o, i - o));
					}
				}
				catch (System.Exception)
				{
					System.Console.Error.WriteLine("unable to convert string\n");
					cont = null;
				}
				o = eox;
				return o;
			}
			if (xt == XT_SYM)
			{
				REXPFactory sym = new REXPFactory();
				o = sym.parseREXP(buf, o); // PRINTNAME that's all we will use
				cont = new REXPSymbol(sym.REXP.asString()); // content of a symbol is its printname string (so far)
				o = eox;
				return o;
			}
			
			if (xt == XT_CLOS)
			{
				/*
				REXP form=new REXP();
				REXP body=new REXP();
				o=parseREXP(form,buf,o);
				o=parseREXP(body,buf,o);
				if (o!=eox) {
				System.err.println("Warning: closure SEXP size mismatch\n");
				o=eox;
				}
				x.cont=body;
				*/
				o = eox;
				return o;
			}
			
			if (xt == XT_UNKNOWN)
			{
				cont = new REXPUnknown(RTalk.getInt(buf, o), Attr);
				o = eox;
				return o;
			}
			
			if (xt == XT_S4)
			{
				cont = new REXPS4(Attr);
				o = eox;
				return o;
			}
			
			cont = null;
			o = eox;
			System.Console.Error.WriteLine("unhandled type: " + xt);
			return o;
		}
		
		/// <summary>Stores the REXP in its binary (ready-to-send) representation including header into a buffer and returns the index of the byte behind the REXP.
		/// <p>Please note that currently only XT_[ARRAY_]INT, XT_[ARRAY_]DOUBLE and XT_[ARRAY_]STR are supported! All other types will be stored as SEXP of the length 0 without any contents.
		/// </summary>
		/// <param name="buf">buffer to store the REXP binary into
		/// </param>
		/// <param name="off">offset of the first byte where to store the REXP
		/// </param>
		/// <returns> the offset of the first byte behind the stored REXP 
		/// </returns>
		public virtual int getBinaryRepresentation(byte /* sbyte */[] buf, int off)
		{
			int myl = BinaryLength;
			bool isLarge = (myl > 0xfffff0);
			bool hasAttr = false;
			//UPGRADE_NOTE: Final was removed from the declaration of 'a '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
			REXPList a = Attr;
			RList al = null;
			if (a != null)
				al = a.asList();
			if (al != null && al.Count > 0)
				hasAttr = true;
			int rxt = type, ooff = off;
			if (type == XT_VECTOR_STR)
				rxt = XT_ARRAY_STR; // VECTOR_STR is broken right now
			if (type == XT_LIST || type == XT_LIST_TAG || type == XT_LIST_NOTAG)
				rxt = (cont.asList() != null && cont.asList().Named)?XT_LIST_TAG:XT_LIST_NOTAG;
			// System.out.println("@"+off+": "+xtName(rxt)+"/"+xtName(type)+" "+cont+" ("+myl+"/"+buf.length+") att="+hasAttr);
			RTalk.setHdr(rxt | (hasAttr?XT_HAS_ATTR:0), myl - (isLarge?8:4), buf, off);
			off += (isLarge?8:4);
			if (hasAttr)
				off = attr.getBinaryRepresentation(buf, off);
			switch (rxt)
			{
				
				case XT_S4: 
				case XT_NULL: 
					break;
				
				case XT_INT:  RTalk.setInt(cont.asInteger(), buf, off); break;
				
				case XT_DOUBLE:
                    RTalk.setLong(BitConverter.ToInt64(BitConverter.GetBytes(cont.asDouble()), 0), buf, off); break;
				
				case XT_ARRAY_INT: 
				{
					int[] ia = cont.asIntegers();
					int i = 0, io = off;
					while (i < ia.Length)
					{
						RTalk.setInt(ia[i++], buf, io); io += 4;
					}
					break;
				}
				
				case XT_ARRAY_DOUBLE: 
				{
					double[] da = cont.asDoubles();
					int i = 0, io = off;
					while (i < da.Length)
					{
                        RTalk.setLong(BitConverter.ToInt64(BitConverter.GetBytes(da[i++]), 0), buf, io);
						io += 8;
					}
					break;
				}
				
				case XT_RAW: 
				{
					byte /* sbyte */[] by = cont.asBytes();
					RTalk.setInt(by.Length, buf, off); off += 4;
					Array.Copy(by, 0, buf, off, by.Length);
					break;
				}
				
				case XT_ARRAY_STR: 
				{
					System.String[] sa = cont.asStrings();
					int i = 0, io = off;
					while (i < sa.Length)
					{
						if (sa[i] != null)
						{
							try
							{
								byte /* sbyte */[] b = SupportClass.ToSByteArray(System.Text.Encoding.GetEncoding(RConnection.transferCharset).GetBytes(sa[i]));
								Array.Copy(b, 0, buf, io, b.Length);
								io += b.Length;
								b = null;
							}
							catch (System.IO.IOException)
							{
								// FIXME: we should so something ... so far we hope noone's gonna mess with the encoding
							}
						}
						buf[io++] = 0;
						i++;
					}
					i = io - off;
					while ((i & 3) != 0)
					{
						buf[io++] = 1; i++;
					} // padding if necessary..
					break;
				}
				
				case XT_LIST_TAG: 
				case XT_LIST_NOTAG: 
				case XT_LANG_TAG: 
				case XT_LANG_NOTAG: 
				case XT_LIST: 
				case XT_VECTOR: 
				case XT_VECTOR_EXP: 
				{
					int io = off;
					//UPGRADE_NOTE: Final was removed from the declaration of 'lst '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
					RList lst = cont.asList();
					if (lst != null)
					{
						int i = 0;
						while (i < lst.Count)
						{
							REXP x = lst.at(i);
							if (x == null)
								x = new REXPNull();
							io = new REXPFactory(x).getBinaryRepresentation(buf, io);
							if (rxt == XT_LIST_TAG || rxt == XT_LANG_TAG)
								io = new REXPFactory(new REXPSymbol(lst.keyAt(i))).getBinaryRepresentation(buf, io);
							i++;
						}
					}
					// System.out.println("io="+io+", expected: "+(ooff+myl));
					break;
				}
				
				
				case XT_SYMNAME: 
				case XT_STR: 
					getStringBinaryRepresentation(buf, off, cont.asString());
					break;
				}
			return ooff + myl;
		}
		
		public static int getStringBinaryRepresentation(byte /* sbyte */[] buf, int off, System.String s)
		{
			if (s == null)
				s = "";
			int io = off;
			try
			{
				byte /* sbyte */[] b = SupportClass.ToSByteArray(System.Text.Encoding.GetEncoding(RConnection.transferCharset).GetBytes(s));
				// System.out.println("<str> @"+off+", len "+b.length+" (cont "+buf.length+") \""+s+"\"");
				Array.Copy(b, 0, buf, io, b.Length);
				io += b.Length;
				b = null;
			}
			catch (System.IO.IOException)
			{
				// FIXME: we should so something ... so far we hope noone's gonna mess with the encoding
			}
			buf[io++] = 0;
			while ((io & 3) != 0)
				buf[io++] = 0; // padding if necessary..
			return io;
		}
		
		/// <summary>returns human-readable name of the xpression type as string. Arrays are denoted by a trailing asterisk (*).</summary>
		/// <param name="xt">xpression type
		/// </param>
		/// <returns> name of the xpression type 
		/// </returns>
		public static System.String xtName(int xt)
		{
			if (xt == XT_NULL)
				return "NULL";
			if (xt == XT_INT)
				return "INT";
			if (xt == XT_STR)
				return "STRING";
			if (xt == XT_DOUBLE)
				return "REAL";
			if (xt == XT_BOOL)
				return "BOOL";
			if (xt == XT_ARRAY_INT)
				return "INT*";
			if (xt == XT_ARRAY_STR)
				return "STRING*";
			if (xt == XT_ARRAY_DOUBLE)
				return "REAL*";
			if (xt == XT_ARRAY_BOOL)
				return "BOOL*";
			if (xt == XT_ARRAY_CPLX)
				return "COMPLEX*";
			if (xt == XT_SYM)
				return "SYMBOL";
			if (xt == XT_SYMNAME)
				return "SYMNAME";
			if (xt == XT_LANG)
				return "LANG";
			if (xt == XT_LIST)
				return "LIST";
			if (xt == XT_LIST_TAG)
				return "LIST+T";
			if (xt == XT_LIST_NOTAG)
				return "LIST/T";
			if (xt == XT_LANG_TAG)
				return "LANG+T";
			if (xt == XT_LANG_NOTAG)
				return "LANG/T";
			if (xt == XT_CLOS)
				return "CLOS";
			if (xt == XT_RAW)
				return "RAW";
			if (xt == XT_S4)
				return "S4";
			if (xt == XT_VECTOR)
				return "VECTOR";
			if (xt == XT_VECTOR_STR)
				return "STRING[]";
			if (xt == XT_VECTOR_EXP)
				return "EXPR[]";
			if (xt == XT_FACTOR)
				return "FACTOR";
			if (xt == XT_UNKNOWN)
				return "UNKNOWN";
			return "<unknown " + xt + ">";
		}
	}
}
#endif