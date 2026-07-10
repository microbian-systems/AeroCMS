using Aero.Marten;
using Aero.Cms.Core.Models;

namespace Aero.Cms.Modules.Media;

/// <summary>
/// Defines an interface for IMediaRepository.
/// </summary>
public interface IMediaRepository : IGenericMartenRepository<MediaAsset>;

/// <summary>
/// Represents a class for MediaRepository.
/// </summary>
public class MediaRepository(IDocumentSession session, ILogger<MediaRepository> log)
    : GenericMartenRepository<MediaAsset>(session, log), IMediaRepository;
