using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Terminkalender.Data;
using Terminkalender.Models;
using Terminkalender.Services;

namespace Terminkalender.Controllers
{
    public class ReservationsController : Controller
    {
        private readonly TerminkalenderContext _context;
        private readonly ReservationService _reservationService;
        private readonly ILogger<ReservationsController> _logger;

        public ReservationsController(TerminkalenderContext context, ReservationService reservationService, ILogger<ReservationsController> logger)
        {
            _context = context;
            _reservationService = reservationService;
            _logger = logger;
        }

        // Index: Zeigt alle Reservationen
        public async Task<IActionResult> Index()
        {
            var reservations = await _context.Reservations.ToListAsync();
            return View(reservations);
        }

        // Create: Formular Create
        public IActionResult Create()
        {
            return View();
        }

        // Create (POST): Neue Reservierung speichern
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            if (ModelState.IsValid)
            {
                // Prüfe, ob der Raum verfügbar ist
                if (_reservationService.IsRoomAvailable(reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime))
                {
                    reservation.PrivateKey = Guid.NewGuid();
                    reservation.PublicKey = Guid.NewGuid();
                    _context.Add(reservation);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", "Der Raum ist zur angegebenen Zeit bereits reserviert.");
                }
            }
            return View(reservation);
        }

        // Edit: Formular zum Bearbeiten einer bestehenden Reservierung anzeigen
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }
            return View(reservation);
        }

        // Edit (POST): Bearbeitete Reservierung speichern
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reservation reservation)
        {
            if (id != reservation.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (_reservationService.IsRoomAvailable(reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime))
                    {
                        _context.Update(reservation);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Der Raum ist zur angegebenen Zeit bereits reserviert.");
                        return View(reservation);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Bearbeiten der Reservation mit ID {Id}", id);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(reservation);
        }

        // Delete: Bestätigungsseite anzeigen
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservations.FirstOrDefaultAsync(m => m.Id == id);
            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // DeleteConfirmed (POST): Reservierung löschen
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation != null)
            {
                _context.Reservations.Remove(reservation);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
