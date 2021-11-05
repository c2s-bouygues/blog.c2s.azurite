using blog.c2s.azurite.Entities;
using blog.c2s.azurite.Extensions;
using blog.c2s.azurite.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace blog.c2s.endpoints.RequestDelegates.Environment
{
    public class PostUserDelegate
    {
        public static RequestDelegate Delegate => async context =>
        {
            var serviceProvider = context.RequestServices;
            var logger = serviceProvider.GetService<ILogger<PostUserDelegate>>();
            var azureTableService = serviceProvider.GetService<IAzureTableService>();
            try
            {
                var newUser = await context.FromBody<User>();
                newUser.Id = Guid.NewGuid();

                await azureTableService.InsertStoredUserAsync(newUser, context.RequestAborted);

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
    }
}