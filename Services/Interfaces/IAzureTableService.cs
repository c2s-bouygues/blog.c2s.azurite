using blog.c2s.azurite.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace blog.c2s.azurite.Services.Interfaces
{
    public interface IAzureTableService
    {
        Task<IEnumerable<StoredUser>> GetAllStoredUsersAsync();

        Task<StoredUser> GetStoredUserByIdAsync(Guid id, CancellationToken cancellationToken);

        Task InsertStoredUserAsync(User user, CancellationToken cancellationToken);
    }
}
