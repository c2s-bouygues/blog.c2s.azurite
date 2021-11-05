using blog.c2s.azurite.Entities;
using blog.c2s.azurite.Services.Interfaces;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace blog.c2s.azurite.Services
{
    public class AzureTableService : IAzureTableService
    {
        private readonly ILogger<AzureTableService> _logger;
        private readonly CloudTable _table;

        #region Ctor.Dtor

        public AzureTableService(
            ILogger<AzureTableService> logger)
        {
            _logger = logger;

            // Récupération de la chaîne de connexion
            var storageAccount = CloudStorageAccount.DevelopmentStorageAccount;

            // Création du client pour intéragir avec le service Table
            var tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());

            // Création de la table
            var tableName = Constants.CloudTables.User;
            _table = tableClient.GetTableReference(tableName);
        }

        #endregion Ctor.Dtor

        private async Task InitializeCloudTableAsync()
        {
            if (await _table.CreateIfNotExistsAsync())
            {
                _logger.LogInformation("Created Table named: {0}", Constants.CloudTables.User);
            }
            else
            {
                _logger.LogInformation("Table {0} already exists", Constants.CloudTables.User);
            }
        }

        async Task IAzureTableService.InsertStoredUserAsync(User user, CancellationToken cancellationToken)
        {
            await InitializeCloudTableAsync();
            var storedUser = new StoredUser(user);
            var insertOperation = TableOperation.Insert(storedUser);
            var result = await _table.ExecuteAsync(insertOperation, cancellationToken);
            if (result.RequestCharge.HasValue)
                _logger.LogInformation($"RequestCharge de l'opération d'écriture: '{result.RequestCharge.Value}'");
        }

        async Task<IEnumerable<StoredUser>> IAzureTableService.GetAllStoredUsersAsync()
        {
            await InitializeCloudTableAsync();
            var tableResult = _table.ExecuteQuery(new TableQuery<StoredUser>()).ToList();
            return tableResult;
        }

        async Task<StoredUser> IAzureTableService.GetStoredUserByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            await InitializeCloudTableAsync();
            var retrieveOperation = TableOperation.Retrieve<StoredUser>(nameof(StoredUser), id.ToString());
            var tableResult = await _table.ExecuteAsync(retrieveOperation, cancellationToken);
            var result = tableResult.Result as StoredUser;
            return result;
        }
    }
}
