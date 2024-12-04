using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Terminkalender.Data;
using Terminkalender.Models;
using Terminkalender.Services;
using Terminkalender.Validators;

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
            _logger.LogInformation("Index action invoked.");
            var reservations = await _context.Reservations.ToListAsync();
            _logger.LogInformation("Fetched {Count} reservations from the database.", reservations.Count);
            return View(reservations);
        }

        // Create: Formular Create
        public IActionResult Create()
        {
            _logger.LogInformation("Create (GET) action invoked.");
            return View();
        }

        // Create (POST): Neue Reservierung speichern
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            _logger.LogInformation("Create (POST) action invoked with reservation details: {Reservation}", reservation);


            if (ModelState.IsValid)
            {
                // Prüfe, ob der Raum verfügbar ist
                _logger.LogInformation("Checking room availability for room: {Room}, date: {Date}, start: {StartTime}, end: {EndTime}",
                    reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime);

                if (_reservationService.IsRoomAvailable(reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime, reservation.Id))
                {
                    reservation.PrivateKey = Guid.NewGuid();
                    reservation.PublicKey = Guid.NewGuid();
                    _context.Add(reservation);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Reservation created successfully with ID: {Id}", reservation.Id);
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    _logger.LogWarning("Room is not available for the given time slot.");
                    ModelState.AddModelError("", "Der Raum ist zur angegebenen Zeit bereits reserviert.");
                }
            }
            else
            {
                _logger.LogWarning("Model validation failed for reservation: {Reservation}", reservation);
            }
            return View(reservation);
        }

        // GET: Reservations/VerifyPrivateKey/5
        public IActionResult VerifyPrivateKey(int? id, string returnAction = "Edit")
        {
            if (id == null)
            {
                _logger.LogWarning("VerifyPrivateKey (GET) action invoked with null ID.");
                return NotFound();
            }

            _logger.LogInformation("VerifyPrivateKey (GET) action invoked for reservation ID: {Id}", id);
            var model = new VerifyPrivateKeyViewModel
            {
                ReservationId = id.Value,
                ReturnAction = returnAction // Korrektur des Namens
            };

            return View(model);
        }

        // POST: Reservations/VerifyPrivateKey
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPrivateKey(VerifyPrivateKeyViewModel model)
        {
            _logger.LogInformation("VerifyPrivateKey (POST) action invoked with model: {Model}", model);
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState)
                {
                    foreach (var error in modelState.Value.Errors)
                    {
                        _logger.LogWarning("ModelState Error: {ErrorMessage}", error.ErrorMessage);
                    }
                }
                _logger.LogWarning("Model validation failed for VerifyPrivateKey with model: {Model}", model);
                return View(model);
            }

            if (ModelState.IsValid)
            {
                var reservation = await _context.Reservations.FindAsync(model.ReservationId);

                if (reservation == null)
                {
                    _logger.LogWarning("No reservation found with ID: {Id}", model.ReservationId);
                    return NotFound();
                }

                if (reservation.PrivateKey == model.PrivateKey)
                {
                    _logger.LogInformation("PrivateKey verified successfully for reservation ID: {Id}", model.ReservationId);
                    // PrivateKey in der Session speichern
                    HttpContext.Session.SetString("PrivateKey", model.PrivateKey.ToString());

                    // Weiterleitung zur entsprechenden Ansicht
                    if (model.ReturnAction == "Delete")
                    {
                        _logger.LogInformation("ReturnAction == Delete");
                        return RedirectToAction("Delete", new { id = model.ReservationId }); // ID zur Delete-Action übergeben
                    }
                    return RedirectToAction("Edit", new { id = model.ReservationId });
                }
                else
                {
                    _logger.LogWarning("PrivateKey verification failed for reservation ID: {Id}", model.ReservationId);
                    ModelState.AddModelError(string.Empty, "Der Private Key ist nicht korrekt");
                }
            }
            else
            {
                _logger.LogWarning("Model validation failed for VerifyPrivateKey with model: {Model}", model);
            }
            return View(model);
        }


        // Edit: Formular zum Bearbeiten einer bestehenden Reservierung anzeigen
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit (GET) action invoked with null ID.");
                return NotFound();
            }

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning("No reservation found with ID: {Id}", id);
                return NotFound();
            }

            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");

            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogWarning("Unauthorized access attempt for reservation ID: {Id}", id);
                return Unauthorized();
            }

            _logger.LogInformation("Edit (GET) action invoked for reservation ID: {Id}", id);
            return View(reservation);
        }

        // Edit (POST): Bearbeitete Reservierung speichern
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reservation reservation)
        {
            _logger.LogInformation("Edit (POST) action invoked for reservation ID: {Id}", id);

            if (id != reservation.Id)
            {
                _logger.LogError("The reservation ID does not match the provided ID.");
                return NotFound();
            }

            // Überprüfe, ob der PrivateKey in der Session vorhanden ist und korrekt ist
            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");
            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogError("Unauthorized: The Private Key does not match or is missing for reservation ID: {Id}", id);
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed for reservation ID: {Id}", id);
                return View(reservation);
            }

            try
            {
                _logger.LogInformation("Checking room availability for edited reservation with ID: {Id}", id);

                // Verfügbarkeitsprüfung ohne die eigene Reservierung zu berücksichtigen
                if (_reservationService.IsRoomAvailable(reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime, reservation.Id))
                {
                    _context.Update(reservation);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Reservation updated successfully with ID: {Id}", id);
                }
                else
                {
                    _logger.LogWarning("Room is not available for the given time slot for reservation ID: {Id}", id);
                    ModelState.AddModelError("", "Der Raum ist zur angegebenen Zeit bereits reserviert.");
                    return View(reservation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating reservation with ID: {Id}", id);
            }

            return RedirectToAction(nameof(Index));
        }


        // Delete: Bestätigungsseite anzeigen
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Delete (GET) action invoked with null ID.");
                return NotFound();
            }

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning("No reservation found with ID: {Id}", id);
                return NotFound();
            }

            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");
            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogWarning("Unauthorized access attempt for deletion of reservation ID: {Id}", id);
                return RedirectToAction("VerifyPrivateKey", new { id }); // Weiterleitung zur PrivateKey-Verifizierung
            }

            _logger.LogInformation("Delete (GET) action invoked for reservation ID: {Id}", id);
            return View(reservation);
        }

        // DeleteConfirmed (POST): Reservierung löschen
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("DeleteConfirmed (POST) action invoked for reservation ID: {Id}", id);

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning("Reservation not found for deletion with ID: {Id}", id);
                return NotFound(); // Statt Unauthorized zu verwenden
            }

            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");
            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogWarning("Unauthorized deletion attempt for reservation ID: {Id}", id);
                return Unauthorized();
            }

            try
            {
                _context.Reservations.Remove(reservation);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Reservation deleted successfully with ID: {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting reservation with ID: {Id}", id);
                ModelState.AddModelError("", "Ein Fehler ist beim Löschen der Reservierung aufgetreten.");
                return View(reservation);
            }

            return RedirectToAction(nameof(Index));
        }


        // GET: Reservateion/VerifyPublicKey/5
        public IActionResult VerifyPublicKey(int? id, string returnAction = "Details")
        {
            if(id == null)
            {
                _logger.LogWarning("VerifyPublicKey (GET) action invike with null ID");
                return NotFound();
            }

            _logger.LogInformation($"VerifyPublicKey (GET) action invoke for rservation ID: {id}");
            var model = new VerifyPublicKeyViewModel
            {
                ReservationId = id.Value,
                ReturnAction = returnAction
            };
            return View(model);
        }

        // POST: Reservation/VerifyPublicKey
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPublicKey(VerifyPublicKeyViewModel model)
        {
            _logger.LogInformation($"VerifyPublicKey (Post) action invoke with Model {model}");
            if (!ModelState.IsValid)
            {
                foreach(var modelState in ModelState)
                {
                    foreach(var error in modelState.Value.Errors)
                    {
                        _logger.LogWarning($"ModelState Error: {error.ErrorMessage}");
                    }
                }
                _logger.LogWarning($"Model validation failed for VerifyPublicKey with model: {model}");
                return View(model);
            }

            var reservation = await _context.Reservations.FindAsync(model.ReservationId);

            if(reservation == null)
            {
                _logger.LogWarning($"No reservation found with ID: {model.ReservationId}");
                return NotFound();
            }

            if (reservation.PublicKey == model.PublicKey) 
            {
                _logger.LogInformation($"PublicKey verified successfully for reservation ID:{model.ReservationId}");
                return RedirectToAction("Details", new { id = model.ReservationId });
            }
            else 
            {
                _logger.LogWarning("PublicKey verification failed for reservation ID: {Id}", model.ReservationId);
                ModelState.AddModelError(string.Empty, "Der Public Key ist nicht korrekt");
            }
            return View(model);
        }

        //Details: Details einder bestehenden Reservierung anzeigen
        public async Task<IActionResult> Details(int? id)
        {
            if(id == null)
            {
                _logger.LogWarning($"Details (GET) action invoke with null ID");
                return NotFound();
            }

            var reservation = await _context.Reservations.FindAsync(id);
            if(reservation == null)
            {
                _logger.LogWarning($"No reservation found with ID {id}");
                return NotFound();
            }

            _logger.LogInformation($"Details (GET) action invoke foe reservation ID: {id}");
            return View(reservation);
        }

    }
}
