#if USE_R
namespace org.rosuda.REngine
{
	
	/// <summary>Basic class representing an object of any type in R. Each type in R in represented by a specific subclass.
	/// <p>
	/// This class defines basic accessor methods (<tt>as</tt><i>XXX</i>), type check methods (<tt>is</tt><i>XXX</i>), gives access to attributes ({@link #getAttribute}, {@link #hasAttribute}) as well as several convenience methods. If a given method is not applicable to a particular type, it will throw the {@link REXPMismatchException} exception.
	/// <p>This root class will throw on any accessor call and returns <code>false</code> for all type methods. This allows subclasses to override accessor and type methods selectively.
	/// </summary>
	public class REXP
	{
		virtual public bool String
		{
			// type checks
			
			get
			{
				return false;
			}
			
		}
		virtual public bool Numeric
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Integer
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Null
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Factor
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool List
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Logical
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Environment
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Language
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Expression
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Symbol
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Vector
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Raw
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Complex
		{
			get
			{
				return false;
			}
			
		}
		virtual public bool Recursive
		{
			get
			{
				return false;
			}
			
		}
		/// <summary>attribute list. This attribute should never be accessed directly. </summary>
		protected internal REXPList attr;
		
		/// <summary>public root contrsuctor, same as <tt>new REXP(null)</tt> </summary>
		public REXP()
		{
		}
		/// <summary>public root constructor</summary>
		/// <param name="attr">attribute list object (can be <code>null</code> 
		/// </param>
		public REXP(REXPList attr)
		{
			this.attr = attr;
		}
		
		// basic accessor methods
		/// <summary>returns the contents as an array of Strings (if supported by the represented object) </summary>
		public virtual System.String[] asStrings()
		{
			throw new REXPMismatchException(this, "String");
		}
		/// <summary>returns the contents as an array of integers (if supported by the represented object) </summary>
		public virtual int[] asIntegers()
		{
			throw new REXPMismatchException(this, "int");
		}
		/// <summary>returns the contents as an array of doubles (if supported by the represented object) </summary>
		public virtual double[] asDoubles()
		{
			throw new REXPMismatchException(this, "double");
		}
		/// <summary>returns the contents as an array of bytes (if supported by the represented object) </summary>
		public virtual byte /* sbyte */[] asBytes()
		{
			throw new REXPMismatchException(this, "byte");
		}
		/// <summary>returns the contents as a (named) list (if supported by the represented object) </summary>
		public virtual RList asList()
		{
			throw new REXPMismatchException(this, "list");
		}
		/// <summary>returns the contents as a factor (if supported by the represented object) </summary>
		public virtual RFactor asFactor()
		{
			throw new REXPMismatchException(this, "factor");
		}
		
		/// <summary>returns the length of a vector object. Note that we use R semantics here, i.e. a matrix will have a length of <i>m * n</i> since it is represented by a single vector (see {@link #dim} for retrieving matrix and multidimentional-array dimensions).</summary>
		/// <returns> length (number of elements) in a vector object
		/// </returns>
		/// <throws>  REXPMismatchException if this is not a vector object  </throws>
		public virtual int length()
		{
			throw new REXPMismatchException(this, "vector");
		}
		
		// convenience accessor methods
		/// <summary>convenience method corresponding to <code>asIntegers()[0]</code> </summary>
		public virtual int asInteger()
		{
			int[] i = asIntegers(); return i[0];
		}
		/// <summary>convenience method corresponding to <code>asDoubles()[0]</code> </summary>
		public virtual double asDouble()
		{
			double[] d = asDoubles(); return d[0];
		}
		/// <summary>convenience method corresponding to <code>asStrings()[0]</code> </summary>
		public virtual System.String asString()
		{
			System.String[] s = asStrings(); return s[0];
		}
		
		// methods common to all REXPs
		
		/// <summary>retrieve an attribute of the given name from this object</summary>
		/// <param name="name">attribute name
		/// </param>
		/// <returns> attribute value or <code>null</code> if the attribute does not exist 
		/// </returns>
		public virtual REXP getAttribute(System.String name)
		{
			//UPGRADE_NOTE: Final was removed from the declaration of 'a '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
			REXPList a = _attr();
			if (a == null || !a.List)
				return null;
			return a.asList().at(name);
		}
		
		/// <summary>checks whether this obejct has a given attribute</summary>
		/// <param name="name">attribute name
		/// </param>
		/// <returns> <code>true</code> if the attribute exists, <code>false</code> otherwise 
		/// </returns>
		public virtual bool hasAttribute(System.String name)
		{
			//UPGRADE_NOTE: Final was removed from the declaration of 'a '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
			REXPList a = _attr();
			return (a != null && a.List && a.asList().at(name) != null);
		}
		
		
		// helper methods common to all REXPs
		
		/// <summary>returns dimensions of the object (as determined by the "<code>dim</code>" attribute)</summary>
		/// <returns> an array of integers with corresponding dimensions or <code>null</code> if the object has no dimension attribute 
		/// </returns>
		public virtual int[] dim()
		{
			try
			{
				return hasAttribute("dim")?_attr().asList().at("dim").asIntegers():null;
			}
			catch (REXPMismatchException)
			{
			}
			return null;
		}
		
		/// <summary>determines whether this object inherits from a given class in tha same fashion as the <code>inherits()</code> function in R does (i.e. ignoring S4 inheritance)</summary>
		/// <param name="klass">class name
		/// </param>
		/// <returns> <code>true</code> if this object is of the class <code>klass</code>, <code>false</code> otherwise 
		/// </returns>
		public virtual bool inherits(System.String klass)
		{
			if (!hasAttribute("class"))
				return false;
			try
			{
				System.String[] c = getAttribute("class").asStrings();
				if (c != null)
				{
					int i = 0;
					while (i < c.Length)
					{
						if (c[i] != null && c[i].Equals(klass))
							return true;
						i++;
					}
				}
			}
			catch (REXPMismatchException)
			{
			}
			return false;
		}
		
		/// <summary>this method allows a limited access to object's attributes. {@link #getAttribute} should be used instead to access specific attributes. Note that the {@link #attr} attribute should never be used directly incase the REXP implements a lazy access (e.g. via a reference)</summary>
		/// <returns> list of attributes or <code>null</code> if the object has no attributes
		/// </returns>
		public virtual REXPList _attr()
		{
			return attr;
		}
		
		public override System.String ToString()
		{
			return base.ToString() + ((attr != null)?"+":"");
		}
		
		/// <summary>returns representation that it useful for debugging (e.g. it includes attributes) </summary>
		public virtual System.String toDebugString()
		{
			return (attr != null)?(("<" + attr.toDebugString() + ">") + base.ToString()):base.ToString();
		}
		
		//======= complex convenience methods
		/// <summary>returns the content of the REXP as a matrix of doubles (2D-array: m[rows][cols]). This is the same form as used by popular math packages for Java, such as 
		/// JAMA. This means that following leads to desired results:<br>
		/// <code>Matrix m=new Matrix(c.eval("matrix(c(1,2,3,4,5,6),2,3)").asDoubleMatrix());</code>
		/// </summary>
		/// <returns> 2D array of doubles in the form double[rows][cols] or <code>null</code> if the contents is no 2-dimensional matrix of doubles 
		/// </returns>
		public virtual double[][] asDoubleMatrix()
		{
			double[] ct = asDoubles();
			REXP dim = getAttribute("dim");
			if (dim == null)
				throw new REXPMismatchException(this, "matrix (dim attribute missing)");
			int[] ds = dim.asIntegers();
			if (ds.Length != 2)
				throw new REXPMismatchException(this, "matrix (wrong dimensionality)");
			int m = ds[0], n = ds[1];
			double[][] r = new double[m][];
			for (int i = 0; i < m; i++)
			{
				r[i] = new double[n];
			}
			// R stores matrices as matrix(c(1,2,3,4),2,2) = col1:(1,2), col2:(3,4)
			// we need to copy everything, since we create 2d array from 1d array
			int i2 = 0, k = 0;
			while (i2 < n)
			{
				int j = 0;
				while (j < m)
				{
					r[j++][i2] = ct[k++];
				}
				i2++;
			}
			return r;
		}
		
		
		//======= tools
		/// <summary>creates a data frame object from a list object using integer row names</summary>
		/// <param name="l">a (named) list of vectors ({@link REXPVector} subclasses), each element corresponds to a column and all elements must have the same length
		/// </param>
		/// <returns> a data frame object
		/// </returns>
		/// <throws>  REXPMismatchException if the list is empty or any of the elements is not a vector  </throws>
		public static REXP createDataFrame(RList l)
		{
			if (l == null || l.Count < 1)
				throw new REXPMismatchException(new REXPList(l), "data frame (must have dim>0)");
			if (!(l.at(0) is REXPVector))
				throw new REXPMismatchException(new REXPList(l), "data frame (contents must be vectors)");
			REXPVector fe = (REXPVector) l.at(0);
			return new REXPGenericVector(l, new REXPList(new RList(new REXP[]{new REXPString("data.frame"), new REXPString(l.keys()), new REXPInteger(new int[]{REXPInteger.NA, - fe.length()})}, new System.String[]{"class", "names", "row.names"})));
		}
		
		/// <summary>specifies how many items of a vector or list will be displayed in {@link #toDebugString} </summary>
		public static int maxDebugItems = 32;
	}
}
#endif