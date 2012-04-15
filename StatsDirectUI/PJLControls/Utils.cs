using System;

namespace PJLControls
{
	/// <summary>
	/// Utility class.
	/// </summary>
	public static class Utils
	{
		/// <summary>
		/// Check that the supplied enum value belongs to the supplied System.Type.<br></br>
		/// If not throw an InvalidEnumArgumentException.
		/// </summary>
		/// <param name="argumentName">The name of the argument.</param>
		/// <param name="enumValue">The enum value.</param>
		/// <param name="enumClass">The type of the enum that 'enumValue' should belong to.</param>
		public static void CheckValidEnumValue( string argumentName, object enumValue, Type enumClass )
		{
			if( !Enum.IsDefined(enumClass, enumValue) )
			{
				throw new System.ComponentModel.InvalidEnumArgumentException( argumentName, (int)enumValue, enumClass );
			}
		}
	}
}
