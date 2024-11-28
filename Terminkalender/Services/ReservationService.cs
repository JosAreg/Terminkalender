using Terminkalender.Data;
using Terminkalender.Models;
using Serilog;
using NuGet.Protocol;

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

        public bool IsRoomAvailable(Room room, DateTime date, TimeOnly startTime, TimeOnly endTime)
        {
            var overlappingReservations = _context.Reservations // holt aus der Datenbank: Alle reservationen an diesem Tag für diesen Raum und erstellt weine Liste
                .Where(r => r.Room == room && r.Date == date)
                .ToList();

            foreach (var reservation in overlappingReservations)
            {
                // prüft ob eine Startzeit während einer bestehenden 
                bool startsDuringExistingReservations = reservation.StartTime < endTime && reservation.StartTime >= startTime; 
                // prüft ob die Endzeit in einer Bestehenden Reservierung ist
                bool endsDuringExistingReservation = reservation.EndTime > startTime && reservation.EndTime <= endTime;
                // prüft ob eine neue Reservierung vollständig während einer Reservierung stattfinden würde
                bool fullyContainsNewReservation = reservation.StartTime <= startTime && reservation.EndTime >= endTime;

                if (startsDuringExistingReservations || endsDuringExistingReservation || fullyContainsNewReservation)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
