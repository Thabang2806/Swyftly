using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Mabuntle.Infrastructure.Persistence;

public sealed class MabuntleDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is not MabuntleDbContext)
        {
            return (context.GetType(), designTime);
        }

        return (context.GetType(), context.Database.ProviderName, designTime);
    }
}
