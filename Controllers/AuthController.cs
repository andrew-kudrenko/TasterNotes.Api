using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TasterNotes.Application.Models.Request.Auth;
using TasterNotes.Application.Models.Response.Users;
using TasterNotes.Application.Services.Auth;
using TasterNotes.Persistence;
using TasterNotes.Persistence.Models.Auth;
using TasterNotes.Utils;

namespace TasterNotes.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(AppDbContext db, JwtService jwtService, AuthService authService) : Controller
    {
        [Route("refresh")]
        [HttpPost]
        public async Task<IActionResult> RefreshAsync([FromBody] RefreshRequest body)
        {
            var sessionId = Request.Cookies[CookiesKeys.RefreshToken];

            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest(new ErrorResponse("No refresh token"));
            }

            var found = await db.RefreshSessions
                .Where(s => s.RefreshSessionId.Equals(Guid.Parse(sessionId)))
                .OrderBy(s => s.CreatedOn)
                .Include(s => s.User)
                .LastOrDefaultAsync();

            if (found is RefreshSession session)
            {
                db.Remove(found);
                await db.SaveChangesAsync();
                
                if (DateTime.UtcNow < session.ExpiresOn && session.Fingerprint == body.Fingerprint)
                {
                    var maxAge = TimeSpan.FromDays(30);
                    var newSession = await authService.AddRefreshSessionAsync(new()
                    {
                        UserId = session.UserId,
                        Fingerprint = body.Fingerprint,
                        CreatedOn = DateTime.UtcNow,
                        ExpiresOn = DateTime.UtcNow.Add(maxAge),
                    });

                    var refreshToken = newSession.RefreshSessionId.ToString();

                    Response.Cookies.Append(CookiesKeys.RefreshToken, refreshToken, new() { HttpOnly = true, Path = "/api/auth", MaxAge = maxAge });

                    return Ok(new
                    {
                        Access = jwtService.GenerateAccessToken(session.User),
                        Refresh = refreshToken,
                        User = UserMeResponse.FromUser(session.User),
                    });
                }

                return Unauthorized(new ErrorResponse("Invalid refresh session"));
            }

            return Unauthorized(new ErrorResponse("No refresh token by id"));
        }

        [Route("login")]
        [HttpPost]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest body)
        {
            var user = await authService.AuthenticateAsync(body);

            if (user is null)
            {
                return BadRequest(new ErrorResponse("Login or password is incorrect"));
            }

            var accessToken = jwtService.GenerateAccessToken(user);

            var now = DateTime.UtcNow;
            var maxAge = TimeSpan.FromDays(30);
            var session = await authService.AddRefreshSessionAsync(new()
            {
                UserId = user.UserId,
                Fingerprint = body.Fingerprint,
                CreatedOn = now,
                ExpiresOn = now.Add(maxAge),                
            });
            var refreshToken = session.RefreshSessionId.ToString();
            
            Response.Cookies.Append(CookiesKeys.RefreshToken, refreshToken, new() { HttpOnly = true, Path = "/api/auth", MaxAge = maxAge });
            Response.Cookies.Append(CookiesKeys.AuthorizedOn, now.ToString(), new() { MaxAge = maxAge });

            return Ok(new
            {
                User = UserMeResponse.FromUser(user),
                Tokens = new {
                    Access = accessToken,
                    Refresh = refreshToken,
                },
            });
        }

        [Route("register")]
        [HttpPost]
        public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest body)
        {
            if (await authService.AnyUserAsync(body))
            {
                return BadRequest(new ErrorResponse("Already registered"));
            }

            var user = await authService.CreateUserAsync(body);

            return StatusCode(StatusCodes.Status201Created, UserMeResponse.FromUser(user));
        }

        [Authorize]
        [Route("logout")]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var rawSessionId = Request.Cookies[CookiesKeys.RefreshToken];

            if (!string.IsNullOrEmpty(rawSessionId) && Guid.TryParse(rawSessionId, out var sessionId))
            {
                await authService.RemoveRefreshSession(sessionId);
                Response.Cookies.Delete(CookiesKeys.RefreshToken);
                Response.Cookies.Delete(CookiesKeys.AuthorizedOn);

                return Ok();
            }


            return BadRequest(new ErrorResponse("No refresh token"));
        }
    }
}
