using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TasterNotes.Persistence;
using TasterNotes.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using TasterNotes.Application.Models.Response.Users;

namespace TasterNotes.Api.Controllers
{
    [Authorize]
    [Route("api/users")]
    [ApiController]
    public class UsersController(AppDbContext db) : ControllerBase
    {
        [Route("me")]
        public async Task<UserMeResponse?> GetMeAsync()
        {
            var userId = User.GetUserId();

            var user = await db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.UserId.Equals(userId));

            return user is not null ? UserMeResponse.FromUser(user) : null;
        }
    }
}
