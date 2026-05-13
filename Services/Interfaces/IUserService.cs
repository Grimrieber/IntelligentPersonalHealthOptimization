using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IUserService
{
    Task<User?> GetCurrentUserAsync();
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task<bool> HasUserAccountAsync();
    Task DeleteUserAsync(int userId);
}
