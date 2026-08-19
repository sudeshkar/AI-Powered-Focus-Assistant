using FocusAssistant.Core.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusAssistant.Data.EF
{
    /// <summary>
    /// Generic EF Core repository that creates a short-lived DbContext per call.
    /// </summary>
    /// <remarks>
    /// These services are registered as singletons and used from the window-poll
    /// timer, the idle timer and the UI thread at once. Holding one injected
    /// DbContext for the app's lifetime meant concurrent operations shared a
    /// context that is explicitly not thread-safe, producing intermittent
    /// "a second operation was started on this context" failures. A factory keeps
    /// each unit of work isolated.
    /// </remarks>
    public class BaseService<T> : IBaseService<T> where T : class
    {
        protected readonly IDbContextFactory<FocusAssistantDbContext> _contextFactory;

        public BaseService(IDbContextFactory<FocusAssistantDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<T> CreateAsync(T entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Set<T>().Add(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task CreateRangeAsync(IEnumerable<T> entities)
        {
            var list = entities as IList<T> ?? entities.ToList();
            if (list.Count == 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Set<T>().AddRange(list);
            await context.SaveChangesAsync();
        }

        public async Task<T?> GetByIdAsync(object id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().FindAsync(id);
        }

        public async Task<List<T>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<T> UpdateAsync(T entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Set<T>().Update(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(object id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var entity = await context.Set<T>().FindAsync(id);
            if (entity is null)
                return false;

            context.Set<T>().Remove(entity);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(object id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<T>().FindAsync(id) is not null;
        }

        public async Task<List<TResult>> QueryAsync<TResult>(Func<IQueryable<T>, IQueryable<TResult>> query)
        {
            ArgumentNullException.ThrowIfNull(query);

            await using var context = await _contextFactory.CreateDbContextAsync();
            return await query(context.Set<T>().AsNoTracking()).ToListAsync();
        }
    }
}
