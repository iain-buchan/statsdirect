#if USE_R
// REngine - generic Java/R API
//
// Copyright (C) 2006 Simon Urbanek
// --- for licensing information see LICENSE file in the original JRclient distribution ---
//
//  RSrvException.java
//
//  Created by Simon Urbanek on Wed Jun 21 2006.
//
//  $Id: REngineException.java 2555 2006-06-21 20:36:42Z urbaneks $
//
using System;
namespace org.rosuda.REngine
{
	
	[Serializable]
	public class REngineException:System.Exception
	{
		protected internal REngine engine;
		
		public REngineException(REngine engine, System.String msg)
            : base(msg)
		{
			this.engine = engine;
		}

        public REngineException(REngine engine, System.String msg, Exception innerException)
            : base(msg, innerException)
        {
            this.engine = engine;
        }
    }
}
#endif