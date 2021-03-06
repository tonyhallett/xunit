#if false

using System;
using Xunit.Abstractions;

namespace Xunit.v3
{
	/// <summary>
	/// Represents a test assembly.
	/// </summary>
	public interface _ITestAssembly
	{
		/// <summary>
		/// Gets the assembly that this test assembly belongs to.
		/// </summary>
		IAssemblyInfo Assembly { get; }

		/// <summary>
		/// Gets the full path of the configuration file name, if one is present.
		/// May be <c>null</c> if there is no configuration file.
		/// </summary>
		string? ConfigFileName { get; }

		/// <summary>
		/// Gets the assembly version.
		/// </summary>
		Version Version { get; }
	}
}

#endif
