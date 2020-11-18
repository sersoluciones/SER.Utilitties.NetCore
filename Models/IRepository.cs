using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Models
{
    public interface IRepository<T> where T : class
    {
        Task<dynamic> GetAll();
        Task<T> Find(Expression<Func<T, bool>> condition);
        Task<T> Add(T entity);
        Task<T> Update(T entity, Expression<Func<T, bool>> condition);
        Task<T> SelfUpdate(Expression<Func<T, bool>> condition, T entity);
        Task<T> Remove(Expression<Func<T, bool>> condition);
        Task<T> UploadFileAsync(IFormFile file, Expression<Func<T, bool>> condition, string propertyName, string S3Path);
        List<string> GetRolesUser();
        IQueryable<T> GetQueryable();
    }
}
