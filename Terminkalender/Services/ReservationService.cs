using Terminkalender.Data;
using Terminkalender.Models;

namespace Terminkalender.Services
{
    public class ReservationService
    {
        private readonly TerminkalenderContext _context;
        private readonly ILogger<ReservationService> _logger;
        public ReservationService(TerminkalenderContext context, ILogger<ReservationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public bool ValidateTime(TimeOnly startTime, TimeOnly endTime)
        {
            return startTime < endTime;
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
