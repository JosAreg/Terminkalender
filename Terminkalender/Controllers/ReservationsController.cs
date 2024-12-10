using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        public async Task<IActionResult> Index(bool showPast = false)
        {
            _logger.LogInformation($"Index action invoked. Show past: {showPast}");

            // Konvertiere die aktuelle UTC-Zeit in die lokale Zeitzone (+1)
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

            var reservations = await _context.Reservations
                .Where(r => showPast
                    ? r.Date.AddHours(r.StartTime.Hour).AddMinutes(r.StartTime.Minute) < now
                    : r.Date.AddHours(r.StartTime.Hour).AddMinutes(r.StartTime.Minute) >= now)
                .OrderBy(r => r.Date) // Erst nach Datum sortieren
                .ThenBy(r => r.StartTime) // Danach nach Startzeit sortieren
                .ToListAsync();

            _logger.LogInformation($"Fetched {reservations.Count} reservations from the database.");
            ViewBag.ShowPast = showPast; // Flag an View übergeben
            _logger.LogInformation($"Current time: {now}");
            return View(reservations);
        }




        // Create GET
        public IActionResult Create()
        {
            _logger.LogInformation("Create (GET) action invoked.");
            var reservation = new Reservation
            {
                PrivateKey = Guid.NewGuid(),
                PublicKey = Guid.NewGuid(),
                Date = DateTime.Now,
            };
            return View(reservation);
        }

        // Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            _logger.LogInformation("Create (POST) action invoked with reservation details: {Reservation}", reservation);

            // Zeitspanne prüfen, start vor ende
            if (!_reservationService.ValidateTime(reservation.StartTime, reservation.EndTime))
            {
                _logger.LogWarning("Endzeit muss nach Startzeit sein");
                _logger.LogInformation($"StartTime: {reservation.StartTime}, EndTime: {reservation.EndTime}", reservation.StartTime, reservation.EndTime);

                ModelState.AddModelError(string.Empty, "Die Startzeit muss vor der Endzeit sein. Prüfe deine Eingabe");
            }

            // Überprüfung: Startzeit darf nicht in der Vergangenheit liegen
            if (!_reservationService.IsReservationInFuture(DateOnly.FromDateTime(reservation.Date), reservation.StartTime))
            {
                _logger.LogWarning("Die Reservierung darf nicht in der Vergangenheit beginnen.");
                ModelState.AddModelError(string.Empty, "Die Reservierung darf nicht in der Vergangenheit beginnen.");
            }

            if (ModelState.IsValid)
            {
                _logger.LogInformation($"Checking room availability for room: {reservation.Room}, date: {reservation.Date}, start: {reservation.StartTime}, end: {reservation.EndTime}",
                reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime);

                // Prüfen ob der Raum verfügbar ist
                if (_reservationService.IsRoomAvailable(reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime, reservation.Id))
                {
                    //reservation.PrivateKey = Guid.NewGuid();
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

        // VerifyPrivateKey (GET)
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
                ReturnAction = returnAction
            };

            return View(model);
        }

        // VerifyPrivateKey (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPrivateKey(VerifyPrivateKeyViewModel model)
        {
            _logger.LogInformation($"VerifyPrivateKey (POST) action invoked with modelPubK: {model}");
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState)
                {
                    foreach (var error in modelState.Value.Errors)
                    {
                        _logger.LogWarning("ModelState Error: {ErrorMessage}", error.ErrorMessage);
                    }
                }
                _logger.LogWarning($"Model validation failed for VerifyPrivateKey with modelPubK: {model}");
                return View(model);
            }

            if (ModelState.IsValid)
            {
                var reservation = await _context.Reservations.FindAsync(model.ReservationId);

                if (reservation == null)
                {
                    _logger.LogWarning($"No reservation found with ID: {model.ReservationId}");
                    return NotFound();
                }

                if (reservation.PrivateKey == model.PrivateKey)
                {
                    _logger.LogInformation($"PrivateKey verified successfully for reservation ID: {model.ReservationId}");
                    // PrivateKey in der Session speichern
                    HttpContext.Session.SetString("PrivateKey", model.PrivateKey.ToString());

                    // Weiterleitung zur entsprechenden Ansicht
                    if (model.ReturnAction == "Delete")
                    {
                        _logger.LogInformation("ReturnAction == Delete");
                        return RedirectToAction("Delete", new { id = model.ReservationId });
                    }
                    return RedirectToAction("Edit", new { id = model.ReservationId });
                }
                else
                {
                    _logger.LogWarning($"PrivateKey verification failed for reservation ID: {model.ReservationId}");
                    ModelState.AddModelError(string.Empty, "Der Private Key ist nicht korrekt");
                }
            }
            else
            {
                _logger.LogWarning($"Model validation failed for VerifyPrivateKey with modelPubK: {model}");
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
                _logger.LogWarning($"No reservation found with ID: {id}");
                return NotFound();
            }

            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");

            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogWarning($"Unauthorized access attempt for reservation ID: {id}");
                return Unauthorized();
            }

            _logger.LogInformation($"Edit (GET) action invoked for reservation ID: {id}");
            return View(reservation);
        }

        // Edit (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reservation reservation)
        {
            _logger.LogInformation($"Edit (POST) action invoked for reservation ID: {id}");

            if (id != reservation.Id)
            {
                _logger.LogError("The reservation ID does not match the provided ID.");
                return NotFound();
            }

            // Überprüfe, ob der PrivateKey in der Session vorhanden ist und korrekt ist
            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");
            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogError($"Unauthorized: The Private Key does not match or is missing for reservation ID: {id}");
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning($"Model validation failed for reservation ID: {id}");
                return View(reservation);
            }

            try
            {
                _logger.LogInformation($"Checking room availability for edited reservation with ID: {id}");

                // Verfügbarkeitsprüfung ohne die eigene Reservierung zu berücksichtigen
                if (_reservationService.IsRoomAvailable(reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime, reservation.Id))
                {
                    _context.Update(reservation);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Reservation updated successfully with ID: {id}");
                }
                else
                {
                    _logger.LogWarning($"Room is not available for the given time slot for reservation ID: {id}");
                    ModelState.AddModelError("", "Der Raum ist zur angegebenen Zeit bereits reserviert.");
                    return View(reservation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating reservation with ID: {id}");
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
                _logger.LogWarning($"No reservation found with ID: {id}");
                return NotFound();
            }

            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");
            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogWarning($"Unauthorized access attempt for deletion of reservation ID: {id}");
                return RedirectToAction("VerifyPrivateKey", new { id }); // Weiterleitung zur PrivateKey-Verifizierung
            }
            _logger.LogInformation($"Delete (GET) action invoked for reservation ID: {id}");
            return View(reservation);
        }

        // DeleteConfirmed (POST): Reservierung löschen
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation($"DeleteConfirmed (POST) action invoked for reservation ID: {id}");

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning($"Reservation not found for deletion with ID: {id}");
                return NotFound(); // Statt Unauthorized zu verwenden
            }

            var sessionPrivateKey = HttpContext.Session.GetString("PrivateKey");
            if (sessionPrivateKey == null || reservation.PrivateKey.ToString() != sessionPrivateKey)
            {
                _logger.LogWarning($"Unauthorized deletion attempt for reservation ID: {id}");
                return Unauthorized();
            }

            try
            {
                _context.Reservations.Remove(reservation);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Reservation deleted successfully with ID: {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while deleting reservation with ID: {id}");
                ModelState.AddModelError("", "Ein Fehler ist beim Löschen der Reservierung aufgetreten.");
                return View(reservation);
            }

            return RedirectToAction(nameof(Index));
        }


        // GET: Reservateion/VerifyPublicKey/5
        public IActionResult VerifyPublicKey(int? id, string returnAction = "Details")
        {
            if (id == null)
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
        public async Task<IActionResult> VerifyPublicKey(VerifyPublicKeyViewModel modelPubK)
        {
            _logger.LogInformation($"VerifyPublicKey (Post) action invoke with Model {modelPubK}");
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState)
                {
                    foreach (var error in modelState.Value.Errors)
                    {
                        _logger.LogWarning($"ModelState Error: {error.ErrorMessage}");
                    }
                }
                _logger.LogWarning($"Model validation failed for VerifyPublicKey with modelPubK: {modelPubK}");
                return View(modelPubK);
            }

            var reservation = await _context.Reservations.FindAsync(modelPubK.ReservationId);

            if (reservation == null)
            {
                _logger.LogWarning($"No reservation found with ID: {modelPubK.ReservationId}");
                return NotFound();
            }

            if (reservation.PublicKey == modelPubK.PublicKey)
            {
                _logger.LogInformation($"PublicKey verified successfully for reservation ID:{modelPubK.ReservationId}");
                return RedirectToAction("Details", new { id = modelPubK.ReservationId });
            }
            else
            {
                _logger.LogWarning("PublicKey verification failed for reservation ID: {Id}", modelPubK.ReservationId);
                ModelState.AddModelError(string.Empty, "Der Public Key ist nicht korrekt");
            }
            return View(modelPubK);
        }

        //Details: Details einder bestehenden Reservierung anzeigen
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning($"Details (GET) action invoke with null ID");
                return NotFound();
            }

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning($"No reservation found with ID {id}");
                return NotFound();
            }
            _logger.LogInformation($"Details (GET) action invoke foe reservation ID: {id}");
            return View(reservation);
        }
    }
}
