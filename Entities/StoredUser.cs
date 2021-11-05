using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;

namespace blog.c2s.azurite.Entities
{
    namespace blog.c2s.azurite.Entities
    {
        public class StoredUser : TableEntity
        {
            public const int LifeTimeDuration = 15;

            public string Response { get; set; }
            public DateTimeOffset ExpirationDate { get; set; }

            public StoredUser() { }

            public StoredUser(User userBase)
            {
                var serializeResponse = JsonConvert.SerializeObject(userBase);
                Response = serializeResponse;
                PartitionKey = nameof(StoredUser);
                RowKey = userBase.Id.ToString();
                ExpirationDate = DateTimeOffset.Now.AddMinutes(LifeTimeDuration);
            }

            public User User
            {
                get
                {
                    var deserializedResponse = JsonConvert.DeserializeObject<User>(Response);
                    return deserializedResponse;
                }
            }
        }
    }
}
