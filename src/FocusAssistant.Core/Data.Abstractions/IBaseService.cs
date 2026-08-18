using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Data.Abstractions
{
    /// <summary>
    /// Persistence for a single entity type. Every call runs against its own
    /// DbContext, so these services are safe to share across threads.
    /// </summary>
    public interface IBaseService<T> where T : class
    {
        Task<T> CreateAsync(T entity);

        /// <summary>Inserts many entities in one round trip.</summary>
        Task CreateRangeAsync(IEnumerable<T> entities);

        Task<T?> GetByIdAsync(object id);
        Task<List<T>> GetAllAsync();
        Task<T> UpdateAsync(T entity);
        Task<bool> DeleteAsync(object id);
        Task<bool> ExistsAsync(object id);

        /// <summary>
        /// Runs a caller-supplied query server-side. Lets callers filter in SQL
        /// instead of loading whole tables and filtering in memory.
        /// </summary>
        Task<List<TResult>> QueryAsync<TResult>(Func<IQueryable<T>, IQueryable<TResult>> query);
    }
}
