using FaceCheck.Models;
using FaceCheck.Services;
using Microsoft.AspNetCore.Mvc;

namespace FaceCheck.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Login with email and password to get JWT token
        /// </summary>
        /// <param name="request">LoginRequest containing email and password</param>
        /// <returns>LoginResponse with JWT token if successful</returns>
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            
            if (!response.Success)
            {
                return Unauthorized(response);
            }

            return Ok(response);
        }
    }
}
