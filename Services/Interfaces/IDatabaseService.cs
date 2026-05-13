using SQLite;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task<SQLiteAsyncConnection> GetConnectionAsync();
    Task<List<T>> GetAllAsync<T>() where T : new();
    Task<T?> GetByIdAsync<T>(int id) where T : class, new();
    Task<int> InsertAsync<T>(T entity) where T : new();
    Task<int> UpdateAsync<T>(T entity) where T : new();
    Task<int> DeleteAsync<T>(T entity) where T : new();
    Task<List<T>> QueryAsync<T>(string query, params object[] args) where T : new();
    Task RunInTransactionAsync(Action<SQLiteConnection> action);
}
