using blog.c2s.azurite.Extensions;
using blog.c2s.azurite.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace blog.c2s.endpoints.RequestDelegates.Environment
{
    public class GetUserByIdDelegate
    {
        public static RequestDelegate Delegate => async context =>
          {
              var serviceProvider = context.RequestServices;
              var logger = serviceProvider.GetService<ILogger<GetUserByIdDelegate>>();
              var azureTableService = serviceProvider.GetService<IAzureTableService>();
              try
              {
                // On récupère l'Id depuis la route
                var userId = context.FromRoute<Guid>("id");

                  var user = await azureTableService.GetStoredUserByIdAsync(userId, context.RequestAborted);
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
    }
}