using Aero.Core.Exceptions;

namespace Aero.Cms.Core;

/// <summary>
/// General exception for errors arising from with the Aero CMS library
/// </summary>
public class AeroCmsException(string message) : AeroException(message);
