#if USE_R
// REngine
// Copyright (C) 2007 Simon Urbanek
// --- for licensing information see LICENSE file in the original distribution ---
using System;
namespace org.rosuda.REngine
{
	
	/// <summary>representation of a factor variable. In R there is no actual object
	/// type called "factor", instead it is coded as an int vector with a list
	/// attribute. The parser code of REXP converts such constructs directly into
	/// the RFactor objects and defines an own XT_FACTOR type 
	/// </summary>
	/// <version>  $Id: RFactor.java 2841 2008-02-27 18:47:46Z urbanek $
	/// </version>
	public class RFactor
	{
		internal int[] ids;
		internal System.String[] levels_Renamed_Field;
		internal int index_base;
		
		/// <summary>create a new, empty factor var </summary>
		public RFactor()
		{
			ids = new int[0]; levels_Renamed_Field = new System.String[0];
		}
		
		/// <summary>create a new factor variable, based on the supplied arrays.</summary>
		/// <param name="i">array of IDs (inde_base..v.length+index_base-1)
		/// </param>
		/// <param name="v">values - cotegory names
		/// </param>
		/// <param name="copy">copy above vaules or just retain them
		/// </param>
		/// <param name="index_base">index of the first level element (1 for R factors, cannot be negtive)
		/// </param>
		public RFactor(int[] i, System.String[] v, bool copy, int index_base)
		{
			if (i == null)
				i = new int[0];
			if (v == null)
				v = new System.String[0];
			if (copy)
			{
				ids = new int[i.Length]; Array.Copy(i, 0, ids, 0, i.Length);
				levels_Renamed_Field = new System.String[v.Length]; Array.Copy(v, 0, levels_Renamed_Field, 0, v.Length);
			}
			else
			{
				ids = i; levels_Renamed_Field = v;
			}
			this.index_base = index_base;
		}
		
		/// <summary>create a new factor variable by factorizing a given string array. The levels will be created in the orer of appearance.</summary>
		/// <param name="c">contents
		/// </param>
		/// <param name="index_base">base of the level index 
		/// </param>
		public RFactor(System.String[] c, int index_base)
		{
			this.index_base = index_base;
			if (c == null)
				c = new System.String[0];
			System.Collections.ArrayList lv = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(10));
			ids = new int[c.Length];
			int i = 0;
			while (i < c.Length)
			{
				int ix = (c[i] == null)?- 1:lv.IndexOf(c[i]);
				if (ix < 0 && c[i] != null)
				{
					ix = lv.Count;
					lv.Add(c[i]);
				}
				ids[i] = (ix < 0)?REXPInteger.NA:(ix + index_base);
				i++;
			}
			levels_Renamed_Field = new System.String[lv.Count];
			i = 0;
			while (i < levels_Renamed_Field.Length)
			{
				levels_Renamed_Field[i] = ((System.String) lv[i]);
				i++;
			}
		}
		
		/// <summary>same as <code>RFactor(c, 1)</code> </summary>
		public RFactor(System.String[] c):this(c, 1)
		{
		}
		
		/// <summary>same as <code>RFactor(i,v, true, 1)</code> </summary>
		public RFactor(int[] i, System.String[] v):this(i, v, true, 1)
		{
		}
		
		/// <summary>returns the level of a given case</summary>
		/// <param name="i">case number
		/// </param>
		/// <returns> name. may throw exception if out of range 
		/// </returns>
		public virtual System.String at(int i)
		{
			int li = ids[i] - index_base;
			return (li < 0 || li > levels_Renamed_Field.Length)?null:levels_Renamed_Field[li];
		}
		
		/// <summary>returns <code>true</code> if the data contain the given level index </summary>
		public virtual bool contains(int li)
		{
			int i = 0;
			while (i < ids.Length)
			{
				if (ids[i] == li)
					return true;
				i++;
			}
			return false;
		}
		
		/// <summary>return <code>true</code> if the factor contains the given level (it is NOT the same as levelIndex==-1!) </summary>
		public virtual bool contains(System.String name)
		{
			int li = levelIndex(name);
			if (li < 0)
				return false;
			int i = 0;
			while (i < ids.Length)
			{
				if (ids[i] == li)
					return true;
				i++;
			}
			return false;
		}
		
		/// <summary>count the number of occurences of a given level index </summary>
		public virtual int count(int levelIndex)
		{
			int i = 0;
			int ct = 0;
			while (i < ids.Length)
			{
				if (ids[i] == levelIndex)
					ct++;
				i++;
			}
			return ct;
		}
		
		/// <summary>count the number of occurences of a given level name </summary>
		public virtual int count(System.String name)
		{
			return count(levelIndex(name));
		}
		
		/// <summary>return an array with level counts. </summary>
		public virtual int[] counts()
		{
			int[] c = new int[levels_Renamed_Field.Length];
			int i = 0;
			while (i < ids.Length)
			{
				//UPGRADE_NOTE: Final was removed from the declaration of 'li '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
				int li = ids[i] - index_base;
				if (li >= 0 && li < levels_Renamed_Field.Length)
					c[li]++;
				i++;
			}
			return c;
		}
		
		/// <summary>return the index of a given level name or -1 if it doesn't exist </summary>
		public virtual int levelIndex(System.String name)
		{
			if (name == null)
				return - 1;
			int i = 0;
			while (i < levels_Renamed_Field.Length)
			{
				if (levels_Renamed_Field[i] != null && levels_Renamed_Field[i].Equals(name))
					return i + index_base;
				i++;
			}
			return - 1;
		}
		
		/// <summary>return the list of levels (0-based, use {@link #indexBase} correction if you want to access it by level index) </summary>
		public virtual System.String[] levels()
		{
			return levels_Renamed_Field;
		}
		
		/// <summary>return the contents as integer indices (with the index base of this factor) </summary>
		public virtual int[] asIntegers()
		{
			return ids;
		}
		
		/// <summary>return the contents as integer indices with a given index base </summary>
		public virtual int[] asIntegers(int desired_index_base)
		{
			if (desired_index_base == index_base)
				return ids;
			int[] ix = new int[ids.Length];
			int j = 0; while (j < ids.Length)
			{
				ix[j] = ids[j] - index_base + desired_index_base; j++;
			}
			return ix;
		}
		
		/// <summary>return the level name for a given level index </summary>
		public virtual System.String levelAtIndex(int li)
		{
			li -= index_base;
			return (li < 0 || li > levels_Renamed_Field.Length)?null:levels_Renamed_Field[li];
		}
		
		/// <summary>return the level index for a given case </summary>
		public virtual int indexAt(int i)
		{
			return ids[i];
		}
		
		/// <summary>return the factor as an array of strings </summary>
		public virtual System.String[] asStrings()
		{
			System.String[] s = new System.String[ids.Length];
			int i = 0;
			while (i < ids.Length)
			{
				s[i] = at(i);
				i++;
			}
			return s;
		}
		
		/// <summary>return the base of the levels index </summary>
		public virtual int indexBase()
		{
			return index_base;
		}
		
		/// <summary>returns the number of cases </summary>
		public virtual int size()
		{
			return ids.Length;
		}
		
		public override System.String ToString()
		{
			return base.ToString() + "[" + ids.Length + "," + levels_Renamed_Field.Length + ",#" + index_base + "]";
		}
		
		/// <summary>displayable representation of the factor variable
		/// public String toString() {
		/// //return "{"+((val==null)?"<null>;":("levels="+val.size()+";"))+((id==null)?"<null>":("cases="+id.size()))+"}";
		/// StringBuffer sb=new StringBuffer("{levels=(");
		/// if (val==null)
		/// sb.append("null");
		/// else
		/// for (int i=0;i<val.size();i++) {
		/// sb.append((i>0)?",\"":"\"");
		/// sb.append((String)val.elementAt(i));
		/// sb.append("\"");
		/// };
		/// sb.append("),ids=(");
		/// if (id==null)
		/// sb.append("null");
		/// else
		/// for (int i=0;i<id.size();i++) {
		/// if (i>0) sb.append(",");
		/// sb.append((Integer)id.elementAt(i));
		/// };
		/// sb.append(")}");
		/// return sb.toString();
		/// } 
		/// </summary>
	}
}
#endif