using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRAVEL.Data;
using TRAVEL.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ItineraryController : ControllerBase
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<ItineraryController> _logger;

        public ItineraryController(TravelDbContext context, ILogger<ItineraryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Download itinerary PDF for a confirmed booking
        /// </summary>
        [HttpGet("download/{bookingId}")]
        [Authorize]
        public async Task<IActionResult> DownloadItinerary(int bookingId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            // Get booking with related data
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                    .ThenInclude(p => p.Images)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            // Verify user owns this booking or is admin
            var isAdmin = User.IsInRole("Admin");
            if (booking.UserId != userId && !isAdmin)
                return Forbid();

            // Only allow download for confirmed/completed bookings
            if (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.Completed)
                return BadRequest(new { success = false, message = "Itinerary is only available for confirmed bookings" });

            try
            {
                // Configure QuestPDF license (community license for open source)
                QuestPDF.Settings.License = LicenseType.Community;

                var pdfBytes = GenerateItineraryPdf(booking);

                var fileName = $"TRAVIL_Itinerary_{booking.BookingReference}_{booking.TravelPackage.Destination.Replace(" ", "_")}.pdf";

                _logger.LogInformation($"Itinerary PDF generated for booking {bookingId}");

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating itinerary PDF for booking {bookingId}");
                return StatusCode(500, new { success = false, message = "Error generating PDF" });
            }
        }

        private byte[] GenerateItineraryPdf(Booking booking)
        {
            var package = booking.TravelPackage;
            var user = booking.User;
            var duration = (package.EndDate - package.StartDate).Days;

            // Parse itinerary into days
            var itineraryDays = !string.IsNullOrEmpty(package.Itinerary)
                ? package.Itinerary.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("TRAVIL")
                                    .FontSize(28)
                                    .Bold()
                                    .FontColor(Colors.Amber.Darken2);

                                c.Item().Text("Luxury Travel Experiences")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken1);
                            });

                            row.ConstantItem(150).Column(c =>
                            {
                                c.Item().AlignRight().Text("TRAVEL ITINERARY")
                                    .FontSize(12)
                                    .Bold()
                                    .FontColor(Colors.Grey.Darken2);

                                c.Item().AlignRight().Text($"Ref: {booking.BookingReference}")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Medium);

                                c.Item().AlignRight().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Medium);
                            });
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(2).LineColor(Colors.Amber.Darken2);
                    });

                    // Content
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Destination Title
                        col.Item().PaddingBottom(15).Text($"{package.Destination}, {package.Country}")
                            .FontSize(24)
                            .Bold()
                            .FontColor(Colors.Grey.Darken3);

                        // Trip Overview Box
                        col.Item().PaddingBottom(20).Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.Grey.Lighten4).Padding(15).Column(overview =>
                            {
                                overview.Item().Text("TRIP OVERVIEW")
                                    .FontSize(12)
                                    .Bold()
                                    .FontColor(Colors.Amber.Darken2);

                                overview.Item().PaddingTop(10).Row(row =>
                                {
                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Travel Dates").FontSize(9).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text($"{package.StartDate:MMMM dd, yyyy} - {package.EndDate:MMMM dd, yyyy}")
                                            .FontSize(11).Bold();
                                    });

                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Duration").FontSize(9).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text($"{duration} Days / {Math.Max(duration - 1, 1)} Nights").FontSize(11).Bold();
                                    });

                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Category").FontSize(9).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text(package.PackageType.ToString()).FontSize(11).Bold();
                                    });
                                });

                                overview.Item().PaddingTop(10).Row(row =>
                                {
                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Guests").FontSize(9).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text($"{booking.NumberOfGuests} Guest(s)").FontSize(11).Bold();
                                    });

                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Rooms").FontSize(9).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text($"{booking.NumberOfRooms} Room(s)").FontSize(11).Bold();
                                    });

                                    row.RelativeItem().Column(c =>
                                    {
                                        c.Item().Text("Total Price").FontSize(9).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text($"${booking.TotalPrice:N2}").FontSize(11).Bold().FontColor(Colors.Green.Darken2);
                                    });
                                });
                            });

                        // Traveler Information
                        col.Item().PaddingBottom(20).Column(traveler =>
                        {
                            traveler.Item().Text("TRAVELER INFORMATION")
                                .FontSize(12)
                                .Bold()
                                .FontColor(Colors.Amber.Darken2);

                            traveler.Item().PaddingTop(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Name").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    c.Item().Text($"{user.FirstName} {user.LastName}").FontSize(11).Bold();
                                });

                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Email").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    c.Item().Text(user.Email).FontSize(11).Bold();
                                });

                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Phone").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    c.Item().Text(user.PhoneNumber ?? "Not provided").FontSize(11).Bold();
                                });
                            });
                        });

                        // Trip Description
                        col.Item().PaddingBottom(20).Column(desc =>
                        {
                            desc.Item().Text("ABOUT THIS TRIP")
                                .FontSize(12)
                                .Bold()
                                .FontColor(Colors.Amber.Darken2);

                            desc.Item().PaddingTop(10).Text(package.Description)
                                .FontSize(10)
                                .LineHeight(1.5f);
                        });

                        // Daily Itinerary
                        col.Item().PaddingBottom(10).Text("DAILY ITINERARY")
                            .FontSize(12)
                            .Bold()
                            .FontColor(Colors.Amber.Darken2);

                        if (itineraryDays.Length > 0)
                        {
                            for (int i = 0; i < itineraryDays.Length; i++)
                            {
                                var dayText = itineraryDays[i].Trim();
                                if (string.IsNullOrEmpty(dayText)) continue;

                                var dayNum = i + 1;
                                col.Item().PaddingBottom(8).Row(row =>
                                {
                                    row.ConstantItem(55).Background(Colors.Amber.Darken2)
                                        .Padding(5)
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Text($"Day {dayNum}")
                                        .FontSize(9)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    row.RelativeItem().PaddingLeft(10).AlignMiddle().Text(dayText)
                                        .FontSize(10)
                                        .LineHeight(1.4f);
                                });
                            }
                        }
                        else
                        {
                            // Default itinerary if none provided
                            col.Item().PaddingBottom(8).Text("Day 1: Arrival & Welcome - Arrive at your destination, transfer to accommodation, welcome dinner.")
                                .FontSize(10);
                            col.Item().PaddingBottom(8).Text($"Days 2-{Math.Max(duration - 1, 2)}: Exploration & Activities - Full days of guided tours, cultural experiences, and leisure time.")
                                .FontSize(10);
                            col.Item().PaddingBottom(8).Text($"Day {duration}: Departure - Check out and transfer to airport for your departure.")
                                .FontSize(10);
                        }

                        // What's Included
                        col.Item().PaddingTop(15).PaddingBottom(10).Text("WHAT'S INCLUDED")
                            .FontSize(12)
                            .Bold()
                            .FontColor(Colors.Amber.Darken2);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("✓ Premium Accommodation").FontSize(10);
                                c.Item().Text("✓ Daily Breakfast").FontSize(10);
                                c.Item().Text("✓ Airport Transfers").FontSize(10);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("✓ Guided Tours").FontSize(10);
                                c.Item().Text("✓ 24/7 Support").FontSize(10);
                                c.Item().Text("✓ Travel Insurance").FontSize(10);
                            });
                        });

                        // Important Information
                        col.Item().PaddingTop(20).Border(1).BorderColor(Colors.Red.Lighten2)
                            .Background(Colors.Red.Lighten5).Padding(10).Column(info =>
                            {
                                info.Item().Text("IMPORTANT INFORMATION")
                                    .FontSize(11)
                                    .Bold()
                                    .FontColor(Colors.Red.Darken2);

                                info.Item().PaddingTop(5).Text("• Please arrive at the airport at least 3 hours before departure")
                                    .FontSize(9);
                                info.Item().Text("• Carry a printed or digital copy of this itinerary")
                                    .FontSize(9);
                                info.Item().Text("• Ensure your passport is valid for at least 6 months beyond your travel dates")
                                    .FontSize(9);
                                info.Item().Text($"• Cancellation allowed up to 3 days before departure ({package.StartDate.AddDays(-3):MMM dd, yyyy})")
                                    .FontSize(9);
                            });

                        // Emergency Contact
                        col.Item().PaddingTop(15).Column(emergency =>
                        {
                            emergency.Item().Text("EMERGENCY CONTACTS")
                                .FontSize(11)
                                .Bold()
                                .FontColor(Colors.Grey.Darken2);

                            emergency.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("TRAVIL Support: +1 (234) 567-890").FontSize(9);
                                row.RelativeItem().Text("Email: support@travil.com").FontSize(9);
                            });
                        });
                    });

                    // Footer
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Text("TRAVIL - Your Journey, Our Passion")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Medium);

                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.Span("Page ").FontSize(9).FontColor(Colors.Grey.Medium);
                                text.CurrentPageNumber().FontSize(9);
                                text.Span(" of ").FontSize(9).FontColor(Colors.Grey.Medium);
                                text.TotalPages().FontSize(9);
                            });
                        });

                        col.Item().PaddingTop(5).AlignCenter().Text("www.travil.com | hello@travil.com | +1 (234) 567-890")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Medium);
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}