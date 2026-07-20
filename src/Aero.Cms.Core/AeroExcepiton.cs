using Aero.Core.Exceptions;

namespace Aero.Cms.Core;

/// <summary>
/// Represents an AeroCMS-specific exceptional failure.
/// </summary>
/// <param name="message">The error message.</param>
public class AeroCmsException(string message) : AeroException(message);
