#if USE_R
// REngine library - Java client interface to R
// Copyright (C) 2004,2007,2008 Simon Urbanek
using System;
namespace org.rosuda.REngine
{
	
	/// <summary>implementation of R-lists<br>
	/// All lists (dotted-pair lists, language lists, expressions and vectors) are regarded as named generic vectors. 
	/// Note: This implementation has changed radically in Rserve 0.5!
	/// This class inofficially implements the Map interface. Unfortunately a conflict in the Java iterface classes Map and List doesn't allow us to implement both officially. Most prominently the Map 'remove' method had to be renamed to removeByKey.
	/// </summary>
	/// <version>  $Id: RList.java 2906 2008-07-02 21:08:16Z urbanek $
	/// </version>
	[Serializable]
	public class RList:System.Collections.ArrayList, System.Collections.IList
	{
		/// <summary>checks whether this list is named or unnamed</summary>
		/// <returns> <code>true</code> if this list is named, <code>false</code> otherwise 
		/// </returns>
		virtual public bool Named
		{
			get
			{
				return names != null;
			}
			
		}
		public System.Collections.ArrayList names;
		
		/// <summary>constructs an empty list </summary>
		public RList():base()
		{
			names = null;
		}
		
		/// <summary>constructs an initialized, unnamed list</summary>
		/// <param name="contents">- an array of {@link REXP}s to use as contents of this list 
		/// </param>
		public RList(REXP[] contents):base(contents.Length)
		{
			int i = 0;
			while (i < contents.Length)
				base.Add(contents[i++]);
			names = null;
		}
		
		public RList(int initSize, bool hasNames):base(initSize)
		{
			names = null;
			if (hasNames)
				names = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(initSize));
		}
		
		/// <summary>constructs an initialized, unnamed list</summary>
		/// <param name="contents">- a {@link Collection} of {@link REXP}s to use as contents of this list 
		/// </param>
		public RList(System.Collections.ICollection contents):base(contents)
		{
			names = null;
		}
		
		/// <summary>constructs an initialized, named list. The length of the contents vector determines the length of the list.</summary>
		/// <param name="contents">- an array of {@link REXP}s to use as contents of this list
		/// </param>
		/// <param name="names">- an array of {@link String}s to use as names 
		/// </param>
		public RList(REXP[] contents, System.String[] names):this(contents)
		{
			if (names != null && names.Length > 0)
			{
				this.names = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(names.Length));
				int i = 0;
				while (i < names.Length)
					this.names.Add(names[i++]);
				while (this.names.Count < Count)
					this.names.Add(null);
			}
		}
		
		/// <summary>constructs an initialized, named list. The size of the contents collection determines the length of the list.</summary>
		/// <param name="contents">- a {@link Collection} of {@link REXP}s to use as contents of this list
		/// </param>
		/// <param name="names">- an array of {@link String}s to use as names 
		/// </param>
		public RList(System.Collections.ICollection contents, System.String[] names):this(contents)
		{
			if (names != null && names.Length > 0)
			{
				this.names = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(names.Length));
				int i = 0;
				while (i < names.Length)
					this.names.Add(names[i++]);
				while (this.names.Count < Count)
					this.names.Add(null);
			}
		}
		
		/// <summary>constructs an initialized, named list. The size of the contents collection determines the length of the list.</summary>
		/// <param name="contents">- a {@link Collection} of {@link REXP}s to use as contents of this list
		/// </param>
		/// <param name="names">- an {@link Collection} of {@link String}s to use as names 
		/// </param>
		public RList(System.Collections.ICollection contents, System.Collections.ICollection names):this(contents)
		{
			if (names != null && names.Count > 0)
			{
				this.names = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(names));
				while (this.names.Count < Count)
					this.names.Add(null);
			}
		}
		
		/// <summary>get xpression given a key</summary>
		/// <param name="v">key
		/// </param>
		/// <returns> value which corresponds to the given key or
		/// <code>null</code> if the list is unnamed or key not found 
		/// </returns>
		public virtual REXP at(System.String v)
		{
			if (names == null)
				return null;
			int i = names.IndexOf(v);
			if (i < 0)
				return null;
			return (REXP) this[i];
		}
		
		/// <summary>get element at the specified position</summary>
		/// <param name="i">index
		/// </param>
		/// <returns> value at the index or <code>null</code> if the index is out of bounds 
		/// </returns>
		public virtual REXP at(int i)
		{
			return (i >= 0 && i < Count)?(REXP) this[i]:null;
		}
		
		/// <summary>return the key (name) at a given index</summary>
		/// <param name="i">index
		/// </param>
		/// <returns> ket at the index - can be <code>null</code> is the list is unnamed or the index is out of range 
		/// </returns>
		public virtual System.String keyAt(int i)
		{
			return (names == null || i < 0 || i >= names.Count)?null:(System.String) names[i];
		}
		
		/// <summary>set key at the given index. Using this method automatically makes the list a named one even if the key is <code>null</code>. Out of range operations are undefined (currently no-ops)</summary>
		/// <param name="i">index
		/// </param>
		/// <param name="value">key name 
		/// </param>
		public virtual void  setKeyAt(int i, System.String value_Renamed)
		{
			if (i < 0)
				return ;
			if (names == null)
				names = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(10));
			if (names.Count < Count)
				SupportClass.SetCapacity(names, Count);
			if (i < Count)
				names.Insert(i, value_Renamed);
		}
		
		/// <summary>returns all keys of the list</summary>
		/// <returns> array containing all keys or <code>null</code> if list unnamed 
		/// </returns>
		public virtual System.String[] keys()
		{
			if (names == null)
				return null;
			int i = 0;
			System.String[] k = new System.String[names.Count];
			while (i < k.Length)
			{
				k[i] = keyAt(i); i++;
			} ;
			return k;
		}
		
		// --- overrides that sync names
		
		public override void  Insert(int index, System.Object element)
		{
			base.Insert(index, element);
			if (names == null)
				return ;
			names.Insert(index, null);
		}
		
		//UPGRADE_ISSUE: The equivalent in .NET for method 'java.util.Vector.add' returns a different type. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1224'"
		public override int Add(System.Object element)
		{
			int retval = base.Add(element);
			if (names != null)
				names.Add(null);
			return retval;
		}
		
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.addAll' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public virtual bool addAll(System.Collections.ICollection c)
		{
			base.AddRange(c);
			bool ch = true;
			if (names == null)
				return ch;
			int l = Count;
			while (names.Count < l)
				names.Add(null);
			return ch;
		}
		
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.addAll' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public virtual bool addAll(int index, System.Collections.ICollection c)
		{
			base.InsertRange(index, c);
			bool ch = true;
			if (names == null)
				return ch;
			int l = c.Count;
			while (l > 0)
				names.Insert(index, null);
			return ch;
		}
		
		public override void  Clear()
		{
			base.Clear();
			names = null;
		}
		
		public override System.Object Clone()
		{
			return new RList(this, names);
		}
		
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.remove' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public virtual System.Object remove(int index)
		{
			System.Object tempObject;
			tempObject = base[index];
			base.RemoveAt(index);
			System.Object o = tempObject;
			if (names != null)
			{
				if (names is org.rosuda.REngine.RList)
					((org.rosuda.REngine.RList) names).remove(index);
				else
					names.RemoveAt(index);
				if (Count == 0)
					names = null;
			}
			return o;
		}
		
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.remove' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public virtual bool remove(System.Object elem)
		{
			int i = IndexOf(elem);
			if (i < 0)
				return false;
			remove(i);
			if (Count == 0)
				names = null;
			return true;
		}
		
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.removeAll' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public virtual bool removeAll(System.Collections.ICollection c)
		{
			if (names == null)
				return SupportClass.ICollectionSupport.RemoveAll((System.Collections.ArrayList) this, c);
			bool changed = false;
			System.Collections.IEnumerator it = c.GetEnumerator();
			//UPGRADE_TODO: Method 'java.util.Iterator.hasNext' was converted to 'System.Collections.IEnumerator.MoveNext' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilIteratorhasNext'"
			while (it.MoveNext())
			{
				//UPGRADE_TODO: Method 'java.util.Iterator.next' was converted to 'System.Collections.IEnumerator.Current' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilIteratornext'"
				changed |= remove(it.Current);
			}
			return changed;
		}
		
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.retainAll' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public virtual bool retainAll(System.Collections.ICollection c)
		{
			if (names == null)
				return SupportClass.ICollectionSupport.RetainAll((System.Collections.ArrayList) this, c);
			bool[] rm = new bool[Count];
			bool changed = false;
			int i = 0;
			while (i < rm.Length)
			{
				changed |= (rm[i] = !SupportClass.ICollectionSupport.Contains(c, this[i]));
				i++;
			}
			while (i > 0)
			{
				i--;
				if (rm[i])
					remove(i);
			}
			return changed;
		}
		
		// --- old API mapping
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.removeAllElements' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public void  removeAllElements()
		{
			Clear();
		}
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.removeElementAt' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public void  removeElementAt(int index)
		{
			remove(index);
		}
		//UPGRADE_NOTE: The equivalent of method 'java.util.Vector.removeElement' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
		public bool removeElement(System.Object obj)
		{
			return remove(obj);
		}
		
		// --- Map interface
		
		public virtual bool containsKey(System.Object key)
		{
			return (names == null)?false:names.Contains(key);
		}
		
		public virtual bool containsValue(System.Object value_Renamed)
		{
			return Contains(value_Renamed);
		}
		
		/// <summary>NOTE: THIS IS UNIMPLEMENTED and always returns <code>null</code>! Due to the fact that R lists are not proper maps we canot maintain a set-view of the list </summary>
		public virtual SupportClass.SetSupport entrySet()
		{
			return null;
		}
		
		public virtual System.Object get_Renamed(System.Object key)
		{
			return at((System.String) key);
		}
		
		/// <summary>Note: sinde RList is not really a Map, the returned set is only an approximation as it cannot reference duplicate or null names that may exist in the list </summary>
		public virtual SupportClass.SetSupport keySet()
		{
			if (names == null)
				return null;
			//UPGRADE_TODO: Class 'java.util.HashSet' was converted to 'SupportClass.HashSetSupport' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilHashSet'"
			return new SupportClass.HashSetSupport(names);
		}
		
		public virtual System.Object put(System.Object key, System.Object value_Renamed)
		{
			if (key == null)
			{
				Add(value_Renamed);
				return null;
			}
			if (names != null)
			{
				int p = names.IndexOf(key);
				if (p >= 0)
				{
					System.Object tempObject;
					tempObject = base[p];
					base[p] = value_Renamed;
					return tempObject;
				}
			}
			int i = Count;
			base.Add(value_Renamed);
			if (names == null)
				names = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList(i + 1));
			while (names.Count < i)
				names.Add(null);
			names.Add(key);
			return null;
		}
		
		public virtual void  putAll(System.Collections.IDictionary t)
		{
			if (t == null)
				return ;
			if (t is RList)
			{
				// we need some more sophistication for RLists as they may have null-names which we append
				RList l = (RList) t;
				if (names == null)
				{
					addAll(l);
					return ;
				}
				int n = l.Count;
				int i = 0;
				while (i < n)
				{
					System.String key = l.keyAt(i);
					if (key == null)
						Add(l.at(i));
					else
						put(key, l.at(i));
					i++;
				}
			}
			else
			{
				//UPGRADE_TODO: Method 'java.util.Map.keySet' was converted to 'SupportClass.HashSetSupport' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilMapkeySet'"
				SupportClass.SetSupport ks = new SupportClass.HashSetSupport(t.Keys);
				System.Collections.IEnumerator i = ks.GetEnumerator();
				//UPGRADE_TODO: Method 'java.util.Iterator.hasNext' was converted to 'System.Collections.IEnumerator.MoveNext' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilIteratorhasNext'"
				while (i.MoveNext())
				{
					//UPGRADE_TODO: Method 'java.util.Iterator.next' was converted to 'System.Collections.IEnumerator.Current' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilIteratornext'"
					System.Object key = i.Current;
					put(key, t[key]);
				}
			}
		}
		
		public virtual System.Object removeByKey(System.Object key)
		{
			if (names == null)
				return null;
			int i = names.IndexOf(key);
			if (i < 0)
				return null;
			System.Object o = this[i];
			removeElementAt(i);
			if (names is org.rosuda.REngine.RList)
				((org.rosuda.REngine.RList) names).removeElementAt(i);
			else
				names.RemoveAt(i);
			return o;
		}
		
		public virtual System.Collections.ICollection values()
		{
			return this;
		}
		
		// other
		public override string ToString()
		{
			return "RList" + SupportClass.CollectionToString((System.Collections.ArrayList) this) + "{" + (Named?"named,":"") + Count + "}";
		}
        /**
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Boolean Contains(System.Object value)
		{
			return false;
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Int32 IndexOf(System.Object value)
		{
			return 0;
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public void  Remove(System.Object value)
		{
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public void  RemoveAt(System.Int32 index)
		{
		}
		//UPGRADE_NOTE: The following method implementation was automatically added to preserve functionality. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1306'"
		virtual public void  CopyTo(System.Array array, System.Int32 index)
		{
			for (int i = index; i < this.Count; i++)
				array.SetValue(this[i], i);
		}
		//UPGRADE_TODO: The following method was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Collections.IEnumerator GetEnumerator()
		{
			return null;
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Object this[System.Int32 index]
		{
			get
			{
				return null;
			}
			
			set
			{
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Boolean IsReadOnly
		{
			get
			{
				return false;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Boolean IsFixedSize
		{
			get
			{
				return false;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Int32 Count
		{
			get
			{
				return 0;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Object SyncRoot
		{
			get
			{
				return null;
			}
			
		}
		//UPGRADE_TODO: The following property was automatically generated and it must be implemented in order to preserve the class logic. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1232'"
		virtual public System.Boolean IsSynchronized
		{
			get
			{
				return false;
			}
			
		}
         **/
	}
}
#endif