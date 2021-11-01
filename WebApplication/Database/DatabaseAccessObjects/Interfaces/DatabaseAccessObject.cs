using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApplication.Models;
using WebApplication.Models.DataTransferObjects;
using WebApplication.Models.Interfaces;

namespace WebApplication.Database.DatabaseAccessObjects.Interfaces
{
    public interface IDatabaseAccessObject<T> where T : DataTransferObjectBase
    {
        IEnumerable<T> GetAll(Predicate<T> condition = null);
        void CreateTableIfNotExists();
        bool Remove(T obj);
        Task<Messenger> Insert(Dictionary<string,object> parameters);
        Task<Messenger> Get(string name = null, string id = null);
        // Task<Messenger> Edit(Dictionary<string,Dictionary<object,Type>> edit,Dictionary<string,object> where);
    }
}