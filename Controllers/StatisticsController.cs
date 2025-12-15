using FaceCheck.Models;
using FaceCheck.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FaceCheck.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ILogger<StatisticsController> _logger;

        public StatisticsController(IStatisticsService statisticsService, ILogger<StatisticsController> logger)
        {
            _statisticsService = statisticsService;
            _logger = logger;
        }

        [HttpGet("current-month")]
        public async Task<ActionResult<UserAttendanceStatisticsResponse>> GetCurrentMonthStatistics()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Unauthorized(new UserAttendanceStatisticsResponse
                {
                    Success = false,
                    Message = "User not authenticated."
                });
            }

            var response = await _statisticsService.GetUserAttendanceStatisticsAsync(userId);
            
            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpGet("month/{year}/{month}")]
        public async Task<ActionResult<UserAttendanceStatisticsResponse>> GetMonthStatistics(int year, int month)
        {
            // Validate month
            if (month < 1 || month > 12)
            {
                return BadRequest(new UserAttendanceStatisticsResponse
                {
                    Success = false,
                    Message = "Month must be between 1 and 12."
                });
            }

            var userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Unauthorized(new UserAttendanceStatisticsResponse
                {
                    Success = false,
                    Message = "User not authenticated."
                });
            }

            var response = await _statisticsService.GetUserAttendanceStatisticsAsync(userId, year, month);
            
            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpGet("year/{year}")]
        public async Task<ActionResult<UserAttendanceStatisticsResponse>> GetYearStatistics(int year)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Unauthorized(new UserAttendanceStatisticsResponse
                {
                    Success = false,
                    Message = "User not authenticated."
                });
            }

            // Return current month in the specified year
            var today = DateTime.Now;
            var month = today.Year == year ? today.Month : 1;

            var response = await _statisticsService.GetUserAttendanceStatisticsAsync(userId, year, month);
            
            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return -1;
        }
    }
}
