using Marten;
using Aero.Marten;
using Aero.Cms.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Media;

public interface IMediaRepository : IGenericMartenRepository<MediaAsset>;

public class MediaRepository(IDocumentSession session, ILogger<MediaRepository> log)
    : GenericMartenRepository<MediaAsset>(session, log), IMediaRepository;
