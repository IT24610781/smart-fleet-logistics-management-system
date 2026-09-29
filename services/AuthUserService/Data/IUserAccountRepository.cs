using AuthUserService.Models;

namespace AuthUserService.Data;

public interface IUserAccountRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task DeactivateDriverAsync(Guid id);
}
