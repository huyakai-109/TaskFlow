using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace TaskFlow.Repositories.Repositories
{
    public interface IBaseRepository<T>
       where T : class
    {
        Task Add(T entity);

        Task Add(params T[] entities);

        Task Add(IEnumerable<T> entities);

        Task Update(T entity);

        Task Update(params T[] entities);

        Task Update(IEnumerable<T> entities);

        Task Delete(object id);

        Task Delete(object[] keyValues);

        Task Delete(T entity);

        Task Delete(params T[] entities);

        Task Delete(IEnumerable<T> entities);

        Task<T?> FindById(long id);

        Task<T?> Search(params object[] keyValues);

        Task<T?> Single(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
            bool disableTracking = true);

        Task<IQueryable<T>> QueryAll(bool disableTracking = true);

        Task<IQueryable<T>> QueryCondition(
            Expression<Func<T, bool>> expression,
            bool disableTracking = true);

        Task<bool> Any(Expression<Func<T, bool>> expression);

        Task<IQueryable<TType>> Select<TType>(Expression<Func<T, TType>> select);

        public Task<IQueryable<T>> QueryRaw(string sql, params object[] parameters);
    }

    public class BaseRepository<T>(DbContext context) : IBaseRepository<T>
        where T : class
    {
        protected DbSet<T> DbSet { get; } = context.Set<T>();

        protected DbContext DbContext { get; } = context ?? throw new ArgumentException(nameof(context));

        public async Task Add(T entity)
        {
            await DbSet.AddAsync(entity);
        }

        public async Task Add(params T[] entities)
        {
            await DbSet.AddRangeAsync(entities);
        }

        public async Task Add(IEnumerable<T> entities)
        {
            await DbSet.AddRangeAsync(entities);
        }

        public async Task Update(T entity)
        {
            DbSet.Update(entity);
            await Task.CompletedTask;
        }

        public async Task Update(params T[] entities)
        {
            DbSet.UpdateRange(entities);
            await Task.CompletedTask;
        }

        public async Task Update(IEnumerable<T> entities)
        {
            DbSet.UpdateRange(entities);
            await Task.CompletedTask;
        }

        public async Task Delete(object id)
        {
            var entity = await DbSet.FindAsync(id);
            if (entity != null)
            {
                DbSet.Remove(entity);
            }
        }

        public async Task Delete(object[] keyValues)
        {
            var entity = await DbSet.FindAsync(keyValues);

            if (entity != null)
            {
                DbSet.Remove(entity);
            }
        }

        public async Task Delete(T entity)
        {
            var typeInfo = typeof(T).GetTypeInfo();
            var key = DbContext.Model.FindEntityType(typeInfo)!.FindPrimaryKey()!.Properties.FirstOrDefault();
            var id = entity.GetType().GetProperty(key?.Name ?? string.Empty)?.GetValue(entity);
            if (id == null)
            {
                return;
            }

            await Delete(id);
        }

        public async Task Delete(params T[] entities)
        {
            DbSet.RemoveRange(entities);
            await Task.CompletedTask;
        }

        public async Task Delete(IEnumerable<T> entities)
        {
            DbSet.RemoveRange(entities);
            await Task.CompletedTask;
        }

        public async Task<T?> FindById(long id)
        {
            return await DbSet.FindAsync(id);
        }

        public async Task<T?> Search(params object[] keyValues)
        {
            return await DbSet.FindAsync(keyValues);
        }

        public async Task<T?> Single(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
            bool disableTracking = true)
        {
            IQueryable<T> query = DbSet;
            if (disableTracking)
            {
                query = query.AsNoTracking();
            }

            if (include != null)
            {
                query = include(query);
            }

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            if (orderBy != null)
            {
                return await orderBy(query).FirstOrDefaultAsync();
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<IQueryable<T>> QueryAll(bool disableTracking = true)
        {
            var queryable = disableTracking ? DbSet.AsNoTracking() : DbSet;
            return await Task.FromResult(queryable);
        }

        public async Task<IQueryable<T>> QueryCondition(
            Expression<Func<T, bool>> expression,
            bool disableTracking = true)
        {
            var queryable = disableTracking ? DbSet.Where(expression).AsNoTracking() : DbSet.Where(expression);
            return await Task.FromResult(queryable);
        }

        public async Task<bool> Any(Expression<Func<T, bool>> expression)
        {
            return await DbSet.AnyAsync(expression);
        }

        public async Task<IQueryable<TType>> Select<TType>(Expression<Func<T, TType>> select)
        {
            return await Task.FromResult(DbSet.Select(select));
        }

        public async Task<IQueryable<T>> QueryRaw(string sql, params object[] parameters)
        {
            return await Task.FromResult(DbSet.FromSqlRaw(sql, parameters));
        }
    }
}
