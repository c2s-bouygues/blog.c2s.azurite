using blog.c2s.azurite.Extensions;
using blog.c2s.azurite.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;

namespace blog.c2s.endpoints.RequestDelegates.Environment
{
    public class GetUsersDelegate
    {
        public static RequestDelegate Delegate => async context =>
        {
            var serviceProvider = context.RequestServices;
            var logger = serviceProvider.GetService<ILogger<GetUsersDelegate>>();
            var azureTableService = serviceProvider.GetService<IAzureTableService>();
            try
            {
                var users = await azureTableService.GetAllStoredUsersAsync();
                if (users == null)
                {
                    context.NotFound();
                    return;
                }
                else if (!users.Any())
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
    }
}
