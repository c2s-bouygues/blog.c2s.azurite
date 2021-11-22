Le développement de solutions compatibles Azure nous contraint souvent à utiliser ces mêmes outils également en local : **Azure Storage** en est un exemple parmi d'autres. Cela oblige les équipes à ouvrir un compte de stockage en ligne et à payer pour ce dernier.

Ce tutoriel va vous accompagner pas à pas dans la découverte d'**Azurite**, une alternative locale à Azure Storage.

## 1. Prérequis

En prérequis pour cette découverte d'**Azurite**, il est nécessaire d'installer les outils suivants :

- [Visual Studio Code](<https://code.visualstudio.com/>)
- L'extension **Thunder Client** de **Visual Studio Code** (recherchez `rangav.vscode-thunder-client` dans l'onglet **Extensions**)
- [Azure Storage Explorer](<https://azure.microsoft.com/fr-fr/features/storage-explorer/>)
- [Azurite](<https://github.com/Azure/Azurite#npm/>)

Afin de vérifier que **Azurite**  est correctement installé sur votre machine, vous pouvez ouvrir un terminal et taper la commande suivante :

```bash
azurite version
```

Pour faciliter les développements, il est nécessaire de récupérer le code de l'application de l'article [.NET APIs - Endpoints](https://github.com/c2s-bouygues/blog.c2s.endpoints).

La version finale du code associé à cet article est disponible ici : [https://github.com/c2s-bouygues/blog.c2s.azurite](https://github.com/c2s-bouygues/blog.c2s.azurite).

## 2. Configurer Azurite
La configuration d'**Azurite** est simple, il suffit de créer un dossier dans lequel seront stockées les données (au format json) et le tour est joué !

À l'aide d'un terminal, tapez la commande suivante : 
```bash
mkdir azurite
```

>**Note :**  Veillez à créer le dossier `azurite` à un endroit qui vous arrange, par exemple C:/Users/{votreNom}

Vous pouvez à présent lancer le serveur **Azurite** à l'aide de la commande suivante : 

```bash
azurite --silent --location azurite --debug azurite/debug.log
```
- `--silent` : cette option permet d'éviter d'avoir trop de logs dans le terminal
- `--location` : cette option permet de spécifier le dossier dans lequel se trouve (ou se trouveront) les données de la base locale
- `--debug` : cette option permet de specifier le fichier dans lequel les logs des opérations réalisées sur la base seront enregistrés

>**Note :**  La commande ci-dessus permet de lancer à la fois les services : table, queue et blob. Il est possible de les lancer unitairement en remplacant `Azurite` par : `azurite-table`, `azurite-queue` ou `azurite-blob`

Le serveur est à présent en route, essayons de nous y connecter avec **Azure Storage Explorer**.

Dans l'explorateur dépliez : 
  - **Local et attaché**
  - **Comptes de stockage**
  - **Émulateur - Ports par défaut (Key)**

Vous devez alors voir apparaitre les trois services `table`, `queue` et `blob`

![launch-azurite-azure-explorer-view][launch-azurite-azure-explorer-view]

Si vous dépliez l'un de ses trois services (dans l'objectif de voir son contenu), vous verrez apparaître les logs des requêtes vers **Azurite** sur votre terminal.

La mise en route d'**Azurite** est terminée, nous allons maintenant pouvoir y connecter notre application.

## 2. Créer des données

Commençons par créer notre modèle de données.

Notre application ayant pour but de créer, modifier et lister des utilisateurs, nous pourrons utiliser une [table](https://docs.microsoft.com/fr-fr/azure/storage/tables/table-storage-overview).

Créez un dossier `~/Entities` à la racine du projet.

Créez une classe nommée `User` dans ce répertoire et ajoutez y les propriétés comme suit : 

```c#
using System;

namespace blog.c2s.azurite.Entities
{
    public class User
    {
        public Guid Id { get; set; } // Identifiant technique
        public string FirstName { get; set; } 
        public string LastName { get; set; }
        public string Email { get; set; }
    }
}
```

La classe `User` à présent créée, nous allons pouvoir créer l'entité qui sera injectée dans notre table que nous nommerons plus tard `users`.

Créez une classe nommée `StoredUser` et ajoutez y les propriétés comme suit :

```c#
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;

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
            Response = JsonConvert.SerializeObject(userBase);
            PartitionKey = nameof(StoredMessage);
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
```
Cette classe permet d'encapsuler notre objet `User` sous la propriété `Response` et en ajoutant :
  - Une clé de partition (`PartitionKey`)
  - Une clé primaire au sein de la clé de partition (`RowKey`)
  - Une date d'expiration (`LifeTimeDuration`)

La propriété `User` sert simplement à faciliter l'utilisation des données (sérialisées et stockées sous la propriété `Response`).

❗ Attention à la taille maximale des données d'une [entité](https://docs.microsoft.com/en-us/azure/storage/tables/scalability-targets) et également au contenu de l'objet à séraliser.

>**Note :**  Vous aurez besoin d'installer le package NuGet `Microsoft.Azure.Cosmos.Table` pour avoir accès à la classe TableEntity. À noter que Visual Studio propose d'utiliser `WindowsAzure.Storage` **mais** ce package est annoté comme déprécié, ignorez donc ce message si vous le voyez.

Notre structure de données est prête, nous pouvons à présent voir comment y accéder.

## 3. Accéder aux données

L'utilisation d'**Azurite** est la même que celle d'**Azure Storage** : ces deux outils partagent la même API.

La différence se fera dans la déclaration de la **chaîne de connexion** (connectionString).

Nous allons donc créer un service chargé de réaliser les appels à la base de données.

À la racine du projet, créez un dossier `~/Services`.

Créez une classe nommée `AzureTableService` comme suit : 
```c#
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

```

Prêtez attention à la ligne suivante : 
```c#
var storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
```

Cette ligne permet en effet de récupérer par défaut la chaîne de connexion par défaut du [compte de stockage local](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.cloudstorageaccount.developmentstorageaccount?view=azure-dotnet).

Dans le contexte d'une application en production, on stockerait la chaîne de connexion dans la configuration de l'application (par exemple), et la valeur affectée à la variable `storageAccount` serait conditionnée par l'environnement dans lequel s'exécuterait l'application : 
- `Azurite` en local
- `Azure Storage` en production

Pour que cette classe `AzureTableService` fonctionne il nous faudra également opérer plusieurs actions supplémentaires : 
- créez l'interface `IAzureTableService` dans un sous-dossier `~/Services/Interfaces` comme suit :
```c#
using blog.c2s.azurite.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace blog.c2s.azurite.Services.Interfaces
{
    public interface IAzureTableService
    {
        Task<IEnumerable<StoredUser>> GetAllStoredUsers();
        Task<StoredUser> GetStoredUserById(Guid id);        
        Task InsertStoredUser(User user);
    }
}
```

- créez la classe `Constants` à la racine du projet comme suit : 
```c#
namespace blog.c2s.azurite
{
    public static class Constants
    {
        public static class CloudTables
        {
            public const string User = "users";
        }
    }
}
```

- modifiez la classe `Startup` comme suit :
```c#
// ...
using blog.c2s.azurite.Extensions;
using System.Reflection;

// ...
public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IAzureTableService, AzureTableService>();
}
```

>**Note**: cette dernière modification permet d'ajouter notre service dans l'injecteur de dépendance natif d'ASPNet Core.

Notre service d'accès aux données étant à présent implémenté, il ne nous reste plus qu'à connecter les `endpoints` de notre API à ce service.

Modifiez les classes suivantes dans le dossier `~/RequestDelegates/API` comme suit : 
- `GetUsersDelegate`
```c#
// ...
using blog.c2s.azurite.Extensions;
using blog.c2s.azurite.Services.Interfaces;
using System.Linq;

// ...
public static RequestDelegate Delegate => async context =>
{
    var serviceProvider = context.RequestServices;
    var logger = serviceProvider.GetService<ILogger<GetUsersDelegate>>();
    var azureTableService = serviceProvider.GetService<IAzureTableService>();
    try
    {
        var users = await azureTableService.GetAllStoredUsers();
        if(users == null)
        {
            context.NotFound();
            return;
        }else if (!users.Any())
        {
            context.NoContent();
            return;                  
        }
        else
        {                    
            await context.OK(users.Select(x => x.User));
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex.Message);
        logger.LogTrace(ex.StackTrace);
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        await context.Response.WriteAsync(ex.Message);
    }
};
```

- `GetUserByIdDelegate`
```c#
// ...
using blog.c2s.azurite.Extensions;
using blog.c2s.azurite.Services.Interfaces;

// ...
public static RequestDelegate Delegate => async context =>
{
    var serviceProvider = context.RequestServices;
    var logger = serviceProvider.GetService<ILogger<GetUserByIdDelegate>>();
    var azureTableService = serviceProvider.GetService<IAzureTableService>();
    try
    {
        // On récupère l'Id depuis la route
        var userId = context.FromRoute<Guid>("id");

        var user = await azureTableService.GetStoredUserById(userId, context.RequestAborted);
        if (user == null)
        {
            context.NotFound();
            return;
        }
        else
        {                    
            await context.OK(user.User);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex.Message);
        logger.LogTrace(ex.StackTrace);
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        await context.Response.WriteAsync(ex.Message);
    }
};
```

- `PostUserDelegate`
```c#
// ...
using blog.c2s.azurite.Entities;
using blog.c2s.azurite.Extensions;
using blog.c2s.azurite.Services.Interfaces;

// ...
public static RequestDelegate Delegate => async context =>
{
    var serviceProvider = context.RequestServices;
    var logger = serviceProvider.GetService<ILogger<PostUserDelegate>>();
    var azureTableService = serviceProvider.GetService<IAzureTableService>();
    try
    {
        var newUser = await context.FromBody<User>();
        newUser.Id = Guid.NewGuid();

        await azureTableService.InsertStoredUser(newUser, context.RequestAborted);

        context.NoContent();
    }
    catch (Exception ex)
    {
        logger.LogError(ex.Message);
        logger.LogTrace(ex.StackTrace);
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        await context.Response.WriteAsync(ex.Message);
    }
};
```

Vous avez du remarquer qu'il manque certaines méthodes : `HttpContext.FromRoute<T>()`, `HttpContext.FromBody<T>()`, `HttpContent.NoContent()`, etc.

Ces méthodes sont incluses dans une classe d'extensions de la classe `HttpContext` que vous pouvez retrouver [ici](https://github.com/c2s-bouygues/blog.c2s.azurite/blob/main/Extensions/HttpExtensions.cs).

Elle fournie différents helpers afin de rendre moins verbeux les `Delegate` de notre API.

Notre application est à présent fonctionnelle, il n'y a plus qu'à la tester !
Voici à présent l'arborescence du projet :

```console
Root
│   Constants.cs
│   Program.cs
│   Startup.cs   
│   
└───Entities
    │   StoredUser.cs
    │   User.cs
│   
└───Extensions
    │   HttpExtensions.cs
│
└───RequestDelegates
│   └───API
│       │   GetUserByIdDelegate.cs
│       │   GetUsersDelegate.cs
│       │   PostUserDelegate.cs
│       ... // Si vous avez entrepris d'ajouter d'autres `Delegate`
└───Routes
    │   APIRoutes.cs
│   
└───Services
    └───Interfaces
        │   IAzureTableService.cs
    │   IAzureTableService.cs

```

## 4. Tester l'API avec Thunder Client

Dans cette partie nous allons voir comment tester manuellement notre API.

Commencez d'abord par lancer Azurite.
```bash
azurite --silent --location azurite --debug azurite\debug.log
```

À présent lancez votre API avec Visual Studio (F5 ou via le bouton dans la barre d'action).

Notre API est à présent en route, dirigeons nous vers l'extension **Thunder Client**.

Dans **Visual Studio Code** vous devez avoir vu apparaître ce logo :

![thunder-client-logo][thunder-client-logo]

Cliquez dessus puis cliquez sur `New Request`.

Renseignez à présent les différents champs afin de récupérer la liste de tous les utilisateurs (ie `GetUsersDelegate`). Vous devriez avoir quelque chose similaire à ceci :

![thunder-client-get-all-users-request][thunder-client-get-all-users-request]

Exécutez votre requête, vous devriez avoir ce résultat :

![thunder-client-get-all-users-nocontent][thunder-client-get-all-users-nocontent]

Pas de données ??? Et oui ! Notre base de données est encore vide, il nous faut en ajouter 😁.

De la même manière que vous avez créé une requête pour récupérer les utilisateurs, faîtes de même pour créer un utilisateur (`PostUserDelegate`).

>**Note**: Regardez bien la façon dont sont récupérées les données dans l'API afin de déduire les champs de la requête à remplir.

Pour vérifier que la création a bien eu lieu, vous n'aurez qu'à relancer la récupération de tous les utilisateurs.
Vous pouvez également créer une requête pour chaque endpoint que vous aurez implémenté afin de vous exercer 😁.

Pour les plus impatients, vous trouverez dans le dossier `~/.thunderclient` du github un fichier à importer dans **Thunder Client** contenant les requêtes pré-remplies.

## 5. Conclusion

Nous avons vu comment utiliser **Azurite** en local au lieu d'utiliser un compte de stockage en ligne (**Azure Storage**).

Cela évite de payer un service pour des développements en local.
De plus, chaque membre de l'équipe a sa propre base de données ce qui facilite souvent les tests de chacun.

Nous avons également pris en main l'outil **Thunder Client** afin de tester manuellement notre API.
Il ne s'agit pas du seul outil permettant de le faire, **Postman** ou encore **SOAP UI** en sont d'autres exemples.

Nous ne nous sommes pas intéressés à l'utilisation d'**Azurite** lors de tests automatisés car il n'y a rien de spécifique à faire pour que cela fonctionne.
Il suffit de s'assurer que le service est lancé, d'une manière ou d'une autre.

Pour finir voici une liste de liens externes : 
- L'intégralité du code associé à cet article sur notre [Github](<https://github.com/c2s-bouygues/blog.c2s.azurite>)
- La documentation avancée d'[Azurite](<https://docs.microsoft.com/fr-fr/azure/storage/common/storage-use-azurite?tabs=visual-studio>) pour des configurations plus complètes / avancées
- Un [exemple](<https://docs.microsoft.com/fr-fr/azure/storage/blobs/use-azurite-to-run-automated-tests#run-tests-on-azure-pipelines>) de pipeline d'intégration dans des tests automatisés

[launch-azurite-azure-explorer-view]: https://devc2sagorablogstorage.blob.core.windows.net/public/azurite/azure-storage-explorer-local-db.png
[thunder-client-logo]: https://devc2sagorablogstorage.blob.core.windows.net/public/azurite/logo_thunder_client.png
[thunder-client-get-all-users-request]: https://devc2sagorablogstorage.blob.core.windows.net/public/azurite/get-all-users-request.png
[thunder-client-get-all-users-nocontent]: https://devc2sagorablogstorage.blob.core.windows.net/public/azurite/get-all-users-nocontent.png
