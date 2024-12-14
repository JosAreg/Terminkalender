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

        public async Task<IActionResult> Index(bool showPast = false)
        {
            _logger.LogInformation($"Index action invoked. Show past: {showPast}");

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);

            var reservations = await _context.Reservations
                .Where(r => showPast
                    ? r.Date.AddHours(r.StartTime.Hour).AddMinutes(r.StartTime.Minute) < now
                    : r.Date.AddHours(r.StartTime.Hour).AddMinutes(r.StartTime.Minute) >= now)
                .OrderBy(r => r.Date) 
                .ThenBy(r => r.StartTime) 
                .ToListAsync();

            _logger.LogInformation($"Fetched {reservations.Count} reservations from the database.");
            ViewBag.ShowPast = showPast; 
            _logger.LogInformation($"Current time: {now}");
            return View(reservations);
        }

        public IActionResult Create()
        {
            _logger.LogInformation("Create (GET) action invoked.");
            var reservation = new Reservation
            {
                PrivateKey = Guid.NewGuid(),
                PublicKey = Guid.NewGuid(),
                Date = DateTime.Now,
                Id = _reservationService.GenerateReservationId()
            };
            return View(reservation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            _logger.LogInformation("Create (POST) action invoked with reservation details: {Reservation}", reservation);


            if (reservation.Id == 0)
            {
                _logger.LogError("Die ID wurde nicht korrekt übergeben.");
                ModelState.AddModelError("", "Die ID konnte nicht generiert werden.");
                return View(reservation);
            }

            if (!_reservationService.ValidateTime(reservation.StartTime, reservation.EndTime))
            {
                _logger.LogWarning("Endzeit muss nach Startzeit sein");
                ModelState.AddModelError(string.Empty, "Die Startzeit muss vor der Endzeit sein. Prüfe deine Eingabe");
                return View(reservation);
            }

            if (!_reservationService.IsReservationInFuture(DateOnly.FromDateTime(reservation.Date), reservation.StartTime))
            {
                _logger.LogWarning("Die Reservierung darf nicht in der Vergangenheit beginnen.");
                ModelState.AddModelError(string.Empty, "Die Reservierung darf nicht in der Vergangenheit beginnen.");
                return View(reservation);
            }

            if (!_reservationService.IsRoomAvailable(reservation.Room, reservation.Date, reservation.StartTime, reservation.EndTime, reservation.Id))
            {
                _logger.LogWarning("Room is not available for the given time slot.");
                ModelState.AddModelError("", "Der Raum ist zur angegebenen Zeit bereits reserviert.");
                return View(reservation);
            }

            _context.Add(reservation);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Reservation created successfully with ID: {Id}", reservation.Id);

            return RedirectToAction(nameof(Index));
        }

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
                    HttpContext.Session.SetString("PrivateKey", model.PrivateKey.ToString());

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
                return RedirectToAction("VerifyPrivateKey", new { id }); 
            }
            _logger.LogInformation($"Delete (GET) action invoked for reservation ID: {id}");
            return View(reservation);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation($"DeleteConfirmed (POST) action invoked for reservation ID: {id}");

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning($"Reservation not found for deletion with ID: {id}");
                return NotFound(); 
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
