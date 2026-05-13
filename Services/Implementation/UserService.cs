using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class UserService : IUserService
{
    private readonly IDatabaseService _databaseService;

    public UserService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<User>().FirstOrDefaultAsync();
    }

    public async Task<User> CreateUserAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _databaseService.InsertAsync(user);
        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        await _databaseService.UpdateAsync(user);
        return user;
    }

    public async Task<bool> HasUserAccountAsync()
    {
        var db = await _databaseService.GetConnectionAsync();
        var count = await db.Table<User>().CountAsync();
        return count > 0;
    }

    public async Task DeleteUserAsync(int userId)
    {
        var user = await _databaseService.GetByIdAsync<User>(userId);
        if (user != null)
            await _databaseService.DeleteAsync(user);
    }
}
