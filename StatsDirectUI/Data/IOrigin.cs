namespace StatsDirect.Data
{
    ///  <summary>
    ///  The available origin types.
    ///  </summary>
    public enum OriginType
    {
        Worksheet
    }

    // TRANSMISSINGCOMMENT: Interface IOrigin
    public interface IOrigin 
    { 
        
        
        ///  <summary>
        ///  Returns the type of this Origin.
        ///  </summary>
        ///  <value></value>
        ///  <returns>The type of this origin, for use in a Select Case or switch() statement for downcasting</returns>
        ///  <remarks></remarks>
        OriginType Type { get; }
        
    } 
    
    
} 
