namespace StatsDirect.Data
{
    ///  <summary>
    ///  Data providers that can refill freeze-dried data in IStripForRedo should implement this interface.
    ///  </summary>
    ///  <remarks>Strictly, this should be part of the template system.  Unfortunately template requires data, and this would require a circular dependency.</remarks>
    public interface IRefillSource 
    { 
        ///  <summary>
        ///  Request that v is filled from its origin
        ///  </summary>
        ///  <param name="v">The variable to be refilled</param>
        void Refill( Variable v );
        
    } 
    
    
} 
