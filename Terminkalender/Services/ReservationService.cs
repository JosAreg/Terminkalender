using Terminkalender.Data;
using Terminkalender.Models;

namespace Terminkalender.Services
{
    public class ReservationService
    {
        private readonly TerminkalenderContext _context;
        private readonly ILogger<ReservationService> _logger;
        private readonly TimeZoneInfo _timeZone;
        public ReservationService(TerminkalenderContext context, ILogger<ReservationService> logger)
        {
            _context = context;
            _logger = logger;
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }

        public bool ValidateTime(TimeOnly startTime, TimeOnly endTime)
        {
            return startTime < endTime;
        }

        public int GenerateReservationId()
        {
            int id;
            do
            {
                id = new Random().Next(1, int.MaxValue);
            } while (_context.Reservations.Any(r => r.Id == id)); // Prüfe, ob die ID bereits existiert
            return id;
        }


        // Prüfen ob die Startzeit in der Zukunft liegt
        public bool IsReservationInFuture(DateOnly date, TimeOnly startTime) 
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

            var reservationStart = date
                .ToDateTime(TimeOnly.MinValue)
                .AddHours(startTime.Hour)
                .AddMinutes(startTime.Minute);

            return reservationStart >= now;
        }

        public bool IsRoomAvailable(Room room, DateTime date, TimeOnly startTime, TimeOnly endTime, int Id)
        {
            var overlappingReservations = _context.Reservations
                .Where(r => r.Room == room && r.Date.Date == date.Date && r.Id != Id) // Die eigene Reservierung ignorieren
                .ToList();

            foreach (var reservation in overlappingReservations)
            {
                // prüft, ob die Startzeit während einer bestehenden Reservierung liegt
                bool startsDuringExistingReservation = reservation.StartTime < endTime && reservation.StartTime >= startTime;

                // prüft, ob die Endzeit während einer bestehenden Reservierung liegt
                bool endsDuringExistingReservation = reservation.EndTime > startTime && reservation.EndTime <= endTime;

                // prüft, ob eine neue Reservierung vollständig während einer bestehenden Reservierung stattfinden würde
                bool fullyContainsNewReservation = reservation.StartTime <= startTime && reservation.EndTime >= endTime;

                if (startsDuringExistingReservation || endsDuringExistingReservation || fullyContainsNewReservation)
                {
                    _logger.LogWarning("Konflikt entdeckt: Reservierung {ReservationId} überschneidet sich.", reservation.Id);
                    return false;
                }
            }
            _logger.LogInformation("Der Raum ist verfügbar für den angegebenen Zeitraum.");
            return true;
        }
    }
}
