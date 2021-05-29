using DelegateDecompiler;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityFramworkExtentions.CustomTypes;

namespace EntityFramworkExtentions.QuerableExtensions
{
    public static class QuerableExtensions
    {
        public static IQueryable<T> Include<T, TEnumerable>(this IQueryable<T> queryable,
            Expression<Func<T, IEnumerable<TEnumerable>>> path)
        {
            var newPath = (Expression<Func<T, IEnumerable<TEnumerable>>>)
                DecompileExpressionVisitor.Decompile(path);
            return QueryableExtensions.Include(queryable, newPath);
        }

        public static IOrderedQueryable<T> SortBy<T>(this IQueryable<T> source, string property)
        {
            bool isAsc = !property.EndsWith(" desc");
            if (!isAsc)
            {
                property = property.Replace(" desc", "");
                return ApplyOrder(source, property, "OrderByDescending");
            }

            return ApplyOrder(source, property, "OrderBy");
        }
        public static IOrderedQueryable<T> SortByDescending<T>(this IQueryable<T> source, string property)
        {
            return ApplyOrder(source, property, "OrderByDescending");
        }
        public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, string property)
        {
            return ApplyOrder(source, property, "ThenBy");
        }
        public static IOrderedQueryable<T> ThenByDescending<T>(this IOrderedQueryable<T> source, string property)
        {
            return ApplyOrder(source, property, "ThenByDescending");
        }
        static IOrderedQueryable<T> ApplyOrder<T>(IQueryable<T> source, string property, string methodName)
        {
            var props = property.Split('.');
            var type = typeof(T);
            var arg = Expression.Parameter(type, "x");
            Expression expr = arg;
            foreach (var prop in props)
            {
                // use reflection (not ComponentModel) to mirror LINQ
                var pi = type.GetProperty(prop);
                expr = Expression.Property(expr, pi ?? throw new InvalidOperationException());
                type = pi.PropertyType;
            }
            var delegateType = typeof(Func<,>).MakeGenericType(typeof(T), type);
            var lambda = Expression.Lambda(delegateType, expr, arg);

            var result = typeof(Queryable).GetMethods().Single(
                    method => method.Name == methodName
                            && method.IsGenericMethodDefinition
                            && method.GetGenericArguments().Length == 2
                            && method.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T), type)
                    .Invoke(null, new object[] { source, lambda });
            return (IOrderedQueryable<T>)result;
        }
        public static async Task<DataCollection<TEntity>> GetListWithPagingAsync<TEntity>(
            this IQueryable<TEntity> dbQuery, int pageSize, int currentPage, string keySelector,
            bool isDesc)
        {
            if (currentPage == 0 || pageSize == 0)
                return null;

            if (string.IsNullOrWhiteSpace(keySelector))
                throw new NotSupportedException("In Paging there should be a sorting key");

            var itemCollection =
                new DataCollection<TEntity> { TotalItemCount = await dbQuery.CountAsync().ConfigureAwait(false) };
            if (itemCollection.TotalItemCount == 0)
                return itemCollection;
            itemCollection.TotalPageCount =
                (int)Math.Ceiling(itemCollection.TotalItemCount / (double)pageSize);

            dbQuery = isDesc ? dbQuery.SortByDescending(keySelector) : dbQuery.SortBy((keySelector));

            dbQuery = dbQuery.Skip((currentPage - 1) * pageSize).Take(pageSize);
            itemCollection.ThisPageItemCount = await dbQuery.CountAsync().ConfigureAwait(false);
            itemCollection.Items = await dbQuery.ToListAsync().ConfigureAwait(false);
            return itemCollection;
        }
        public static async Task<DataCollection<TEntity>> GetListWithPagingAsync<TEntity>(
         this IQueryable<TEntity> dbQuery, int pageSize, int currentPage, string keySelector,
         bool isDesc, Expression<Func<TEntity, bool>> where)
        {
            if (currentPage == 0 || pageSize == 0)
                return null;

            if (string.IsNullOrWhiteSpace(keySelector))
                throw new NotSupportedException("In Paging there should be a sorting key");


            var itemCollection = new DataCollection<TEntity>();
            dbQuery = dbQuery.Where(where);
            itemCollection.TotalItemCount = await dbQuery.CountAsync().ConfigureAwait(false);
            if (itemCollection.TotalItemCount == 0)
                return itemCollection;

            itemCollection.TotalPageCount =
                (int)Math.Ceiling(itemCollection.TotalItemCount / (double)pageSize);

            dbQuery = isDesc ? dbQuery.SortByDescending(keySelector) : dbQuery.SortBy(keySelector);

            dbQuery = dbQuery.Skip((currentPage - 1) * pageSize).Take(pageSize);
            itemCollection.ThisPageItemCount = await dbQuery.CountAsync().ConfigureAwait(false);
            itemCollection.Items = await dbQuery
                .ToListAsync().ConfigureAwait(false);

            return itemCollection;
        }
    }
}
