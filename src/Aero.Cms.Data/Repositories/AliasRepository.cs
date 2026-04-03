using Aero.Cms.Core.Entities;
using Aero.Core.Railway;
using Aero.Marten;
using Aero.Marten.Optional;
using Marten;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Aero.Cms.Modules.Aliases;


public interface IAliasRepository : IMartenGenericRepositoryOption<AliasDocument>
{
    // add specific alias methods here if needed
}

/// <summary>
/// Provides data access and management operations for alias documents using a Marten document session.
/// </summary>
/// <param name="session">The document session used to interact with the underlying data store. Cannot be null.</param>
/// <param name="log">The logger instance used for logging repository operations. Cannot be null.</param>
public class AliasRepository(IDocumentSession session, ILogger<AliasRepository> log)
    : MartenGenericRepositoryOption<AliasDocument>(session, log), IAliasRepository
{

}


//public interface IAliasService
//{
//    Task<Result<AliasError, AliasDocument>> CreateAsync(CreateAliasRequest request, CancellationToken ct);
//    Task<Result<AliasError, AliasDocument>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct);
//    Task<Result<AliasError, AliasDocument>> DeleteAsync(DeleteAliasRequest request, CancellationToken ct);
//    Task<Result<AliasError, AliasDocument>> GetByIdAsync(long id, CancellationToken ct);
//    Task<Result<AliasError, AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken ct);
//    Task<Result<AliasError, AliasDocument>> GetByOldPathAsync(string oldPath, CancellationToken ct);
//    Task<IEnumerable<AliasDocument>> GetAllAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default);
//    Task<Result<AliasError, AliasDocument>> GeByPathAsync(long siteId, string path, CancellationToken ct);
//    Task<Result<AliasError, AliasDocument>> GetSiteId(long siteId, int page = 1, int rows = 10, CancellationToken ct = default);
//    Task<IEnumerable<AliasDocument>> FindAsync(Expression<Func<AliasDocument, bool>> predicate, int page = 1, int rows = 10, CancellationToken ct = default);
//}

//public class AliasService(IAliasRepository db) : IAliasService
//{
//    public async Task<Result<AliasError, AliasDocument>> CreateAsync(CreateAliasRequest request, CancellationToken ct)
//    {
//        return await db.InsertAsync(new AliasDocument
//        {
//            SiteId = request.siteId,
//            OldPath = request.OldPath,
//            NewPath = request.NewPath,
//            Notes = request.notes
//        }, ct);
//    }

//    public async Task<Result<AliasError, AliasDocument>> DeleteAsync(DeleteAliasRequest request, CancellationToken ct)
//    {
//        var res = await db.DeleteAsync(request.id, ct);

//        throw new NotImplementedException();
//        //return res switch
//        //{
//        //    true => new Result<AliasError, AliasDocument>.Ok { Value = null! },
//        //    false => new Result<AliasError, AliasDocument>.Failure { Error = new AliasError() }
//        //};
//    }

//    public async Task<IEnumerable<AliasDocument>> FindAsync(Expression<Func<AliasDocument, bool>> predicate, int page = 1, int rows = 10, CancellationToken ct = default)
//    {
//        var results = await db.FindAsync(predicate, ct);
//        return results ?? [];
//    }

//    public Task<Result<AliasError, AliasDocument>> GeByPathAsync(long siteId, string path, CancellationToken ct)
//    {
//        throw new NotImplementedException();
//    }

//    public async Task<IEnumerable<AliasDocument>> GetAllAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
//    {
//        throw new NotImplementedException();
//    }

//    public Task<Result<AliasError, AliasDocument>> GetByIdAsync(long id, CancellationToken ct)
//    {
//        throw new NotImplementedException();
//    }

//    public Task<Result<AliasError, AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken ct)
//    {
//        throw new NotImplementedException();
//    }

//    public Task<Result<AliasError, AliasDocument>> GetByOldPathAsync(string oldPath, CancellationToken ct)
//    {
//        throw new NotImplementedException();
//    }

//    public Task<Result<AliasError, AliasDocument>> GetSiteId(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
//    {
//        throw new NotImplementedException();
//    }

//    public Task<Result<AliasError, AliasDocument>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct)
//    {
//        throw new NotImplementedException();
//    }
//}