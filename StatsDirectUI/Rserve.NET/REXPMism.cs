#if USE_R
// REngine - generic Java/R API
//
// Copyright (C) 2007,2008 Simon Urbanek
// --- for licensing information see LICENSE file in the distribution ---
//
//  REXPMismatch.java
//
//  Created by Simon Urbanek on 2007/05/03
//
//  $Id: REngineException.java 2555 2006-06-21 20:36:42Z urbaneks $
//
using System;
namespace org.rosuda.REngine
{
	
	/// <summary>This exception is thrown whenever the operation requested is not supported by the given R object type, e.g. using <tt>asStrings</tt> on an S4 object. Most {@link REXP} methods throw this exception. Previous R/Java interfaces were silently returning <code>null</code> in those cases, but using exceptions helps to write more robust code. </summary>
	[Serializable]
	public class REXPMismatchException:System.Exception
	{
		/// <summary>retrieve the exception sender/origin</summary>
		/// <returns> REXP object that triggered the exception 
		/// </returns>
		virtual public REXP Sender
		{
			get
			{
				return sender;
			}
			
		}
		/// <summary>get the assumed access type that was violated by the sender.</summary>
		/// <returns> string describing the access type. See {@link #REXPMismatchException} for details. 
		/// </returns>
		virtual public System.String Access
		{
			get
			{
				return access;
			}
			
		}
		internal REXP sender;
		internal System.String access;
		
		/// <summary>primary constructor. The exception message will be formed as "attempt to access &lt;REXP-class&gt; as &lt;access-string&gt;"</summary>
		/// <param name="sender">R object that triggered this exception (cannot be <code>null</code>!)
		/// </param>
		/// <param name="access">assumed type of the access that was requested. It should be a simple name of the assumed type (e.g. <tt>"vector"</tt>). The type name can be based on R semantics beyond basic types reflected by REXP classes. In cases where certain assertions were not satisfied, the string should be of the form <tt>"type (assertion)"</tt> (e.g. <tt>"data frame (must have dim>0)"</tt>). 
		/// </param>
		public REXPMismatchException(REXP sender, System.String access):base("attempt to access " + sender.GetType().FullName + " as " + access)
		{
			this.sender = sender;
			this.access = access;
		}
	}
}
#endif