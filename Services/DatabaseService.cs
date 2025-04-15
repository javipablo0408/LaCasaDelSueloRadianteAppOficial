using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public DatabaseService(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<Cliente>().Wait();
        }

        public Task<List<Cliente>> GetClientesAsync()
        {
            return _database.Table<Cliente>().ToListAsync();
        }

        public Task<Cliente> GetClienteByNameAsync(string nombre)
        {
            return _database.Table<Cliente>().FirstOrDefaultAsync(c => c.NombreCompleto == nombre);
        }

        public Task<int> SaveClienteAsync(Cliente cliente)
        {
            if (cliente.Id != 0)
            {
                return _database.UpdateAsync(cliente);
            }
            else
            {
                return _database.InsertAsync(cliente);
            }
        }
    }
}